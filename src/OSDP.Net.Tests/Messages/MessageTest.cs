using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tests.Messages
{
    [TestFixture]
    public class MessageTest
    {
        [Test]
        public void BuildMultiPartMessageData()
        {
            // Arrange
            var part1 = new byte[] {0x00, 0x01};
            var part2 = new byte[] {0x02, 0x03, 0x04};
            var part3 = new byte[] {0x05};
            var completeData = new Span<byte>(new byte[6]);

            // Act
            Message.BuildMultiPartMessageData(6, 0, 2, part1, completeData);
            Message.BuildMultiPartMessageData(6, 2, 3, part2, completeData);
            Message.BuildMultiPartMessageData(6, 5, 1, part3, completeData);

            // Assert
            Assert.That(new byte[]{0x00, 0x01, 0x02, 0x03, 0x04, 0x05}, Is.EqualTo(completeData.ToArray()));
        }

        [Test]
        public void CalculateMaximumMessageSize_Clear()
        {
            // Arrange
            // Act
            ushort actual = Message.CalculateMaximumMessageSize(128);

            // Assert
            Assert.That(120, Is.EqualTo(actual));
        }

        [Test]
        public void CalculateMaximumMessageSize_Encrypted()
        {
            // Arrange
            // Act
            ushort actual = Message.CalculateMaximumMessageSize(129, true);

            // Assert
            Assert.That(112, Is.EqualTo(actual));
        }

        [TestCase("05-00-10-00-12-AB",
            ExpectedResult = "05-00-10-00-12-AB-80-00-00-00-00-00-00-00-00-00")]
        [TestCase("05-00-58-00-12-AB-CC-CC-CC-CC-CC-CC-CC-CC-CC",
            ExpectedResult = "05-00-58-00-12-AB-CC-CC-CC-CC-CC-CC-CC-CC-CC-80")]
        [TestCase("05-00-60-00-12-AB-CC-CC-CC-CC-CC-CD-CC-CC-CC-CC",
            ExpectedResult = "05-00-60-00-12-AB-CC-CC-CC-CC-CC-CD-CC-CC-CC-CC-" +
                             "80-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00")]
        public string PadThisData(string buffer)
        {
            var channel = new ACUMessageSecureChannel();
            return BitConverter.ToString(channel.PadTheData(BinaryUtils.HexToBytes(buffer).ToArray()).ToArray());
        }
    }
}