using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData;

[Category("Unit")]
internal class ExtendedReadTest
{
    [Test]
    public void ParseData_FullPayload()
    {
        var payload = new byte[] { 0x01, 0x02, 0xAA, 0xBB };

        var actual = ExtendedRead.ParseData(payload);

        Assert.That(actual.Mode, Is.EqualTo(1));
        Assert.That(actual.PReply, Is.EqualTo(2));
        Assert.That(actual.PData, Is.EqualTo(new byte[] { 0xAA, 0xBB }));
    }

    [Test]
    public void ParseData_NoPData()
    {
        var payload = new byte[] { 0x01, 0x01 };

        var actual = ExtendedRead.ParseData(payload);

        Assert.That(actual.Mode, Is.EqualTo(1));
        Assert.That(actual.PReply, Is.EqualTo(1));
        Assert.That(actual.PData, Is.Empty);
    }

    [Test]
    public void ParseData_ShortPayload_Lenient()
    {
        // PD state transitions during smart-card scan can produce short replies —
        // ParseData must not throw when only Mode is present.
        var payload = new byte[] { 0x01 };

        var actual = ExtendedRead.ParseData(payload);

        Assert.That(actual.Mode, Is.EqualTo(1));
        Assert.That(actual.PReply, Is.EqualTo(0));
        Assert.That(actual.PData, Is.Empty);
    }

    [Test]
    public void ParseData_EmptyPayload_Lenient()
    {
        var actual = ExtendedRead.ParseData([]);

        Assert.That(actual.Mode, Is.EqualTo(0));
        Assert.That(actual.PReply, Is.EqualTo(0));
        Assert.That(actual.PData, Is.Empty);
    }

    [Test]
    public void CheckConstantValues()
    {
        var reply = new ExtendedRead(1, 2, [0xAA, 0xBB]);

        Assert.That(reply.Code, Is.EqualTo((byte)ReplyType.ExtendedRead));
        Assert.That(reply.Code, Is.EqualTo(0xB1));
        Assert.That(reply.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.ReplyMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData_ProducesExpectedBytes()
    {
        var reply = new ExtendedRead(1, 2, [0xAA, 0xBB, 0xCC]);

        var actual = reply.BuildData();

        Assert.That(actual, Is.EqualTo(new byte[] { 0x01, 0x02, 0xAA, 0xBB, 0xCC }));
    }

    [Test]
    public void BuildData_NoPData_ProducesModeAndPReplyOnly()
    {
        var reply = new ExtendedRead(1, 1, []);

        Assert.That(reply.BuildData(), Is.EqualTo(new byte[] { 0x01, 0x01 }));
    }

    [Test]
    public void BuildData_RoundTripsThroughParseData()
    {
        var original = new ExtendedRead(1, 1, [0x03, 0x90, 0x00]);

        var parsed = ExtendedRead.ParseData(original.BuildData());

        Assert.That(parsed.Mode, Is.EqualTo(original.Mode));
        Assert.That(parsed.PReply, Is.EqualTo(original.PReply));
        Assert.That(parsed.PData, Is.EqualTo(original.PData));
    }

    [Test]
    public void ModeZeroSettingReport_Disabled_ProducesExpectedBytes()
    {
        var reply = ExtendedRead.ModeZeroSettingReport(0, false);

        Assert.That(reply.Mode, Is.EqualTo(0));
        Assert.That(reply.PReply, Is.EqualTo(1));
        Assert.That(reply.PData, Is.EqualTo(new byte[] { 0, 0 }));
    }

    [Test]
    public void ModeZeroSettingReport_Enabled_ProducesExpectedBytes()
    {
        var reply = ExtendedRead.ModeZeroSettingReport(1, true);

        Assert.That(reply.Mode, Is.EqualTo(0));
        Assert.That(reply.PReply, Is.EqualTo(1));
        Assert.That(reply.PData, Is.EqualTo(new byte[] { 1, 1 }));
    }

    [Test]
    public void CardPresent_ProducesExpectedBytes()
    {
        var reply = ExtendedRead.CardPresent(0x02);

        Assert.That(reply.Mode, Is.EqualTo(1));
        Assert.That(reply.PReply, Is.EqualTo(1));
        Assert.That(reply.PData, Is.EqualTo(new byte[] { 0x02 }));
    }

    [Test]
    public void ApduResponse_ProducesExpectedBytes()
    {
        var apduResponse = new byte[] { 0x6A, 0x82 };

        var reply = ExtendedRead.ApduResponse(0x01, apduResponse);

        Assert.That(reply.Mode, Is.EqualTo(1));
        Assert.That(reply.PReply, Is.EqualTo(1));
        Assert.That(reply.PData, Is.EqualTo(new byte[] { 0x01, 0x6A, 0x82 }));
    }

    [Test]
    public void SessionTerminated_ProducesExpectedBytes()
    {
        var reply = ExtendedRead.SessionTerminated(0x00);

        Assert.That(reply.Mode, Is.EqualTo(1));
        Assert.That(reply.PReply, Is.EqualTo(2));
        Assert.That(reply.PData, Is.EqualTo(new byte[] { 0x00 }));
    }
}
