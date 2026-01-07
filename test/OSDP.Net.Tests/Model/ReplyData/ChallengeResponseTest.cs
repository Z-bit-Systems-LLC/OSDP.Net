using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tests.Model.ReplyData
{
    [Category("Unit")]
    internal class ChallengeResponseTest
    {
        [Test]
        public void ParseData()
        {
            var data = BinaryUtils.HexToBytes(
                "01-02-03-04-05-06-07-08-11-22-33-44-55-66-77-88-00-10-20-30-40-50-60-70-80-90-A0-B0-C0-D0-E0-F0").ToArray();
            var actual = ChallengeResponse.ParseData(data, true);

            Assert.That(BitConverter.ToString(actual.ClientUID), Is.EqualTo("01-02-03-04-05-06-07-08"));
            Assert.That(BitConverter.ToString(actual.RndB), Is.EqualTo("11-22-33-44-55-66-77-88"));
            Assert.That(BitConverter.ToString(actual.Cryptogram), Is.EqualTo("00-10-20-30-40-50-60-70-80-90-A0-B0-C0-D0-E0-F0"));
        }

        [Test]
        public void BuildData()
        {
            var cUID = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var rndB = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
            var cryptogram = new byte[] { 
                0x00, 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0, 0xF0 };

            var actual = new ChallengeResponse(cUID, rndB, cryptogram, true).BuildData();

            Assert.That(BitConverter.ToString(actual), Is.EqualTo(
                "01-02-03-04-05-06-07-08-11-22-33-44-55-66-77-88-00-10-20-30-40-50-60-70-80-90-A0-B0-C0-D0-E0-F0"
            ));
        }
    }
}
