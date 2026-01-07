using System;
using NUnit.Framework;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData;

[Category("Unit")]
public class FormattedCardDataTest
{
    [Test]
    public void ParseData()
    {
        var data = new byte[] { 0x05, 0x00, 0x09, 0x74, 0x65, 0x73, 0x74, 0x69, 0x6E, 0x70, 0x75, 0x74 };

        var formattedCardData = FormattedCardData.ParseData(data);

        Assert.That(formattedCardData.ReaderNumber, Is.EqualTo(5));
        Assert.That(formattedCardData.ReadDirection, Is.EqualTo(ReadDirection.Forward));
        Assert.That(formattedCardData.Lenght, Is.EqualTo(9));
        Assert.That(formattedCardData.Data, Is.EqualTo("testinput"));
    }

    [Test]
    public void BuildData()
    {
        var formattedCardData = new FormattedCardData(5, ReadDirection.Forward, "testinput");
        var buffer = formattedCardData.BuildData();
        Assert.That(BitConverter.ToString(buffer), Is.EqualTo("05-00-09-74-65-73-74-69-6E-70-75-74"));
    }
}