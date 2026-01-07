using System.Collections.Generic;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData
{
    [Category("Unit")]
    public class PIVDataTest
    {
        [Test]
        public void ParseData()
        {
            // Arrange
            var data = new List<byte>();

            data.AddRange(Message.ConvertShortToBytes(20));
            data.AddRange(Message.ConvertShortToBytes(10));
            data.AddRange(Message.ConvertShortToBytes(5));
            data.AddRange(new byte[] {0x00, 0x01, 0x02, 0x03, 0x04});

            // Act
            var pivData = DataFragmentResponse.ParseData(data.ToArray());

            // Assert
            Assert.That(20, Is.EqualTo(pivData.WholeMessageLength));
            Assert.That(10, Is.EqualTo(pivData.Offset));
            Assert.That(5, Is.EqualTo(pivData.LengthOfFragment));
            Assert.That(new byte[] {0x00, 0x01, 0x02, 0x03, 0x04}, Is.EqualTo(pivData.Data));
        }

        [Test]
        public void ParseDataNoData()
        {
            // Arrange
            var data = new List<byte>();

            data.AddRange(Message.ConvertShortToBytes(20));
            data.AddRange(Message.ConvertShortToBytes(10));
            data.AddRange(Message.ConvertShortToBytes(0));
            data.AddRange(new byte[] { });

            // Act
            var pivData = DataFragmentResponse.ParseData(data.ToArray());

            // Assert
            Assert.That(20, Is.EqualTo(pivData.WholeMessageLength));
            Assert.That(10, Is.EqualTo(pivData.Offset));
            Assert.That(0, Is.EqualTo(pivData.LengthOfFragment));
            Assert.That(new byte[] { }, Is.EqualTo(pivData.Data));
        }
    }
}