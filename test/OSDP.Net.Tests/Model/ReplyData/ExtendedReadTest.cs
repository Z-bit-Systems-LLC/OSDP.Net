using NUnit.Framework;
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
}
