using NUnit.Framework;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData
{
    [TestFixture]
    [Category("Unit")]
    public class ExtendedIdEntryTest
    {
        [Test]
        public void BuildData_SimpleString_CreatesCorrectTlv()
        {
            var entry = new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "Test");
            var data = entry.BuildData();

            // Tag (0x00) + Length (0x04, 0x00) + Data ("Test" = 4 bytes)
            Assert.That(data.Length, Is.EqualTo(7));
            Assert.That(data[0], Is.EqualTo(0x00)); // Tag for Manufacturer
            Assert.That(data[1], Is.EqualTo(0x04)); // Length LSB
            Assert.That(data[2], Is.EqualTo(0x00)); // Length MSB
            Assert.That(System.Text.Encoding.UTF8.GetString(data, 3, 4), Is.EqualTo("Test"));
        }

        [Test]
        public void BuildData_EmptyString_CreatesZeroLengthTlv()
        {
            var entry = new ExtendedIdEntry(ExtendedIdTag.Url, "");
            var data = entry.BuildData();

            Assert.That(data.Length, Is.EqualTo(3)); // Tag + 2 bytes for length
            Assert.That(data[0], Is.EqualTo(0x05)); // Tag for Url
            Assert.That(data[1], Is.EqualTo(0x00)); // Length LSB
            Assert.That(data[2], Is.EqualTo(0x00)); // Length MSB
        }

        [Test]
        public void ParseData_ValidTlv_ParsesCorrectly()
        {
            // Tag = 0x01 (ProductName), Length = 7, Data = "Product"
            var data = new byte[] { 0x01, 0x07, 0x00, 0x50, 0x72, 0x6F, 0x64, 0x75, 0x63, 0x74 };
            var entry = ExtendedIdEntry.ParseData(data, out int bytesConsumed);

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.Tag, Is.EqualTo(ExtendedIdTag.ProductName));
            Assert.That(entry.Value, Is.EqualTo("Product"));
            Assert.That(bytesConsumed, Is.EqualTo(10));
        }

        [Test]
        public void ParseData_InsufficientData_ReturnsNull()
        {
            var data = new byte[] { 0x00, 0x05 }; // Only 2 bytes, needs at least 3 for header
            var entry = ExtendedIdEntry.ParseData(data, out int bytesConsumed);

            Assert.That(entry, Is.Null);
            Assert.That(bytesConsumed, Is.EqualTo(0));
        }

        [Test]
        public void ParseData_TruncatedValue_ReturnsNull()
        {
            // Header says length is 10, but only 3 bytes of data provided
            var data = new byte[] { 0x00, 0x0A, 0x00, 0x41, 0x42, 0x43 };
            var entry = ExtendedIdEntry.ParseData(data, out int bytesConsumed);

            Assert.That(entry, Is.Null);
            Assert.That(bytesConsumed, Is.EqualTo(0));
        }

        [Test]
        public void RoundTrip_PreservesData()
        {
            var original = new ExtendedIdEntry(ExtendedIdTag.FirmwareVersion, "1.2.3.4");
            var data = original.BuildData();
            var parsed = ExtendedIdEntry.ParseData(data, out _);

            Assert.That(parsed.Tag, Is.EqualTo(original.Tag));
            Assert.That(parsed.Value, Is.EqualTo(original.Value));
        }

        [Test]
        public void BuildData_UnicodeString_EncodesAsUtf8()
        {
            var entry = new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "Tëst Mfg");
            var data = entry.BuildData();

            // UTF-8 encoding of "Tëst Mfg" is 9 bytes (ë is 2 bytes in UTF-8)
            Assert.That(data.Length, Is.EqualTo(12)); // 3 header + 9 data
            Assert.That(data[1], Is.EqualTo(0x09)); // Length should be 9
        }

        [Test]
        public void ParseData_Utf8Content_DecodesCorrectly()
        {
            // "Tëst" in UTF-8: 54 C3 AB 73 74 (5 bytes)
            var data = new byte[] { 0x00, 0x05, 0x00, 0x54, 0xC3, 0xAB, 0x73, 0x74 };
            var entry = ExtendedIdEntry.ParseData(data, out _);

            Assert.That(entry.Value, Is.EqualTo("Tëst"));
        }

        [Test]
        public void Constructor_WithRawTagByte_SetsTagCorrectly()
        {
            var entry = new ExtendedIdEntry(0xFF, "Private tag data");

            Assert.That(entry.TagByte, Is.EqualTo(0xFF));
            Assert.That(entry.Value, Is.EqualTo("Private tag data"));
        }

        [Test]
        public void ToString_ReturnsFormattedString()
        {
            var entry = new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "ABC123");

            Assert.That(entry.ToString(), Is.EqualTo("SerialNumber: ABC123"));
        }
    }
}
