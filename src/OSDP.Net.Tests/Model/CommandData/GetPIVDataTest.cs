using NUnit.Framework;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Utilities;
using System.Linq;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Tests.Model.CommandData
{
    [Category("Unit")]
    internal class GetPIVDataTest
    {
        private byte[] TestData => [0x5F, 0xC1, 0x02, 0x01, 0x19, 0x00];
        
        private GetPIVData TestGetPIVData => new([0x5F, 0xC1, 0x02], 1, 25);
        
        [Test]
        public void CheckConstantValues()
        {
            // Arrange Act Assert
            Assert.That(TestGetPIVData.CommandType, Is.EqualTo(CommandType.PivData));
            Assert.That(TestGetPIVData.SecurityControlBlock().ToArray(),
                Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
        }
        
        [Test]
        public void BuildData()
        {
            var actual = TestGetPIVData.BuildData().ToArray();
            Assert.That(actual, Is.EqualTo(TestData));
        }

        [TestCase("5F-C1-02-01-19-00")]
        [TestCase("5F-C1-02-01-19")]
        public void ParseData(string inputData)
        {
            var actual = GetPIVData.ParseData(BinaryUtils.HexToBytes(inputData).ToArray());

            Assert.That(actual.ObjectId, Is.EquivalentTo(new byte[] { 0x5F, 0xC1, 0x02 }));
            Assert.That(actual.ElementId, Is.EqualTo(1));
            Assert.That(actual.DataOffset, Is.EqualTo(25));
        }
    }
}
