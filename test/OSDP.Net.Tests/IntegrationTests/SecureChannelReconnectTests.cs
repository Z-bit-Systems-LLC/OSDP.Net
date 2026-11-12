using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OSDP.Net.Model;
using OSDP.Net.Tests.Utilities;

namespace OSDP.Net.Tests.IntegrationTests;

/// <summary>
/// An ACU that shuts down and reconnects must be able to set up a new secure channel with a PD
/// that is still holding the previous session.
/// </summary>
/// <remarks>
/// Runs over <see cref="TimedWire"/>, where bytes take real line time to cross, so a reply that is
/// still on the wire when the ACU shuts down reaches the ACU's next session, as it does on a real
/// line or a simulated one.
/// </remarks>
[Category("Integration")]
public class SecureChannelReconnectTests
{
    private const byte DeviceAddress = 0;
    private const byte DeviceCapabilitiesCommandCode = 0x62;
    private const byte SessionChallengeBlockType = 0x11;
    private static readonly TimeSpan SecureChannelTimeout = TimeSpan.FromSeconds(8);

    private ILoggerFactory _loggerFactory;
    private TimedWire _wire;
    private TestDevice _device;

    [SetUp]
    public void Setup()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder
            .AddFilter("OSDP.Net", LogLevel.Debug)
            .SetMinimumLevel(LogLevel.Debug)
            .AddSimpleConsole(options => options.TimestampFormat = "mm:ss.fff "));

        _wire = new TimedWire();
    }

    [TearDown]
    public async Task Teardown()
    {
        await (_device?.StopListening() ?? Task.CompletedTask);
        _device?.Dispose();
        _loggerFactory?.Dispose();
    }

    private static readonly object[] Keys =
    [
        new object[] { "default key", IntegrationConsts.DefaultSCBK },
        new object[] { "custom key", IntegrationConsts.NonDefaultSCBK }
    ];

    [TestCaseSource(nameof(Keys))]
    public async Task GivenEstablishedSecureChannel_WhenSamePanelReconnects_SecureChannelIsEstablishedAgain(
        string description, byte[] securityKey)
    {
        await StartDevice(securityKey);
        var panel = new ControlPanel(_loggerFactory);

        try
        {
            await ConnectSecurely(panel, securityKey, "first session");
            await panel.Shutdown();

            await ConnectSecurely(panel, securityKey, "second session");
            await panel.Shutdown();

            await ConnectSecurely(panel, securityKey, "third session");
        }
        finally
        {
            await panel.Shutdown();
        }
    }

    /// <summary>
    /// The previous session's last reply is still crossing the wire when the ACU reconnects, so it
    /// arrives while the ACU waits for the reply to its new osdp_CHLNG. The ACU must recognise that it
    /// is not the reply to the challenge (its sequence number doesn't match, OSDP v2.2.2 Table 2) and
    /// keep waiting, rather than take it as the reply and fall one reply behind for the rest of the
    /// handshake.
    /// </summary>
    [TestCaseSource(nameof(Keys))]
    public async Task GivenReplyStillOnTheWire_WhenPanelReconnects_SecureChannelIsEstablished(
        string description, byte[] securityKey)
    {
        await StartDevice(securityKey);

        foreach (var reusePanel in new[] { true, false })
        {
            var panel = new ControlPanel(_loggerFactory);
            try
            {
                var connectionId = await ConnectSecurely(panel, securityKey, "first session");
                await ShutdownWithReplyInFlight(panel, connectionId);

                if (!reusePanel) panel = new ControlPanel(_loggerFactory);

                await ConnectSecurely(panel, securityKey,
                    reusePanel ? "second session, same panel" : "second session, new panel");
            }
            finally
            {
                await panel.Shutdown();
            }
        }
    }

    private async Task ShutdownWithReplyInFlight(ControlPanel panel, Guid connectionId)
    {
        var commandArrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnPdReceived(byte[] data)
        {
            if (CommandCode(data) == DeviceCapabilitiesCommandCode) commandArrived.TrySetResult(true);
        }

        _wire.PdReceived += OnPdReceived;
        try
        {
            // Like OSDP Bench reading the capabilities just as the user disconnects: the PD has the
            // command and is replying when the panel shuts down
            var capabilities = panel.DeviceCapabilities(connectionId, DeviceAddress);
            var arrived = await Task.WhenAny(commandArrived.Task, Task.Delay(SecureChannelTimeout));
            Assert.That(arrived, Is.SameAs(commandArrived.Task), "osdp_CAP never reached the PD");

            await panel.Shutdown();

            // Cut off by the shutdown, as intended. It only gives up once its reply times out, so
            // don't wait for it: the reconnect has to happen while its reply is still on the wire.
            _ = capabilities.ContinueWith(task => task.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
        finally
        {
            _wire.PdReceived -= OnPdReceived;
        }
    }

    private static byte? SecurityBlockType(byte[] data)
    {
        var start = Array.IndexOf(data, (byte)0x53);
        if (start < 0 || data.Length < start + 7) return null;
        return (data[start + 4] & 0x08) != 0 ? data[start + 6] : null;
    }

    // The command code follows the header and any security control block; it is never encrypted
    private static byte? CommandCode(byte[] data)
    {
        var start = Array.IndexOf(data, (byte)0x53);
        if (start < 0 || data.Length < start + 6) return null;
        var control = data[start + 4];
        var codeIndex = start + 5 + ((control & 0x08) != 0 ? data[start + 5] : 0);
        return codeIndex < data.Length ? data[codeIndex] : null;
    }

    private async Task StartDevice(byte[] securityKey)
    {
        var configuration = new DeviceConfiguration(new ClientIdentification([0x01, 0x02, 0x03], 12345))
        {
            Address = DeviceAddress,
            SecurityKey = securityKey,
            RequireSecurity = true
        };

        _device = new TestDevice(configuration, _loggerFactory);
        await _device.StartListening(new SingleConnectionListener(_wire.PdEnd));
    }

    private async Task<Guid> ConnectSecurely(ControlPanel panel, byte[] securityKey, string stage)
    {
        var secured = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionId = Guid.Empty;
        var challenges = 0;

        void OnStatusChanged(object sender, ControlPanel.ConnectionStatusEventArgs e)
        {
            if (e.ConnectionId == connectionId && e.IsConnected && e.IsSecureChannelEstablished)
            {
                secured.TrySetResult(true);
            }
        }

        void OnPdReceived(byte[] data)
        {
            if (SecurityBlockType(data) == SessionChallengeBlockType) challenges++;
        }

        panel.ConnectionStatusChanged += OnStatusChanged;
        _wire.PdReceived += OnPdReceived;
        try
        {
            connectionId = panel.StartConnection(_wire.AcuEnd);
            panel.AddDevice(connectionId, DeviceAddress, true, true, securityKey);

            var completed = await Task.WhenAny(secured.Task, Task.Delay(SecureChannelTimeout));
            Assert.That(completed, Is.SameAs(secured.Task),
                $"Secure channel was not established for the {stage}");
            Assert.That(challenges, Is.EqualTo(1),
                $"The ACU had to challenge the PD more than once for the {stage}, so a reply was mismatched");

            var identification = await panel.IdReport(connectionId, DeviceAddress);
            Assert.That(identification, Is.Not.Null, $"osdp_ID over the secure channel failed for the {stage}");

            return connectionId;
        }
        finally
        {
            panel.ConnectionStatusChanged -= OnStatusChanged;
            _wire.PdReceived -= OnPdReceived;
        }
    }
}
