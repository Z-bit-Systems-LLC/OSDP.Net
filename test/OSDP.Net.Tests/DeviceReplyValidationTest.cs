using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Model;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests;

[TestFixture]
[Category("Unit")]
public class DeviceReplyValidationTest
{
    private static Device CreateDevice() =>
        new(new DeviceConfiguration(new ClientIdentification([0x01, 0x02, 0x03], 12345)));

    [Test]
    public void EnsureValidReply_StatusCommandWithWrongReply_SubstitutesNak()
    {
        var device = CreateDevice();

        var result = device.EnsureValidReply(CommandType.LocalStatus, new Ack());

        Assert.That((ReplyType)result.Code, Is.EqualTo(ReplyType.Nak));
    }

    [Test]
    public void EnsureValidReply_StatusCommandWithCorrectReport_PassesThrough()
    {
        var device = CreateDevice();

        var result = device.EnsureValidReply(CommandType.LocalStatus, new LocalStatus(false, false));

        Assert.That((ReplyType)result.Code, Is.EqualTo(ReplyType.LocalStatusReport));
    }

    [Test]
    public void EnsureValidReply_StatusCommandWithNak_PassesThrough()
    {
        var device = CreateDevice();

        var result = device.EnsureValidReply(CommandType.InputStatus, new Nak(ErrorCode.UnknownCommandCode));

        Assert.That((ReplyType)result.Code, Is.EqualTo(ReplyType.Nak));
    }

    [Test]
    public void EnsureValidReply_IdReportWithEitherIdVariant_PassesThrough()
    {
        var device = CreateDevice();

        var result = device.EnsureValidReply(
            CommandType.IdReport,
            new DeviceIdentification([0x01, 0x02, 0x03], 4, 5, 6, 7, 8, 9));

        Assert.That((ReplyType)result.Code, Is.EqualTo(ReplyType.PdIdReport));
    }

    [Test]
    public void EnsureValidReply_NonMandatedCommand_PassesAckThrough()
    {
        var device = CreateDevice();

        var result = device.EnsureValidReply(CommandType.LEDControl, new Ack());

        Assert.That((ReplyType)result.Code, Is.EqualTo(ReplyType.Ack));
    }
}
