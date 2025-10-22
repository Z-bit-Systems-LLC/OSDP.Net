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

        [Test]
        public void BuildData_WithKeypadData_ReturnsCorrectByteArray()
        {
            // Arrange
            var keypadData = new KeypadData(0, "1234");

            // Act
            var result = keypadData.BuildData();

            // Assert
            Assert.That(6, Is.EqualTo(result.Length));
            Assert.That(0x00, Is.EqualTo(result[0])); // Reader number
            Assert.That(0x04, Is.EqualTo(result[1])); // Digit count (4 characters)
            Assert.That(0x31, Is.EqualTo(result[2])); // ASCII '1'
            Assert.That(0x32, Is.EqualTo(result[3])); // ASCII '2'
            Assert.That(0x33, Is.EqualTo(result[4])); // ASCII '3'
            Assert.That(0x34, Is.EqualTo(result[5])); // ASCII '4'
        }

        [Test]
        public void BuildData_WithSpecialCharacters_ConvertsCorrectly()
        {
            // Arrange
            var keypadData = new KeypadData(0, "12*#");

            // Act
            var result = keypadData.BuildData();

            // Assert
            Assert.That(6, Is.EqualTo(result.Length));
            Assert.That(0x00, Is.EqualTo(result[0])); // Reader number
            Assert.That(0x04, Is.EqualTo(result[1])); // Digit count (4 characters)
            Assert.That(0x31, Is.EqualTo(result[2])); // ASCII '1'
            Assert.That(0x32, Is.EqualTo(result[3])); // ASCII '2'
            Assert.That(0x7F, Is.EqualTo(result[4])); // '*' as DELETE
            Assert.That(0x0D, Is.EqualTo(result[5])); // '#' as return
        }

        [Test]
        public void Constructor_WithSpecialCharacters_EncodesCorrectly()
        {
            // Arrange & Act
            var keypadData = new KeypadData(0, "*#123");

            // Assert
            Assert.That(5, Is.EqualTo(keypadData.DigitCount));
            Assert.That(0x7F, Is.EqualTo(keypadData.Data[0])); // '*' as DELETE
            Assert.That(0x0D, Is.EqualTo(keypadData.Data[1])); // '#' as return
            Assert.That(0x31, Is.EqualTo(keypadData.Data[2])); // ASCII '1'
            Assert.That(0x32, Is.EqualTo(keypadData.Data[3])); // ASCII '2'
            Assert.That(0x33, Is.EqualTo(keypadData.Data[4])); // ASCII '3'
        }

        [Test]
        public void Code_ReturnsKeypadDataReplyType()
        {
            // Arrange
            var keypadData = new KeypadData(0, "1234");

            // Act
            var code = keypadData.Code;

            // Assert
            Assert.That(0x53, Is.EqualTo(code)); // osdp_KEYPAD = 0x53
        }

        [Test]
        public void Constructor_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var keypadData = new KeypadData(1, "1234");

            // Assert
            Assert.That(1, Is.EqualTo(keypadData.ReaderNumber));
            Assert.That(4, Is.EqualTo(keypadData.DigitCount));
            Assert.That(4, Is.EqualTo(keypadData.Data.Length));
        }

        [Test]
        public void ParseData_WithSpecialCharacters_ParsesCorrectly()
        {
            // Arrange - simulate data with * and # encoded as 0x7F and 0x0D
            var data = new List<byte> {0x00, 0x04}; // Reader 0, 4 characters
            data.AddRange(new byte[] {0x31, 0x32, 0x7F, 0x0D}); // "12*#"

            // Act
            var keypadData = KeypadData.ParseData(data.ToArray());

            // Assert
            Assert.That(0, Is.EqualTo(keypadData.ReaderNumber));
            Assert.That(4, Is.EqualTo(keypadData.DigitCount));
            Assert.That(0x31, Is.EqualTo(keypadData.Data[0])); // '1'
            Assert.That(0x32, Is.EqualTo(keypadData.Data[1])); // '2'
            Assert.That(0x7F, Is.EqualTo(keypadData.Data[2])); // '*' encoded as DELETE
            Assert.That(0x0D, Is.EqualTo(keypadData.Data[3])); // '#' encoded as return
        }

        [Test]
        public void RoundTrip_WithSpecialCharacters_PreservesData()
        {
            // Arrange - create keypad data with special characters
            var original = new KeypadData(1, "123*#456");

            // Act - build the data and parse it back
            var built = original.BuildData();
            var parsed = KeypadData.ParseData(built);

            // Assert
            Assert.That(original.ReaderNumber, Is.EqualTo(parsed.ReaderNumber));
            Assert.That(original.DigitCount, Is.EqualTo(parsed.DigitCount));
            Assert.That(original.Data, Is.EqualTo(parsed.Data));
        }
    }
}