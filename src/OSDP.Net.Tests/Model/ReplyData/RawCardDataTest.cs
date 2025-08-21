using NUnit.Framework;
using System;
using System.Collections;
using System.Linq;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData
{
    [TestFixture]
    [Category("Unit")]
    public class RawCardDataTest
    {
        [Test]
        public void ParseData()
        {
            var data = new byte[] { 0x05, 0x00, 0x10, 0x00, 0x12, 0xab };

            var rawCardData = RawCardData.ParseData(data);

            Assert.That(rawCardData.ReaderNumber, Is.EqualTo(5));
            Assert.That(rawCardData.FormatCode, Is.EqualTo(FormatCode.NotSpecified));
            Assert.That(rawCardData.BitCount, Is.EqualTo(16));
            Assert.That(RawCardData.FormatData(rawCardData.Data), Is.EqualTo("0001001010101011"));
        }

        [Test]
        public void BuildData()
        {
            // Resharper disable once ConditionIsAlwaysTrueOrFalse
            var data = new BitArray("0001001010101011".Select(
                x => x != '0' && (x == '1' ? true : throw new ArgumentException())).ToArray());

            var rawCardData = new RawCardData(5, FormatCode.NotSpecified, data);
            var buffer = rawCardData.BuildData();
            Assert.That(BitConverter.ToString(buffer), Is.EqualTo("05-00-10-00-12-AB"));
        }
    }
}
