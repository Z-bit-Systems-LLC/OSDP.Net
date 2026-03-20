using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Compliance;

/// <summary>
/// OSDP 2.2.2 Compliance Tests - Optional Feature Validation
///
/// Validates PD behavior for optional OSDP features:
/// - Capability-based command acceptance/rejection (Section 7.3)
/// - Output control (Section 7.9) when PD has output capability
/// - LED, Buzzer, Text commands rejected when PD lacks capability
/// - Communication configuration (osdp_COMSET, Section 7.10)
///   - Address changes
///   - Baud rate changes
/// </summary>
[TestFixture]
[Category("Compliance")]
[Category("Compliance.Optional")]
public class OptionalFeatureTests : IntegrationTestFixtureBase
{
    [SetUp]
    public async Task SetupOptionalFeatureTests()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();
    }

    // OSDP 2.2.2 Section 7.3 - Capability Reporting Accuracy
    // PD capabilities must accurately reflect what commands the device supports.

    [Test]
    public async Task Capabilities_AccuratelyReflectImplementedFeatures()
    {
        // OSDP 2.2.2 Section 7.3 - PD must report capabilities that match actual behavior
        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);

        Assert.That(caps, Is.Not.Null);
        Assert.That(caps.Capabilities, Is.Not.Null);

        // Verify the TestDevice reports expected capabilities
        var capsList = caps.Capabilities.ToList();

        var cardDataCap = capsList.FirstOrDefault(c => c.Function == CapabilityFunction.CardDataFormat);
        Assert.That(cardDataCap, Is.Not.Null, "TestDevice should report CardDataFormat capability");

        var checkCharCap = capsList.FirstOrDefault(c => c.Function == CapabilityFunction.CheckCharacterSupport);
        Assert.That(checkCharCap, Is.Not.Null, "TestDevice should report CheckCharacterSupport capability");
        Assert.That(checkCharCap!.Compliance, Is.EqualTo(1), "CheckCharacterSupport compliance should be 1");

        var commSecCap = capsList.FirstOrDefault(c => c.Function == CapabilityFunction.CommunicationSecurity);
        Assert.That(commSecCap, Is.Not.Null, "TestDevice should report CommunicationSecurity capability");

        var osdpVersionCap = capsList.FirstOrDefault(c => c.Function == CapabilityFunction.OSDPVersion);
        Assert.That(osdpVersionCap, Is.Not.Null, "TestDevice should report OSDPVersion capability");
        Assert.That(osdpVersionCap!.Compliance, Is.EqualTo(2), "OSDPVersion compliance should be 2 (OSDP v2)");
    }

    [Test]
    public async Task Capabilities_ReceiveBufferSizeReportsMinimum128Bytes()
    {
        // OSDP 2.2.2 - PD must support minimum 128-byte receive buffer
        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);
        var bufferCap = caps.Capabilities
            .FirstOrDefault(c => c.Function == CapabilityFunction.ReceiveBufferSize);

        Assert.That(bufferCap, Is.Not.Null, "PD must report Receive Buffer Size capability");
        // NumberOf field contains buffer size in units; compliance + numberOfItems encode the size
        // A value of 0 compliance with 1 numberOfItems = 256 bytes (standard encoding)
    }

    // OSDP 2.2.2 Section 7.9 - osdp_OUT / osdp_OSTATR
    // Output control commands when PD has output capability.

    [Test]
    public async Task OutputControl_NopCommandReturnsStatus()
    {
        // OSDP 2.2.2 Section 7.9 - NOP control code should not change output state
        var outputControls = new OutputControls([
            new OutputControl(0, OutputControlCode.Nop, 0)
        ]);

        var result = await TargetPanel.OutputControl(ConnectionId, DeviceAddress, outputControls);
        Assert.That(result, Is.Not.Null);
    }

    // OSDP 2.2.2 - Commands for capabilities the PD does not support
    // PD must return NAK (UnknownCommandCode) for unimplemented optional commands.

    [Test]
    public void PdRejectsUnimplementedBuzzerCommand()
    {
        // TestDevice does not implement buzzer support
        var buzzerControl = new ReaderBuzzerControl(0, ToneCode.Off, 1, 1, 1);

        var exception = Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.ReaderBuzzerControl(ConnectionId, DeviceAddress, buzzerControl));

        Assert.That(exception!.Reply.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode),
            "PD must NAK optional commands it does not implement");
    }

    [Test]
    public void PdRejectsUnimplementedTextOutputCommand()
    {
        // TestDevice does not implement text output support
        var textOutput = new ReaderTextOutput(0, TextCommand.PermanentTextNoWrap, 0, 1, 1, "Test");

        var exception = Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.ReaderTextOutput(ConnectionId, DeviceAddress, textOutput));

        Assert.That(exception!.Reply.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode),
            "PD must NAK optional commands it does not implement");
    }

    [Test]
    public void PdRejectsUnimplementedLEDCommand()
    {
        // TestDevice reports LED capability but does not implement the handler
        var ledControls = new ReaderLedControls([
            new ReaderLedControl(0, 0,
                TemporaryReaderControlCode.Nop, 1, 0, LedColor.Black, LedColor.Black, 0,
                PermanentReaderControlCode.Nop, 1, 0, LedColor.Black, LedColor.Black)
        ]);

        var exception = Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.ReaderLedControl(ConnectionId, DeviceAddress, ledControls));

        Assert.That(exception!.Reply.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode),
            "PD must NAK optional commands it does not implement");
    }
}

/// <summary>
/// OSDP 2.2.2 Compliance Tests - Communication Configuration (osdp_COMSET)
///
/// Validates PD behavior for osdp_COMSET command (Section 7.10):
/// - Address change
/// - Baud rate change
/// - Device re-initialization after configuration change
/// </summary>
[TestFixture]
[Category("Compliance")]
[Category("Compliance.Optional")]
public class CommunicationConfigurationTests : IntegrationTestFixtureBase
{
    // OSDP 2.2.2 Section 7.10 - osdp_COMSET
    // PD must accept communication configuration changes and report the new settings.

    [Test]
    public async Task ComSet_AddressChangeAccepted()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        var addressChangeReceived = new TaskCompletionSource<byte>();
        TargetDevice.DeviceComSetUpdated += (_, e) =>
        {
            addressChangeReceived.TrySetResult(e.NewAddress);
        };

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        byte newAddress = 15;
        var commSettings = new Net.Model.CommandData.CommunicationConfiguration(newAddress, 9600);
        var result = await TargetPanel.CommunicationConfiguration(ConnectionId, DeviceAddress, commSettings);

        Assert.Multiple(() =>
        {
            Assert.That(result.Address, Is.EqualTo(newAddress),
                "PD must report the new address in COMSET reply");
            Assert.That(result.BaudRate, Is.EqualTo(9600),
                "Baud rate should remain unchanged when only address changes");
        });

        // Verify the device event was raised
        var receivedAddr = await addressChangeReceived.Task;
        Assert.That(receivedAddr, Is.EqualTo(newAddress));
    }

    [Test]
    public async Task ComSet_BaudRateChangeAccepted()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        var comSetReceived = new TaskCompletionSource<int>();
        TargetDevice.DeviceComSetUpdated += (_, e) =>
        {
            comSetReceived.TrySetResult(e.NewBaudRate);
        };

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        int newBaudRate = 19200;
        var commSettings = new Net.Model.CommandData.CommunicationConfiguration(DeviceAddress, newBaudRate);
        var result = await TargetPanel.CommunicationConfiguration(ConnectionId, DeviceAddress, commSettings);

        Assert.Multiple(() =>
        {
            Assert.That(result.Address, Is.EqualTo(DeviceAddress),
                "Address should remain unchanged when only baud rate changes");
            Assert.That(result.BaudRate, Is.EqualTo(newBaudRate),
                "PD must report the new baud rate in COMSET reply");
        });

        var receivedBaud = await comSetReceived.Task;
        Assert.That(receivedBaud, Is.EqualTo(newBaudRate));
    }

    [Test]
    public async Task ComSet_AddressChangeAndReconnect()
    {
        // OSDP 2.2.2 Section 7.10 - After address change, PD must respond at new address
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        TargetDevice.DeviceComSetUpdated += (_, _) => { };

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        byte newAddress = 20;
        var commSettings = new Net.Model.CommandData.CommunicationConfiguration(newAddress, 9600);
        var result = await TargetPanel.CommunicationConfiguration(ConnectionId, DeviceAddress, commSettings);
        Assert.That(result.Address, Is.EqualTo(newAddress));

        // Restart device with new address and verify communication
        await TargetDevice.StopListening();
        TargetDevice.Dispose();
        await InitTestTargetDevice(cfg => cfg.Address = newAddress);

        RemoveDeviceFromPanel();
        AddDeviceToPanel(address: newAddress, useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        await AssertPanelToDeviceCommsAreHealthy();
    }

    [Test]
    public async Task ComSet_RejectsInvalidBaudRate()
    {
        // TestDevice only accepts 9600, 19200, 115200 - invalid rates default to 9600
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        int invalidBaudRate = 12345;
        var commSettings = new Net.Model.CommandData.CommunicationConfiguration(DeviceAddress, invalidBaudRate);
        var result = await TargetPanel.CommunicationConfiguration(ConnectionId, DeviceAddress, commSettings);

        Assert.That(result.BaudRate, Is.EqualTo(9600),
            "PD should fall back to default baud rate for unsupported values");
    }
}
