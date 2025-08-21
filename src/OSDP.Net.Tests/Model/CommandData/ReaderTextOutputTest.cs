using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData
{
    [TestFixture]
    [Category("Unit")]
    internal class ReaderTextOutputTest
    {
        private byte[] TestData =>
        [
            0x00, 0x02, 0x00, 0x01, 0x01, 0x18, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A,
            0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A, 0x2A
        ];

        private ReaderTextOutput TestReaderTextOutput => new ReaderTextOutput(0, TextCommand.PermanentTextWithWrap, 0,
            1, 1, "************************");

        [Test]
        public void CheckConstantValues()
        {
            // Arrange Act Assert
            Assert.That(TestReaderTextOutput.CommandType, Is.EqualTo(CommandType.TextOutput));
            Assert.That(TestReaderTextOutput.SecurityControlBlock().ToArray(),
                Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
        }
        
        [Test]
        public void BuildData()
        {
            // Arrange
            // Act
            var actual = TestReaderTextOutput.BuildData();

            // Assert
            Assert.That(actual, Is.EqualTo(TestData));
        }

        [Test]
        public void ParseData()
        {
            // Arrange Act
            var actual = ReaderTextOutput.ParseData(TestData);

            // Assert
            Assert.That(actual.ReaderNumber, Is.EqualTo(TestReaderTextOutput.ReaderNumber));
            Assert.That(actual.TextCommand, Is.EqualTo(TestReaderTextOutput.TextCommand));
            Assert.That(actual.TemporaryTextTime, Is.EqualTo(TestReaderTextOutput.TemporaryTextTime));
            Assert.That(actual.Row, Is.EqualTo(TestReaderTextOutput.Row));
            Assert.That(actual.Column, Is.EqualTo(TestReaderTextOutput.Column));
            Assert.That(actual.Text, Is.EqualTo(TestReaderTextOutput.Text));
        }
    }
}
