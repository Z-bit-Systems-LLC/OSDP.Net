using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Compliance;

/// <summary>
/// OSDP 2.2.2 Compliance Tests - Mandatory Command/Reply Validation
///
/// Validates that the PD correctly handles all mandatory OSDP commands and
/// returns properly formatted replies:
/// - osdp_ID (0x61) - ID Report Request (Section 7.2)
/// - osdp_CAP (0x62) - PD Capabilities Request (Section 7.3)
/// - osdp_LSTAT (0x64) - Local Status Report (Section 7.5)
/// - osdp_ISTAT (0x65) - Input Status Report (Section 7.6)
/// - osdp_OSTAT (0x66) - Output Status Report (Section 7.7)
/// - osdp_RSTAT (0x67) - Reader Status Report (Section 7.8)
/// - osdp_OUT (0x68) - Output Control (Section 7.9)
/// </summary>
[TestFixture]
[Category("Compliance")]
[Category("Compliance.Mandatory")]
public class MandatoryCommandTests : IntegrationTestFixtureBase
{
    [SetUp]
    public async Task SetupMandatoryCommandTests()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();
    }

    // OSDP 2.2.2 Section 7.2 - osdp_ID / osdp_PDID
    // PD must return device identification with vendor code, model, version,
    // serial number, and firmware version.

    [Test]
    public async Task IdReport_ReturnsValidDeviceIdentification()
    {
        var id = await TargetPanel.IdReport(ConnectionId, DeviceAddress);

        Assert.That(id, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(id.VendorCode, Is.Not.Null);
            Assert.That(id.VendorCode, Has.Length.EqualTo(3), "Vendor code must be 3 bytes");
            Assert.That(id.SerialNumber, Is.Not.Zero, "Serial number should be non-zero");
            Assert.That(id.FirmwareMajor, Is.GreaterThanOrEqualTo(0));
        });
    }

    [Test]
    public async Task IdReport_ReturnsConsistentResultsOnMultipleCalls()
    {
        var id1 = await TargetPanel.IdReport(ConnectionId, DeviceAddress);
        var id2 = await TargetPanel.IdReport(ConnectionId, DeviceAddress);

        Assert.Multiple(() =>
        {
            Assert.That(id2.VendorCode, Is.EqualTo(id1.VendorCode));
            Assert.That(id2.ModelNumber, Is.EqualTo(id1.ModelNumber));
            Assert.That(id2.SerialNumber, Is.EqualTo(id1.SerialNumber));
        });
    }

    // OSDP 2.2.2 Section 7.3 - osdp_CAP / osdp_PDCAP
    // PD must return capabilities report with function codes that accurately
    // reflect the device's actual capabilities.

    [Test]
    public async Task DeviceCapabilities_ReturnsValidCapabilitiesReport()
    {
        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);

        Assert.That(caps, Is.Not.Null);
        Assert.That(caps.Capabilities, Is.Not.Null);
        Assert.That(caps.Capabilities.Count(), Is.GreaterThan(0), "PD must report at least one capability");
    }

    [Test]
    public async Task DeviceCapabilities_IncludesCheckCharacterSupport()
    {
        // OSDP 2.2.2: All PDs must support checksum mode (Function Code 8)
        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);

        var checkCharCap = caps.Capabilities
            .FirstOrDefault(c => c.Function == CapabilityFunction.CheckCharacterSupport);

        Assert.That(checkCharCap, Is.Not.Null, "PD must report Check Character Support capability");
    }

    [Test]
    public async Task DeviceCapabilities_IncludesCommunicationSecurity()
    {
        // OSDP 2.2.2: PDs that support secure channel must report it (Function Code 9)
        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);

        var secCap = caps.Capabilities
            .FirstOrDefault(c => c.Function == CapabilityFunction.CommunicationSecurity);

        Assert.That(secCap, Is.Not.Null, "PD must report Communication Security capability");
    }

    [Test]
    public async Task DeviceCapabilities_IncludesReceiveBufferSize()
    {
        // OSDP 2.2.2: PD must support minimum 128-byte receive buffer (Function Code 10)
        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);

        var bufferCap = caps.Capabilities
            .FirstOrDefault(c => c.Function == CapabilityFunction.ReceiveBufferSize);

        Assert.That(bufferCap, Is.Not.Null, "PD must report Receive Buffer Size capability");
    }

    // OSDP 2.2.2 Section 7.5 - osdp_LSTAT / osdp_LSTATR
    // PD must return local status (tamper, power status).

    [Test]
    public async Task LocalStatus_ReturnsValidStatusReport()
    {
        var status = await TargetPanel.LocalStatus(ConnectionId, DeviceAddress);

        Assert.That(status, Is.Not.Null);
        // LocalStatus has TamperStatus and PowerStatus boolean fields
        // Just verify we get a valid non-null response
        Assert.That(status.ToString(), Is.Not.Empty);
    }

    // OSDP 2.2.2 Section 7.6 - osdp_ISTAT / osdp_ISTATR
    // PD must return input status if it has inputs.

    [Test]
    public async Task InputStatus_ReturnsValidStatusReport()
    {
        var status = await TargetPanel.InputStatus(ConnectionId, DeviceAddress);

        Assert.That(status, Is.Not.Null);
        Assert.That(status.InputStatuses, Is.Not.Null);
    }

    // OSDP 2.2.2 Section 7.7 - osdp_OSTAT / osdp_OSTATR
    // PD must return output status if it has outputs.

    [Test]
    public async Task OutputStatus_ReturnsValidStatusReport()
    {
        var status = await TargetPanel.OutputStatus(ConnectionId, DeviceAddress);

        Assert.That(status, Is.Not.Null);
        Assert.That(status.OutputStatuses, Is.Not.Null);
    }

    // OSDP 2.2.2 Section 7.8 - osdp_RSTAT / osdp_RSTATR
    // PD must return reader tamper status.

    [Test]
    public async Task ReaderStatus_ReturnsValidStatusReport()
    {
        var status = await TargetPanel.ReaderStatus(ConnectionId, DeviceAddress);

        Assert.That(status, Is.Not.Null);
        Assert.That(status.ReaderTamperStatuses, Is.Not.Null);
        Assert.That(status.ReaderTamperStatuses.Count(), Is.GreaterThan(0),
            "PD must report status for at least one reader");
    }

    // OSDP 2.2.2 Section 7.9 - osdp_OUT / osdp_OSTATR
    // PD must process output control commands if it has outputs.

    [Test]
    public async Task OutputControl_ReturnsOutputStatus()
    {
        var outputControls = new OutputControls([
            new OutputControl(0, OutputControlCode.Nop, 0)
        ]);

        var result = await TargetPanel.OutputControl(ConnectionId, DeviceAddress, outputControls);

        Assert.That(result, Is.Not.Null);
    }

    // OSDP 2.2.2 - Reply format consistency
    // Verify PD returns consistent data across multiple requests

    [Test]
    public async Task DeviceCapabilities_ReturnsConsistentResults()
    {
        var caps1 = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);
        var caps2 = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);

        Assert.That(caps2.Capabilities.Count(), Is.EqualTo(caps1.Capabilities.Count()),
            "Capability count must be consistent between requests");
    }

}

/// <summary>
/// Validates mandatory commands also work over a secure channel.
/// </summary>
[TestFixture]
[Category("Compliance")]
[Category("Compliance.Mandatory")]
[Category("Compliance.Security")]
public class MandatoryCommandsOverSecureChannelTests : IntegrationTestFixtureBase
{
    [SetUp]
    public async Task SetupSecureChannel()
    {
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();
    }

    [Test]
    public async Task IdReport_WorksOverSecureChannel()
    {
        var id = await TargetPanel.IdReport(ConnectionId, DeviceAddress);
        Assert.That(id, Is.Not.Null);
        Assert.That(id.VendorCode, Has.Length.EqualTo(3));
    }

    [Test]
    public async Task DeviceCapabilities_WorksOverSecureChannel()
    {
        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);
        Assert.That(caps, Is.Not.Null);
        Assert.That(caps.Capabilities.Count(), Is.GreaterThan(0));
    }

    [Test]
    public async Task LocalStatus_WorksOverSecureChannel()
    {
        var status = await TargetPanel.LocalStatus(ConnectionId, DeviceAddress);
        Assert.That(status, Is.Not.Null);
    }

    [Test]
    public async Task InputStatus_WorksOverSecureChannel()
    {
        var status = await TargetPanel.InputStatus(ConnectionId, DeviceAddress);
        Assert.That(status, Is.Not.Null);
    }

    [Test]
    public async Task OutputStatus_WorksOverSecureChannel()
    {
        var status = await TargetPanel.OutputStatus(ConnectionId, DeviceAddress);
        Assert.That(status, Is.Not.Null);
    }

    [Test]
    public async Task ReaderStatus_WorksOverSecureChannel()
    {
        var status = await TargetPanel.ReaderStatus(ConnectionId, DeviceAddress);
        Assert.That(status, Is.Not.Null);
    }
}
