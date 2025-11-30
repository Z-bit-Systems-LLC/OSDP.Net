using System;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Model;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using DeviceCapabilities = OSDP.Net.Model.ReplyData.DeviceCapabilities;
using System.Linq;
using Moq;

namespace OSDP.Net.Tests.IntegrationTests;

//
// NOTE: The Majority of naming/structure in this file is very much a work-in-progress
// and will be updated if we continue to build out a set of integration tests
//
// Presently this is a POC experiment to see how far we can take a test harness that
// instantiates the ControlPanel (ACU) and a Device (PD) and establishes a set of
// full end-to-end test scenarios between those components.
//
// If/when this continues to evolve, we'll need refactor the tests into a set of helpers
// such that device setup/teardown code isn't repeated. And then a helper or two will 
// need to be added to make it easy/clear to write assertions which wait for certain
// events to occur (e.g. device came online).
//
// NOTE: Integration tests by nature are SLOWER than unit tests. Hence, why they are 
// tagged with "Integration" category as we might want to exclude them at some point if
// the default PR test checks become too slow. There's only 5 tests here and they already
// take 25 sec to run. Then again, this might also highlight improvement opportunity
// in...
//   - ControlPanel and/or Device connection establishment logic. 
//   - ControlPanel's ability to signal when connection failed (right now we know when
//     it succeeds but if it doesn't we just keep trying until 10 sec timeout).
//



[TestFixture]
[Category("Integration")]
public class PeripheryDeviceTest : IntegrationTestFixtureBase
{
    public static TestCaseData[] EstablishingAcuToPdConnectionTestCases => [
        //// PD requires Security; ACU opens secure channel; both use same key ==> OK
        new (IntegrationConsts.NonDefaultSCBK, IntegrationConsts.NonDefaultSCBK, true, true, true),
        new (IntegrationConsts.DefaultSCBK, IntegrationConsts.DefaultSCBK, true, true, true),

        //// PD requires Security; ACU opens secure channel; one side uses default key; other uses different key ==> NO
        new (IntegrationConsts.DefaultSCBK, IntegrationConsts.NonDefaultSCBK, true, true, false),
        new (IntegrationConsts.NonDefaultSCBK, IntegrationConsts.DefaultSCBK, true, true, false),

        //// PD doesn't require Security; ACU doesn't use secure channel; two sides use different keys ==> OK
        new (IntegrationConsts.NonDefaultSCBK, IntegrationConsts.DefaultSCBK, false, false, true),

        //// PD doesn't require Security; ACU opens secure channel; two sides use different keys ==> NO
        new (IntegrationConsts.NonDefaultSCBK, IntegrationConsts.DefaultSCBK, false, true, false),
        new (IntegrationConsts.DefaultSCBK, IntegrationConsts.NonDefaultSCBK, false, true, false),

        //// PD doesn't require Security; ACU opens secure channel; both sides use same key ==> OK
        new (IntegrationConsts.NonDefaultSCBK, IntegrationConsts.NonDefaultSCBK, false, true, true),
        new (IntegrationConsts.DefaultSCBK, IntegrationConsts.DefaultSCBK, false, true, true),
    ];

    [TestCaseSource(nameof(EstablishingAcuToPdConnectionTestCases))]
    public async Task TestEstablishingAcuToPdConnection(
        byte[] deviceKey, byte[] panelKey, 
        bool pdRequireSecurity, bool panelUseSecureChannel, bool expectConnectSuccess)
    {
        await InitTestTargets(cfg => {
            cfg.SecurityKey = deviceKey;
            cfg.RequireSecurity = pdRequireSecurity;
        });

        AddDeviceToPanel(panelKey, useSecureChannel: panelUseSecureChannel);

        if (expectConnectSuccess)
        {
            await WaitForDeviceOnlineStatus();
        }
        else
        {
            await AssertPanelRemainsDisconnected();
        }
    }

    [Test]
    public async Task VerifyDeviceWillIgnoreCommandsSentToDifferentAddress()
    {
        await InitTestTargets();

        AddDeviceToPanel(address: 5);

        await AssertPanelRemainsDisconnected();
    }

    [Test]
    public async Task KeySetCommandAbleToChangeSecureChannelKey()
    {
        await InitTestTargets();

        AddDeviceToPanel();

        await WaitForDeviceOnlineStatus();

        var newKey = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf };
        var result = await TargetPanel.EncryptionKeySet(ConnectionId, 0,
            new EncryptionKeyConfiguration(KeyType.SecureChannelBaseKey, newKey));
        Assert.That(result, Is.True);

        await AssertPanelToDeviceCommsAreHealthy();

        RemoveDeviceFromPanel();
        AddDeviceToPanel(newKey);

        await WaitForDeviceOnlineStatus();
    }

    [Test]
    public async Task PanelIsAbleToChangeDeviceAddressWithComSetCommand()
    {
        await InitTestTargets();

        AddDeviceToPanel();

        await WaitForDeviceOnlineStatus();

        byte newAddress = 20;
        var commSettings = new Net.Model.CommandData.CommunicationConfiguration(newAddress, 9600);
        var results = await TargetPanel.CommunicationConfiguration(ConnectionId, 0, commSettings);

        Assert.That(results.Address, Is.EqualTo(newAddress));

        Assert.ThrowsAsync<TimeoutException>(() => AssertPanelToDeviceCommsAreHealthy());

        RemoveDeviceFromPanel();
        AddDeviceToPanel(address:  newAddress);

        await WaitForDeviceOnlineStatus();

        await AssertPanelToDeviceCommsAreHealthy();
    }

    [Test]
    public async Task DeviceResetsItselfWhenPanelChangesBaudRateWithComSetCommand()
    {
        var mockComSetUpdate = new Mock<EventHandler<DeviceComSetUpdatedEventArgs>>();

        await InitTestTargets();

        TargetDevice.DeviceComSetUpdated += async (o, e) =>
        {
            TestContext.WriteLine("----- Received Device ComSet Updated EVENT -----");

            // Record call results so that we can verify them as part of the test
            mockComSetUpdate.Object.Invoke(o, e);

            // Simulate what a "real" client would do when it got the request to change comm settings
            await TargetDevice.StopListening();
            TargetDevice.Dispose();

            // Re-init the device with new baud rate
            await InitTestTargetDevice(baudRate: e.NewBaudRate);
        };

        AddDeviceToPanel();

        await WaitForDeviceOnlineStatus();

        var connLostCheckpoint = SetupCheckpointForExpectedTestEvent(TestEventType.ConnectionLost);

        int newBaudRate = 19200;
        var commSettings = new Net.Model.CommandData.CommunicationConfiguration(DeviceAddress, newBaudRate);
        var results = await TargetPanel.CommunicationConfiguration(ConnectionId, 0, commSettings);

        Assert.That(results.Address, Is.EqualTo(DeviceAddress));
        Assert.That(results.BaudRate, Is.EqualTo(newBaudRate));

        await connLostCheckpoint;

        mockComSetUpdate.Verify(e => e(
            It.IsAny<object>(),
            It.IsAny<DeviceComSetUpdatedEventArgs>()), Times.Once);

        var eventArgs = (DeviceComSetUpdatedEventArgs)mockComSetUpdate.Invocations.First().Arguments[1];
        Assert.Multiple(() =>
        {
            Assert.That(eventArgs.OldAddress, Is.EqualTo(0));
            Assert.That(eventArgs.NewAddress, Is.EqualTo(0));
            Assert.That(eventArgs.OldBaudRate, Is.EqualTo(9600));
            Assert.That(eventArgs.NewBaudRate, Is.EqualTo(19200));
        });

        RemoveDeviceFromPanel();
        await RestartTargetPanelConnection(baudRate: newBaudRate);
        
        AddDeviceToPanel();

        await WaitForDeviceOnlineStatus();
    }
}


public class TestDevice : Device
{
    public TestDevice(DeviceConfiguration config, ILoggerFactory loggerFactory)
        : base(config, loggerFactory) { }

    protected override PayloadData HandleIdReport()
    {
        return new DeviceIdentification([0x01, 0x02, 0x03], 4, 5, 6, 7, 8, 9);
    }

    protected override PayloadData HandleDeviceCapabilities()
    {
        var deviceCapabilities = new DeviceCapabilities([
            new DeviceCapability(CapabilityFunction.CardDataFormat, 1, 0),
            new DeviceCapability(CapabilityFunction.ReaderLEDControl, 1, 0),
            new DeviceCapability(CapabilityFunction.ReaderTextOutput, 0, 0),
            new DeviceCapability(CapabilityFunction.CheckCharacterSupport, 1, 0),
            new DeviceCapability(CapabilityFunction.CommunicationSecurity, 1, 1),
            new DeviceCapability(CapabilityFunction.ReceiveBufferSize, 0, 1),
            new DeviceCapability(CapabilityFunction.OSDPVersion, 2, 0)
        ]);

        return deviceCapabilities;
    }

    protected override PayloadData HandleKeySettings(EncryptionKeyConfiguration commandPayload)
    {
        return new Ack();
    }

    protected override PayloadData HandleCommunicationSet(Net.Model.CommandData.CommunicationConfiguration commandPayload)
    {
        int[] validBaudRates = [9600, 19200, 115200];
        var newBaudRate = validBaudRates.Contains(commandPayload.BaudRate) ? commandPayload.BaudRate : validBaudRates[0];

        return new Net.Model.ReplyData.CommunicationConfiguration(commandPayload.Address, newBaudRate);
    }

    protected override PayloadData HandleLocalStatusReport()
    {
        return new LocalStatus(false, false);
    }

    protected override PayloadData HandleInputStatusReport()
    {
        return new InputStatus([InputStatusValue.Inactive]);
    }

    protected override PayloadData HandleOutputStatusReport()
    {
        return new OutputStatus([false]);
    }

    protected override PayloadData HandleReaderStatusReport()
    {
        return new ReaderStatus([ReaderTamperStatus.Normal]);
    }

    protected override PayloadData HandleOutputControl(OutputControls commandPayload)
    {
        return new OutputStatus([false]);
    }
}
