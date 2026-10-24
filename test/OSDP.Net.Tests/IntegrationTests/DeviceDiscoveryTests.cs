using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.PanelCommands.DeviceDiscover;
using OSDP.Net.Tests.Utilities;
using DeviceCapabilities = OSDP.Net.Model.ReplyData.DeviceCapabilities;

namespace OSDP.Net.Tests.IntegrationTests;

/// <summary>
/// Covers the default key check at the end of <see cref="ControlPanel.DiscoverDevice"/>, which used to
/// wait its full 8 second timeout whenever the PD turned down SCBK-D.
/// </summary>
[TestFixture]
[Category("Integration")]
public class DeviceDiscoveryTests
{
    // Well under the 8 second timeout, but loose enough for a slow build agent
    private static readonly TimeSpan EarlyFinish = TimeSpan.FromSeconds(4);

    private static readonly DeviceCapability[] SecureChannelCapabilities =
    [
        new(CapabilityFunction.CheckCharacterSupport, 1, 0),
        new(CapabilityFunction.CommunicationSecurity, 1, 1)
    ];

    private ILoggerFactory _loggerFactory;
    private LoopbackOsdpConnection _acuConnection;
    private LoopbackOsdpConnection _deviceConnection;
    private SingleConnectionListener _connectionListener;
    private TestDevice _device;
    private ScriptedPd _scriptedPd;
    private ControlPanel _panel;

    [SetUp]
    public void Setup()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("OSDP.Net", LogLevel.Debug)
                .SetMinimumLevel(LogLevel.Debug)
                .AddConsole();
        });

        // NUnit reuses the fixture instance, so clear whatever the last test started
        _connectionListener = null;
        _device = null;
        _scriptedPd = null;

        (_acuConnection, _deviceConnection) = LoopbackOsdpConnection.CreatePair();
        _panel = new ControlPanel(_loggerFactory);
    }

    [TearDown]
    public async Task Teardown()
    {
        await (_panel?.Shutdown() ?? Task.CompletedTask);
        await (_device?.StopListening() ?? Task.CompletedTask);
        await (_scriptedPd?.Stop() ?? Task.CompletedTask);

        _device?.Dispose();
        _connectionListener?.Dispose();
        _acuConnection?.Dispose();
        _deviceConnection?.Dispose();
        _loggerFactory?.Dispose();
    }

    [Test]
    public async Task PdAcceptsDefaultKey_ReportsDefaultKey()
    {
        // Arrange
        await StartDevice(config => config.SecurityKey = SecurityContext.DefaultKey);

        // Act
        var (result, _) = await Discover();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(DiscoveryStatus.Succeeded));
            Assert.That(result.UsesDefaultSecurityKey, Is.True);
        });
    }

    [Test]
    public async Task PdNaksDefaultKeyChallenge_EndsEarlyWithoutDefaultKey()
    {
        // Arrange
        await StartDevice(config =>
        {
            config.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            config.AllowUnsecured = [CommandType.IdReport, CommandType.DeviceCapabilities];
        });

        // Act
        var (result, keyCheckDuration) = await Discover();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(DiscoveryStatus.Succeeded));
            Assert.That(result.UsesDefaultSecurityKey, Is.False);
            Assert.That(keyCheckDuration, Is.LessThan(EarlyFinish));
        });
    }

    [Test]
    public async Task PdAnswersWithWrongClientCryptogram_EndsEarlyWithoutDefaultKey()
    {
        // Arrange
        _scriptedPd = new ScriptedPd(_deviceConnection, SecureChannelCapabilities,
            () => new ChallengeResponse(new byte[8], new byte[8], new byte[16], true));

        // Act
        var (result, keyCheckDuration) = await Discover();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(DiscoveryStatus.Succeeded));
            Assert.That(result.UsesDefaultSecurityKey, Is.False);
            Assert.That(keyCheckDuration, Is.LessThan(EarlyFinish));
        });
    }

    [Test]
    public async Task PdAnswersWithWrongKeyType_EndsEarlyWithoutDefaultKey()
    {
        // Arrange
        _scriptedPd = new ScriptedPd(_deviceConnection, SecureChannelCapabilities,
            () => new ChallengeResponse(new byte[8], new byte[8], new byte[16], false));

        // Act
        var (result, keyCheckDuration) = await Discover();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(DiscoveryStatus.Succeeded));
            Assert.That(result.UsesDefaultSecurityKey, Is.False);
            Assert.That(keyCheckDuration, Is.LessThan(EarlyFinish));
        });
    }

    [Test]
    public async Task PdReportsNoSecureChannelSupport_SkipsDefaultKeyCheck()
    {
        // Arrange
        _scriptedPd = new ScriptedPd(_deviceConnection,
            [new DeviceCapability(CapabilityFunction.CommunicationSecurity, 0, 0)],
            () => new ChallengeResponse(new byte[8], new byte[8], new byte[16], true));

        // Act
        var (result, _) = await Discover();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(DiscoveryStatus.Succeeded));
            Assert.That(result.UsesDefaultSecurityKey, Is.False);
            Assert.That(_scriptedPd.ChallengeCount, Is.Zero);
        });
    }

    [Test]
    public async Task CancelledDuringDefaultKeyCheck_ReportsCancelled()
    {
        // Arrange
        _scriptedPd = new ScriptedPd(_deviceConnection, SecureChannelCapabilities, () => null);
        using var cancellation = new CancellationTokenSource();
        var statuses = new List<DiscoveryStatus>();
        var options = new DiscoveryOptions
        {
            CancellationToken = cancellation.Token,
            ProgressCallback = current =>
            {
                statuses.Add(current.Status);
                if (current.Status == DiscoveryStatus.CapabilitiesDiscovered)
                {
                    // Progress is only reported while the discovery below is awaited
                    // ReSharper disable once AccessToDisposedClosure
                    cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));
                }
            }
        };

        // Act
        await Assert.CatchAsync<OperationCanceledException>(async () =>
            await _panel.DiscoverDevice([_acuConnection], options));

        // Assert
        Assert.That(statuses.Last(), Is.EqualTo(DiscoveryStatus.Cancelled));
    }

    private async Task StartDevice(Action<DeviceConfiguration> configureDevice)
    {
        var config = new DeviceConfiguration(new ClientIdentification([0x01, 0x02, 0x03], 12345))
        {
            Address = IntegrationConsts.DefaultTestDeviceAddr
        };
        configureDevice(config);

        _connectionListener = new SingleConnectionListener(_deviceConnection);
        _device = new TestDevice(config, _loggerFactory);
        await _device.StartListening(_connectionListener);
    }

    /// <summary>
    /// Runs discovery and measures the default key check, which is everything after the capabilities are read.
    /// </summary>
    private async Task<(DiscoveryResult Result, TimeSpan KeyCheckDuration)> Discover()
    {
        var keyCheck = new Stopwatch();
        var options = new DiscoveryOptions
        {
            ProgressCallback = current =>
            {
                if (current.Status == DiscoveryStatus.CapabilitiesDiscovered) keyCheck.Start();
                if (current.Status == DiscoveryStatus.Succeeded) keyCheck.Stop();
            }
        };

        var result = await _panel.DiscoverDevice([_acuConnection], options);

        Assert.That(result, Is.Not.Null, "Device was not discovered");
        return (result, keyCheck.Elapsed);
    }

    /// <summary>
    /// A PD that answers in clear text from a script, for osdp_CCRYPT replies the <see cref="Device"/> class
    /// never sends. Every address is answered, so discovery finds it at address 0.
    /// </summary>
    private sealed class ScriptedPd
    {
        private readonly IOsdpConnection _connection;
        private readonly DeviceCapability[] _capabilities;
        private readonly Func<PayloadData> _challengeReply;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _replyTask;
        private int _challengeCount;

        /// <param name="connection">PD side of the connection.</param>
        /// <param name="capabilities">Capabilities reported in osdp_PDCAP.</param>
        /// <param name="challengeReply">Reply to each osdp_CHLNG, or null to send nothing.</param>
        public ScriptedPd(IOsdpConnection connection, DeviceCapability[] capabilities,
            Func<PayloadData> challengeReply)
        {
            _connection = connection;
            _capabilities = capabilities;
            _challengeReply = challengeReply;
            _replyTask = Task.Run(ReplyToCommands);
        }

        public int ChallengeCount => Volatile.Read(ref _challengeCount);

        public async Task Stop()
        {
            await _cancellation.CancelAsync();
            await _replyTask;
            _cancellation.Dispose();
        }

        private async Task ReplyToCommands()
        {
            var cancellationToken = _cancellation.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                var commandBuffer = new Collection<byte>();
                if (!await Bus.WaitForStartOfMessage(_connection, commandBuffer, TimeSpan.FromSeconds(8),
                        cancellationToken) ||
                    !await Bus.WaitForMessageLength(_connection, commandBuffer, cancellationToken) ||
                    !await Bus.WaitForRestOfMessage(_connection, commandBuffer,
                        Bus.ExtractMessageLength(commandBuffer), cancellationToken))
                {
                    continue;
                }

                var command = new IncomingMessage(commandBuffer.ToArray().AsSpan(), new PdMessageSecureChannelBase());
                PayloadData reply = (CommandType)command.Type switch
                {
                    CommandType.IdReport => new DeviceIdentification([0x01, 0x02, 0x03], 4, 5, 6, 7, 8, 9),
                    CommandType.DeviceCapabilities => new DeviceCapabilities(_capabilities),
                    CommandType.SessionChallenge => ReplyToChallenge(),
                    _ => new Ack()
                };

                if (reply != null)
                {
                    await _connection.WriteAsync(new OutgoingReply(command, reply).BuildMessage(null));
                }
            }
        }

        private PayloadData ReplyToChallenge()
        {
            Interlocked.Increment(ref _challengeCount);
            return _challengeReply();
        }
    }
}
