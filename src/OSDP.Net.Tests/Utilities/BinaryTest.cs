using NUnit.Framework;
using OSDP.Net.Utilities;
using System.Linq;

namespace OSDP.Net.Tests.Utilities
{
    [TestFixture]
    [Category("Unit")]
    internal class BinaryUtilsTest
    {
        [Test]
        public void HexToBytes()
        {
            var actual = BinaryUtils.HexToBytes("0123456789ab").ToArray();
            Assert.That(actual, Is.EquivalentTo(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab }));
        }

        [Test]
        public void HexWithDashSeparatorsToBytes()
        {
            var actual = BinaryUtils.HexToBytes("01-23-45-67-89-ab").ToArray();
            Assert.That(actual, Is.EquivalentTo(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab }));
        }

        [Test]
        public void HexStringToBitArray()
        {
            var actual = BinaryUtils.HexStringToBitArray("0123");
            Assert.That(BinaryUtils.BitArrayToString(actual), Is.EqualTo("0000000100100011"));
        }
    }
}
