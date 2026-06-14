using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.IntegrationTests;

/// <summary>
/// Integration tests for OSDP transparent mode (osdp_XWR / osdp_XRD) on the PD side.
/// </summary>
/// <remarks>
/// Validates that the Device base class dispatches incoming ExtendedWrite commands
/// to <c>HandleExtendedWrite</c>, that synchronous XRD replies round-trip back through
/// <see cref="ControlPanel.ExtendedWriteData"/>, and that unsolicited XRD replies enqueued
/// via <c>EnqueuePollReply</c> surface through the <c>ExtendedReadReplyReceived</c> event.
/// </remarks>
[TestFixture]
[Category("Integration")]
public class TransparentModeTests : IntegrationTestFixtureBase
{
    [Test]
    public async Task XwrReadModeSetting_ReturnsExtendedReadReply()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        TargetDevice.ExtendedWriteHandler = _ => ExtendedRead.ModeZeroSettingReport(0, false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var result = await TargetPanel.ExtendedWriteData(
            ConnectionId, DeviceAddress, ExtendedWrite.ReadModeSetting());

        Assert.That(result.ReplyData, Is.Not.Null, "Expected PD to reply with osdp_XRD, not Ack/Nak");
        Assert.That(result.ReplyData.Mode, Is.EqualTo(0));
        Assert.That(result.ReplyData.PReply, Is.EqualTo(1));
        Assert.That(result.ReplyData.PData, Is.EqualTo(new byte[] { 0, 0 }));
    }

    [Test]
    public async Task XwrPassApdu_ReturnsApduResponse()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        // Echo PD: reads reader number + APDU, returns the reader number + a canned response
        TargetDevice.ExtendedWriteHandler = cmd =>
        {
            Assert.That(cmd.Mode, Is.EqualTo(1));
            Assert.That(cmd.PCommand, Is.EqualTo(1));
            var readerNumber = cmd.PData[0];
            return ExtendedRead.ApduResponse(readerNumber, [0x90, 0x00]);
        };

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var selectApdu = new byte[] { 0x00, 0xA4, 0x04, 0x00 };
        var result = await TargetPanel.ExtendedWriteData(
            ConnectionId, DeviceAddress, ExtendedWrite.ModeOnePassAPDUCommand(0x03, selectApdu));

        Assert.That(result.ReplyData, Is.Not.Null);
        Assert.That(result.ReplyData.Mode, Is.EqualTo(1));
        Assert.That(result.ReplyData.PReply, Is.EqualTo(1));
        Assert.That(result.ReplyData.PData, Is.EqualTo(new byte[] { 0x03, 0x90, 0x00 }));
    }

    [Test]
    public async Task UnsolicitedXrd_DeliveredOnPoll()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var xrdReceived = new TaskCompletionSource<ExtendedRead>();
        TargetPanel.ExtendedReadReplyReceived += (_, e) =>
        {
            xrdReceived.TrySetResult(e.ExtendedRead);
        };

        TargetDevice.EnqueuePollReply(ExtendedRead.CardPresent(0x00));

        var result = await Task.WhenAny(xrdReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(xrdReceived.Task), "Timed out waiting for unsolicited XRD");

        var received = await xrdReceived.Task;
        Assert.That(received.Mode, Is.EqualTo(1));
        Assert.That(received.PReply, Is.EqualTo(1));
        Assert.That(received.PData, Is.EqualTo(new byte[] { 0x00 }));
    }
}
