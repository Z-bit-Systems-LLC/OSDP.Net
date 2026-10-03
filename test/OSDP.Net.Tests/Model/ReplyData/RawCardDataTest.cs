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
            Assert.That(RawCardData.FormatHexData(rawCardData.Data), Is.EqualTo("12AB"));
        }

        [Test]
        public void FormatHexData_PartialByte_PadsTrailingBitsWithZeros()
        {
            // 26-bit Wiegand: 4 bytes on the wire with the last 6 bits unused
            var data = new byte[] { 0x00, 0x01, 0x1A, 0x00, 0x8F, 0x12, 0x34, 0xC0 };

            var rawCardData = RawCardData.ParseData(data);

            Assert.That(rawCardData.BitCount, Is.EqualTo(26));
            Assert.That(RawCardData.FormatHexData(rawCardData.Data), Is.EqualTo("8F1234C0"));
        }

        [Test]
        public void ToString_IncludesHexData()
        {
            var rawCardData = RawCardData.ParseData(new byte[] { 0x05, 0x00, 0x10, 0x00, 0x12, 0xab });

            Assert.That(rawCardData.ToString(), Does.Contain("Hex Data: 12AB"));
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
