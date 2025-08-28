using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData
{
    [Category("Unit")]
    internal class ReaderBuzzerControlTest
    {
        private byte[] TestData => [0x00, 0x02, 0x05, 0x02, 0x01];

        private ReaderBuzzerControl TestReaderBuzzerControl => new (0, ToneCode.Default, 5, 2, 1);
            
        [Test]
        public void CheckConstantValues()
        {
            // Arrange Act Assert
            Assert.That(TestReaderBuzzerControl.CommandType, Is.EqualTo(CommandType.BuzzerControl));
            Assert.That(TestReaderBuzzerControl.SecurityControlBlock().ToArray(),
                Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
        }
        
        [Test]
        public void BuildData()
        {
            // Arrange
            // Act
            var actual = TestReaderBuzzerControl.BuildData();

            // Assert
            Assert.That(actual, Is.EqualTo(TestData));
        }
        
        [Test]
        public void ParseData()
        {
            var actual = ReaderBuzzerControl.ParseData(TestData);

            Assert.That(actual.ReaderNumber, Is.EqualTo(TestReaderBuzzerControl.ReaderNumber));
            Assert.That(actual.ToneCode, Is.EqualTo(TestReaderBuzzerControl.ToneCode));
            Assert.That(actual.OnTime, Is.EqualTo(TestReaderBuzzerControl.OnTime));
            Assert.That(actual.OffTime, Is.EqualTo(TestReaderBuzzerControl.OffTime));
            Assert.That(actual.Count, Is.EqualTo(TestReaderBuzzerControl.Count));
        }
    }
}
 