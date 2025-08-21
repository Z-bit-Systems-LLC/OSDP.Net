using System.Collections.Generic;
using NUnit.Framework;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData
{
    [Category("Unit")]
    public class KeypadDataTest
    {
        [Test]
        public void ParseData()
        {
            // Arrange
            var data = new List<byte> {0x01, 0x05};

            data.AddRange(new byte[] {0x00, 0x01, 0x02, 0x03, 0x04});

            // Act
            var keypadData = KeypadData.ParseData(data.ToArray());

            // Assert
            Assert.That(1, Is.EqualTo(keypadData.ReaderNumber));
            Assert.That(5, Is.EqualTo(keypadData.DigitCount));
            Assert.That(new byte[] {0x00, 0x01, 0x02, 0x03, 0x04}, Is.EqualTo(keypadData.Data));
        }

        [Test]
        public void ParseNoData()
        {
            // Arrange
            var data = new List<byte> {0x01, 0x00};

            data.AddRange(new byte[] {});

            // Act
            var keypadData = KeypadData.ParseData(data.ToArray());

            // Assert
            Assert.That(1, Is.EqualTo(keypadData.ReaderNumber));
            Assert.That(0, Is.EqualTo(keypadData.DigitCount));
            Assert.That(new byte[] {},Is.EqualTo( keypadData.Data));
        }
    }
}