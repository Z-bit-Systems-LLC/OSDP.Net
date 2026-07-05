using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;
using OSDP.Net.Pairing;
using OSDP.Net.Tests.Utilities;

namespace OSDP.Net.Tests.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class PairingIntegrationTests
{
    private static readonly DeviceIdentity AcuIdentity = new("ACME Controllers", "ACU-9", "ACU-0001");
    private static readonly DeviceIdentity PdIdentity = new("ACME Access", "AR-200", "PD-0001");

    private ILoggerFactory _loggerFactory;

    [SetUp]
    public void Setup()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning).AddConsole());
    }

    [TearDown]
    public void Teardown() => _loggerFactory?.Dispose();

    [Test]
    public async Task Pairing_FullExchange_DerivesMatchingScbkAndAuthenticatesIdentity()
    {
        var demoCa = CertificateAuthority.Demo();
        byte[] pdScbk = null;
        var pdConfig = BuildPdConfig(demoCa, scbk =>
        {
            pdScbk = scbk;
            return true;
        });

        await using var harness = await Harness.StartCleartextPairingDevice(_loggerFactory, pdConfig);

        // Synchronous collector preserves report order (unlike Progress<T>, which marshals).
        var progress = new PairingProgressCollector();

        var result = await harness.Panel.PairDevice(harness.ConnectionId, harness.Address,
            BuildAcuConfig(demoCa), maximumFragmentSize: 128, timeout: TimeSpan.FromSeconds(20),
            progress: progress);

        Assert.Multiple(() =>
        {
            Assert.That(result.Scbk.Length, Is.EqualTo(32));
            Assert.That(result.Scbk, Is.EqualTo(pdScbk), "ACU and PD must derive the same SCBK");
            Assert.That(result.PeerIdentity.SerialNumber, Is.EqualTo(PdIdentity.SerialNumber));
            Assert.That(result.PeerIdentity.Manufacturer, Is.EqualTo(PdIdentity.Manufacturer));
            Assert.That(progress.Reports, Is.Not.Empty, "Pairing should report progress");
            Assert.That(progress.Reports[^1].Stage, Is.EqualTo(PairingStage.Completed));
            Assert.That(progress.Reports[^1].Fraction, Is.EqualTo(1.0));
            Assert.That(progress.Reports.Select(p => p.Fraction), Is.Ordered, "Fraction should be monotonic");
        });
    }

    private sealed class PairingProgressCollector : IProgress<PairingProgress>
    {
        public System.Collections.Generic.List<PairingProgress> Reports { get; } = new();

        public void Report(PairingProgress value) => Reports.Add(value);
    }

    [Test]
    public async Task Pairing_ThenSc2WithDerivedKey_EstablishesEncryptedChannel()
    {
        var demoCa = CertificateAuthority.Demo();
        byte[] pdScbk = null;
        var pdConfig = BuildPdConfig(demoCa, scbk =>
        {
            pdScbk = scbk;
            return true;
        });

        PairingResult result;
        await using (var pairingHarness = await Harness.StartCleartextPairingDevice(_loggerFactory, pdConfig))
        {
            result = await pairingHarness.Panel.PairDevice(pairingHarness.ConnectionId, pairingHarness.Address,
                BuildAcuConfig(demoCa), timeout: TimeSpan.FromSeconds(20));
        }

        Assert.That(result.Scbk, Is.EqualTo(pdScbk));

        // The derived SCBK drives the standard symmetric SC2 handshake on a fresh connection.
        await using var secureHarness = await Harness.StartSecureChannelV2Device(_loggerFactory, result.Scbk);
        var capabilities = await secureHarness.Panel.DeviceCapabilities(secureHarness.ConnectionId,
            secureHarness.Address);

        Assert.That(capabilities, Is.Not.Null, "Encrypted SC2 command should succeed with the paired key");
    }

    [Test]
    public async Task Pairing_ThenSc2OnSameConnection_EstablishesInPlaceWithoutReconnect()
    {
        // The live scenario: pair, then switch to SC2 on the SAME connection. The PD activates the
        // derived key on its running channel in place (no reconnect), so the ACU's SC2 handshake
        // establishes deterministically.
        var demoCa = CertificateAuthority.Demo();
        byte[] pdScbk = null;
        var pdConfig = BuildPdConfig(demoCa, scbk =>
        {
            pdScbk = scbk;
            return true;
        });

        await using var harness = await Harness.StartCleartextPairingDevice(_loggerFactory, pdConfig);

        var result = await harness.Panel.PairDevice(harness.ConnectionId, harness.Address,
            BuildAcuConfig(demoCa), timeout: TimeSpan.FromSeconds(20));
        Assert.That(result.Scbk, Is.EqualTo(pdScbk));

        // Same connection: switch the ACU to SC2 with the derived key (as the console does).
        harness.Panel.AddDevice(harness.ConnectionId, harness.Address, true, true, result.Scbk,
            SecureChannelVersion.V2);

        // The encrypted SC2 command succeeds once the handshake establishes over the same link.
        OSDP.Net.Model.ReplyData.DeviceCapabilities capabilities = null;
        for (var attempt = 0; attempt < 40 && capabilities == null; attempt++)
        {
            try
            {
                capabilities = await harness.Panel.DeviceCapabilities(harness.ConnectionId, harness.Address);
            }
            catch (Exception)
            {
                await Task.Delay(250);
            }
        }

        Assert.That(capabilities, Is.Not.Null,
            "SC2 should establish in place on the same connection after pairing");
    }

    [Test]
    public async Task Pairing_WhenDeviceNotConfiguredForPairing_IsRejected()
    {
        // Opt-in regression: a symmetric-only device (Pairing == null) must NAK the pairing command.
        await using var harness = await Harness.StartCleartextPairingDevice(_loggerFactory, pairingConfig: null);

        var demoCa = CertificateAuthority.Demo();
        Assert.ThrowsAsync<PairingException>(async () => await harness.Panel.PairDevice(harness.ConnectionId,
            harness.Address, BuildAcuConfig(demoCa), timeout: TimeSpan.FromSeconds(5)));
    }

    [Test]
    public async Task Pairing_WhenPersistenceFails_ReportsPersistenceFailedAndDoesNotCommit()
    {
        var demoCa = CertificateAuthority.Demo();
        var pdConfig = BuildPdConfig(demoCa, _ => false); // persistence callback fails

        await using var harness = await Harness.StartCleartextPairingDevice(_loggerFactory, pdConfig);

        var ex = Assert.ThrowsAsync<PairingException>(async () => await harness.Panel.PairDevice(
            harness.ConnectionId, harness.Address, BuildAcuConfig(demoCa), timeout: TimeSpan.FromSeconds(20)));
        Assert.That(ex.Status, Is.EqualTo(PairingStatus.PersistenceFailed));
    }

    [Test]
    public async Task SymmetricOnlySc2_WithPreSharedKey_StillEstablishes()
    {
        // Regression: pure "V2 AES-256 only" pre-shared-key SC2 works unchanged.
        var scbk = new byte[32];
        for (var i = 0; i < scbk.Length; i++)
        {
            scbk[i] = (byte)(0x20 + i);
        }

        await using var harness = await Harness.StartSecureChannelV2Device(_loggerFactory, scbk);
        var capabilities = await harness.Panel.DeviceCapabilities(harness.ConnectionId, harness.Address);
        Assert.That(capabilities, Is.Not.Null);
    }

    private static PairingConfiguration BuildAcuConfig(CertificateAuthority ca)
    {
        var credentials = PairingCredentials.Generate(AcuIdentity, ca);
        return new PairingConfiguration(credentials, PairingTrustAnchor.FromCa(ca));
    }

    private static PairingConfiguration BuildPdConfig(CertificateAuthority ca, Func<byte[], bool> persist)
    {
        var credentials = PairingCredentials.Generate(PdIdentity, ca);
        return new PairingConfiguration(credentials, PairingTrustAnchor.FromCa(ca))
        {
            OnScbkEstablished = (result, _) => Task.FromResult(persist(result.Scbk))
        };
    }

    private sealed class Harness : IAsyncDisposable
    {
        private LoopbackOsdpConnection _acuConnection;
        private LoopbackOsdpConnection _deviceConnection;
        private SingleConnectionListener _listener;
        private Device _device;

        internal ControlPanel Panel { get; private set; }
        internal Guid ConnectionId { get; private set; }
        internal byte Address { get; private set; }

        internal static Task<Harness> StartCleartextPairingDevice(ILoggerFactory loggerFactory,
            PairingConfiguration pairingConfig)
        {
            var config = new DeviceConfiguration(new ClientIdentification([0x01, 0x02, 0x03], 12345))
            {
                Address = 0,
                RequireSecurity = false,
                // Pairing targets SC2, so the device runs an (unsecured) SC2 channel using a 32-byte
                // placeholder key until pairing derives and activates the real SCBK in place.
                SecurityKey = new byte[32],
                SecureChannelVersion = SecureChannelVersion.V2,
                Pairing = pairingConfig
            };
            return Start(loggerFactory, config, useSecureChannel: false, securityKey: null,
                SecureChannelVersion.V1);
        }

        internal static Task<Harness> StartSecureChannelV2Device(ILoggerFactory loggerFactory, byte[] scbk)
        {
            var config = new DeviceConfiguration(new ClientIdentification([0x01, 0x02, 0x03], 12345))
            {
                Address = 0,
                RequireSecurity = true,
                SecurityKey = scbk,
                SecureChannelVersion = SecureChannelVersion.V2
            };
            return Start(loggerFactory, config, useSecureChannel: true, securityKey: scbk, SecureChannelVersion.V2);
        }

        private static async Task<Harness> Start(ILoggerFactory loggerFactory, DeviceConfiguration config,
            bool useSecureChannel, byte[] securityKey, SecureChannelVersion version)
        {
            var harness = new Harness { Address = config.Address };
            (harness._acuConnection, harness._deviceConnection) = LoopbackOsdpConnection.CreatePair();

            harness._device = new TestDevice(config, loggerFactory);
            harness._listener = new SingleConnectionListener(harness._deviceConnection);
            await harness._device.StartListening(harness._listener);

            harness.Panel = new ControlPanel(loggerFactory);
            var online = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.ConnectionId = harness.Panel.StartConnection(harness._acuConnection);
            harness.Panel.ConnectionStatusChanged += (_, e) =>
            {
                if (e.ConnectionId == harness.ConnectionId && e.IsConnected)
                {
                    online.TrySetResult(true);
                }
            };

            harness.Panel.AddDevice(harness.ConnectionId, config.Address, true, useSecureChannel, securityKey, version);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using (cts.Token.Register(() => online.TrySetCanceled()))
            {
                await online.Task;
            }

            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            await (Panel?.Shutdown() ?? Task.CompletedTask);
            await (_device?.StopListening() ?? Task.CompletedTask);
            _device?.Dispose();
            _listener?.Dispose();
            _acuConnection?.Dispose();
            _deviceConnection?.Dispose();
        }
    }
}
