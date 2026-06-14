using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData;

[TestFixture]
[Category("Unit")]
public class OutputStatusTest
{
    [Test]
    public void BuildData_EncodesActiveAsOne_AndInactiveAsZero()
    {
        var status = new OutputStatus([true, false, true]);

        Assert.That(status.BuildData(), Is.EqualTo(new byte[] { 0x01, 0x00, 0x01 }));
    }

    [Test]
    public void BuildData_ThenParseData_RoundTrips()
    {
        var original = new OutputStatus([true, false, true, false]);

        var parsed = OutputStatus.ParseData(original.BuildData());

        Assert.That(parsed.OutputStatuses, Is.EqualTo(original.OutputStatuses));
    }

    [Test]
    public void Code_IsOutputStatusReport()
    {
        var status = new OutputStatus([false]);

        Assert.That(status.Code, Is.EqualTo((byte)ReplyType.OutputStatusReport));
    }
}
