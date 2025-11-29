using System.Linq;
using NUnit.Framework;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData
{
    [TestFixture]
    [Category("Unit")]
    public class ExtendedDeviceIdentificationTest
    {
        [Test]
        public void BuildData_SingleEntry_BuildsCorrectTlvStream()
        {
            var extId = new ExtendedDeviceIdentification();
            extId.AddEntry(ExtendedIdTag.Manufacturer, "Test Corp");

            var data = extId.BuildData();

            // Parse the built data to verify
            var parsed = ExtendedDeviceIdentification.ParseData(data);
            Assert.That(parsed.Entries.Count, Is.EqualTo(1));
            Assert.That(parsed.Manufacturer, Is.EqualTo("Test Corp"));
        }

        [Test]
        public void BuildData_MultipleEntries_BuildsCorrectTlvStream()
        {
            var extId = new ExtendedDeviceIdentification();
            extId.AddEntry(ExtendedIdTag.Manufacturer, "ACME Inc");
            extId.AddEntry(ExtendedIdTag.ProductName, "Widget Pro");
            extId.AddEntry(ExtendedIdTag.SerialNumber, "SN123456");

            var data = extId.BuildData();
            var parsed = ExtendedDeviceIdentification.ParseData(data);

            Assert.That(parsed.Entries.Count, Is.EqualTo(3));
            Assert.That(parsed.Manufacturer, Is.EqualTo("ACME Inc"));
            Assert.That(parsed.ProductName, Is.EqualTo("Widget Pro"));
            Assert.That(parsed.SerialNumber, Is.EqualTo("SN123456"));
        }

        [Test]
        public void ParseData_MultipleFirmwareVersions_ReturnsAllVersions()
        {
            var extId = new ExtendedDeviceIdentification();
            extId.AddEntry(ExtendedIdTag.FirmwareVersion, "Core 1.0.0");
            extId.AddEntry(ExtendedIdTag.FirmwareVersion, "Radio 2.1.3");
            extId.AddEntry(ExtendedIdTag.FirmwareVersion, "Display 3.0.1");

            var data = extId.BuildData();
            var parsed = ExtendedDeviceIdentification.ParseData(data);

            var versions = parsed.FirmwareVersions.ToList();
            Assert.That(versions.Count, Is.EqualTo(3));
            Assert.That(versions[0], Is.EqualTo("Core 1.0.0"));
            Assert.That(versions[1], Is.EqualTo("Radio 2.1.3"));
            Assert.That(versions[2], Is.EqualTo("Display 3.0.1"));
        }

        [Test]
        public void Code_ReturnsExtendedPdIdReportCode()
        {
            var extId = new ExtendedDeviceIdentification();

            Assert.That(extId.Code, Is.EqualTo(0x59));
        }

        [Test]
        public void ParseData_EmptyData_ReturnsEmptyIdentification()
        {
            var parsed = ExtendedDeviceIdentification.ParseData(new byte[0]);

            Assert.That(parsed.Entries.Count, Is.EqualTo(0));
            Assert.That(parsed.Manufacturer, Is.Null);
        }

        [Test]
        public void ParseData_AllTagTypes_ParsesCorrectly()
        {
            var extId = new ExtendedDeviceIdentification();
            extId.AddEntry(ExtendedIdTag.Manufacturer, "Manufacturer");
            extId.AddEntry(ExtendedIdTag.ProductName, "Product");
            extId.AddEntry(ExtendedIdTag.SerialNumber, "Serial");
            extId.AddEntry(ExtendedIdTag.FirmwareVersion, "1.0.0");
            extId.AddEntry(ExtendedIdTag.HardwareDescription, "Hardware");
            extId.AddEntry(ExtendedIdTag.Url, "https://example.com");
            extId.AddEntry(ExtendedIdTag.ConfigurationReference, "Config");

            var data = extId.BuildData();
            var parsed = ExtendedDeviceIdentification.ParseData(data);

            Assert.That(parsed.Manufacturer, Is.EqualTo("Manufacturer"));
            Assert.That(parsed.ProductName, Is.EqualTo("Product"));
            Assert.That(parsed.SerialNumber, Is.EqualTo("Serial"));
            Assert.That(parsed.FirmwareVersions.First(), Is.EqualTo("1.0.0"));
            Assert.That(parsed.HardwareDescription, Is.EqualTo("Hardware"));
            Assert.That(parsed.Url, Is.EqualTo("https://example.com"));
            Assert.That(parsed.ConfigurationReference, Is.EqualTo("Config"));
        }

        [Test]
        public void Constructor_WithEntries_PopulatesCorrectly()
        {
            var entries = new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "Test"),
                new ExtendedIdEntry(ExtendedIdTag.ProductName, "Device")
            };

            var extId = new ExtendedDeviceIdentification(entries);

            Assert.That(extId.Entries.Count, Is.EqualTo(2));
            Assert.That(extId.Manufacturer, Is.EqualTo("Test"));
            Assert.That(extId.ProductName, Is.EqualTo("Device"));
        }

        [Test]
        public void AddEntry_WithEntryObject_AddsToList()
        {
            var extId = new ExtendedDeviceIdentification();
            var entry = new ExtendedIdEntry(ExtendedIdTag.Url, "https://test.com");

            extId.AddEntry(entry);

            Assert.That(extId.Entries.Count, Is.EqualTo(1));
            Assert.That(extId.Url, Is.EqualTo("https://test.com"));
        }

        [Test]
        public void ToString_FormatsCorrectly()
        {
            var extId = new ExtendedDeviceIdentification();
            extId.AddEntry(ExtendedIdTag.Manufacturer, "ACME");
            extId.AddEntry(ExtendedIdTag.SerialNumber, "12345");

            var str = extId.ToString(0);

            Assert.That(str, Does.Contain("Manufacturer: ACME"));
            Assert.That(str, Does.Contain("Serial Number: 12345"));
        }

        [Test]
        public void RoundTrip_PreservesAllData()
        {
            var original = new ExtendedDeviceIdentification();
            original.AddEntry(ExtendedIdTag.Manufacturer, "Test Manufacturer Ltd.");
            original.AddEntry(ExtendedIdTag.ProductName, "RX Series");
            original.AddEntry(ExtendedIdTag.SerialNumber, "EFF569CB89B600140000");
            original.AddEntry(ExtendedIdTag.FirmwareVersion, "Core -> AVx90 5.00.47");
            original.AddEntry(ExtendedIdTag.FirmwareVersion, "Keypad -> RGB4 Rev. 3");
            original.AddEntry(ExtendedIdTag.HardwareDescription, "PRX60BLE");
            original.AddEntry(ExtendedIdTag.Url, "https://www.example.com");
            original.AddEntry(ExtendedIdTag.ConfigurationReference, "86FE via OSDP");

            var data = original.BuildData();
            var parsed = ExtendedDeviceIdentification.ParseData(data);

            Assert.That(parsed.Manufacturer, Is.EqualTo(original.Manufacturer));
            Assert.That(parsed.ProductName, Is.EqualTo(original.ProductName));
            Assert.That(parsed.SerialNumber, Is.EqualTo(original.SerialNumber));
            Assert.That(parsed.FirmwareVersions.ToList(), Is.EqualTo(original.FirmwareVersions.ToList()));
            Assert.That(parsed.HardwareDescription, Is.EqualTo(original.HardwareDescription));
            Assert.That(parsed.Url, Is.EqualTo(original.Url));
            Assert.That(parsed.ConfigurationReference, Is.EqualTo(original.ConfigurationReference));
        }
    }
}
