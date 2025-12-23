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
            var extId = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "Test Corp")
            });

            var data = extId.BuildData();

            // Parse the built data to verify
            var parsed = ExtendedDeviceIdentification.ParseData(data);
            Assert.That(parsed.Entries.Count, Is.EqualTo(1));
            Assert.That(parsed.Manufacturer, Is.EqualTo("Test Corp"));
        }

        [Test]
        public void BuildData_MultipleEntries_BuildsCorrectTlvStream()
        {
            var extId = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "ACME Inc"),
                new ExtendedIdEntry(ExtendedIdTag.ProductName, "Widget Pro"),
                new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "SN123456")
            });

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
            var extId = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.FirmwareVersion, "Core 1.0.0"),
                new ExtendedIdEntry(ExtendedIdTag.FirmwareVersion, "Radio 2.1.3"),
                new ExtendedIdEntry(ExtendedIdTag.FirmwareVersion, "Display 3.0.1")
            });

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
            var extId = new ExtendedDeviceIdentification(new ExtendedIdEntry[0]);

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
            var extId = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "Manufacturer"),
                new ExtendedIdEntry(ExtendedIdTag.ProductName, "Product"),
                new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "Serial"),
                new ExtendedIdEntry(ExtendedIdTag.FirmwareVersion, "1.0.0"),
                new ExtendedIdEntry(ExtendedIdTag.HardwareDescription, "Hardware"),
                new ExtendedIdEntry(ExtendedIdTag.Url, "https://example.com"),
                new ExtendedIdEntry(ExtendedIdTag.ConfigurationReference, "Config")
            });

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
        public void ToString_FormatsCorrectly()
        {
            var extId = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "ACME"),
                new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "12345")
            });

            var str = extId.ToString();

            Assert.That(str, Does.Contain("Manufacturer: ACME"));
            Assert.That(str, Does.Contain("Serial Number: 12345"));
        }

        [Test]
        public void RoundTrip_PreservesAllData()
        {
            var original = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "Test Manufacturer Ltd."),
                new ExtendedIdEntry(ExtendedIdTag.ProductName, "RX Series"),
                new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "EFF569CB89B600140000"),
                new ExtendedIdEntry(ExtendedIdTag.FirmwareVersion, "Core -> AVx90 5.00.47"),
                new ExtendedIdEntry(ExtendedIdTag.FirmwareVersion, "Keypad -> RGB4 Rev. 3"),
                new ExtendedIdEntry(ExtendedIdTag.HardwareDescription, "PRX60BLE"),
                new ExtendedIdEntry(ExtendedIdTag.Url, "https://www.example.com"),
                new ExtendedIdEntry(ExtendedIdTag.ConfigurationReference, "86FE via OSDP")
            });

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

        [Test]
        public void ParseData_UnknownTags_ParsesSuccessfully()
        {
            // Create data with unknown tag 0x0A (10)
            var extId = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "ACME"),
                new ExtendedIdEntry(0x0A, "Custom Data"),
                new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "12345")
            });

            var data = extId.BuildData();
            var parsed = ExtendedDeviceIdentification.ParseData(data);

            Assert.That(parsed.Entries.Count, Is.EqualTo(3));
            Assert.That(parsed.Manufacturer, Is.EqualTo("ACME"));
            Assert.That(parsed.SerialNumber, Is.EqualTo("12345"));
        }

        [Test]
        public void IsKnown_KnownTags_ReturnsTrue()
        {
            var entry = new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "Test");
            Assert.That(entry.IsKnown, Is.True);
        }

        [Test]
        public void IsKnown_UnknownTags_ReturnsFalse()
        {
            var entry = new ExtendedIdEntry(0xFF, "Unknown");
            Assert.That(entry.IsKnown, Is.False);
        }

        [Test]
        public void UnknownEntries_MixedTags_ReturnsOnlyUnknownTags()
        {
            var extId = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "ACME"),
                new ExtendedIdEntry(0x0A, "Custom 1"),
                new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "12345"),
                new ExtendedIdEntry(0x0B, "Custom 2")
            });

            var unknown = extId.UnknownEntries.ToList();

            Assert.That(unknown.Count, Is.EqualTo(2));
            Assert.That(unknown[0].TagByte, Is.EqualTo(0x0A));
            Assert.That(unknown[0].Value, Is.EqualTo("Custom 1"));
            Assert.That(unknown[1].TagByte, Is.EqualTo(0x0B));
            Assert.That(unknown[1].Value, Is.EqualTo("Custom 2"));
        }

        [Test]
        public void UnknownEntries_NoUnknownTags_ReturnsEmpty()
        {
            var extId = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "ACME"),
                new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "12345")
            });

            var unknown = extId.UnknownEntries.ToList();

            Assert.That(unknown.Count, Is.EqualTo(0));
        }

        [Test]
        public void ToString_UnknownTags_DisplaysUnknownTags()
        {
            var extId = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "ACME"),
                new ExtendedIdEntry(0x0A, "Vendor Extension"),
                new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "12345")
            });

            var str = extId.ToString();

            Assert.That(str, Does.Contain("Manufacturer: ACME"));
            Assert.That(str, Does.Contain("Serial Number: 12345"));
            Assert.That(str, Does.Contain("Unknown Tag 10: Vendor Extension"));
        }

        [Test]
        public void RoundTrip_UnknownTags_PreservesUnknownTags()
        {
            var original = new ExtendedDeviceIdentification(new[]
            {
                new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "Test Corp"),
                new ExtendedIdEntry(0x0A, "Custom Field 1"),
                new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "SN-001"),
                new ExtendedIdEntry(0xFF, "Vendor Specific")
            });

            var data = original.BuildData();
            var parsed = ExtendedDeviceIdentification.ParseData(data);

            Assert.That(parsed.Entries.Count, Is.EqualTo(4));
            Assert.That(parsed.Manufacturer, Is.EqualTo("Test Corp"));
            Assert.That(parsed.SerialNumber, Is.EqualTo("SN-001"));

            var unknownParsed = parsed.UnknownEntries.ToList();
            var unknownOriginal = original.UnknownEntries.ToList();

            Assert.That(unknownParsed.Count, Is.EqualTo(2));
            Assert.That(unknownParsed[0].TagByte, Is.EqualTo(unknownOriginal[0].TagByte));
            Assert.That(unknownParsed[0].Value, Is.EqualTo(unknownOriginal[0].Value));
            Assert.That(unknownParsed[1].TagByte, Is.EqualTo(unknownOriginal[1].TagByte));
            Assert.That(unknownParsed[1].Value, Is.EqualTo(unknownOriginal[1].Value));
        }

        [Test]
        public void ExtendedIdEntry_ToString_UnknownTag_FormatsCorrectly()
        {
            var entry = new ExtendedIdEntry(0xAB, "Test Value");

            var str = entry.ToString();

            Assert.That(str, Is.EqualTo("Unknown Tag 171: Test Value"));
        }

        [Test]
        public void ExtendedIdEntry_ToString_KnownTag_FormatsCorrectly()
        {
            var entry = new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "ACME");

            var str = entry.ToString();

            Assert.That(str, Is.EqualTo("Manufacturer: ACME"));
        }

        [TestFixture]
        public class BuilderTests
        {
            [Test]
            public void Builder_BasicProperties_BuildsCorrectly()
            {
                var extId = new ExtendedDeviceIdentificationBuilder()
                    .WithManufacturer("ACME Inc")
                    .WithProductName("Widget Pro")
                    .WithSerialNumber("SN123456")
                    .Build();

                Assert.That(extId.Manufacturer, Is.EqualTo("ACME Inc"));
                Assert.That(extId.ProductName, Is.EqualTo("Widget Pro"));
                Assert.That(extId.SerialNumber, Is.EqualTo("SN123456"));
            }

            [Test]
            public void Builder_AllStandardProperties_BuildsCorrectly()
            {
                var extId = new ExtendedDeviceIdentificationBuilder()
                    .WithManufacturer("Test Manufacturer")
                    .WithProductName("Test Product")
                    .WithSerialNumber("SN-001")
                    .WithFirmwareVersion("1.0.0")
                    .WithHardwareDescription("Hardware v2")
                    .WithUrl("https://example.com")
                    .WithConfigurationReference("Config-123")
                    .Build();

                Assert.That(extId.Manufacturer, Is.EqualTo("Test Manufacturer"));
                Assert.That(extId.ProductName, Is.EqualTo("Test Product"));
                Assert.That(extId.SerialNumber, Is.EqualTo("SN-001"));
                Assert.That(extId.FirmwareVersions.First(), Is.EqualTo("1.0.0"));
                Assert.That(extId.HardwareDescription, Is.EqualTo("Hardware v2"));
                Assert.That(extId.Url, Is.EqualTo("https://example.com"));
                Assert.That(extId.ConfigurationReference, Is.EqualTo("Config-123"));
            }

            [Test]
            public void Builder_MultipleFirmwareVersions_BuildsCorrectly()
            {
                var extId = new ExtendedDeviceIdentificationBuilder()
                    .WithManufacturer("ACME")
                    .WithFirmwareVersion("Core 1.0.0")
                    .WithFirmwareVersion("Radio 2.1.3")
                    .WithFirmwareVersion("Display 3.0.1")
                    .Build();

                var versions = extId.FirmwareVersions.ToList();
                Assert.That(versions.Count, Is.EqualTo(3));
                Assert.That(versions[0], Is.EqualTo("Core 1.0.0"));
                Assert.That(versions[1], Is.EqualTo("Radio 2.1.3"));
                Assert.That(versions[2], Is.EqualTo("Display 3.0.1"));
            }

            [Test]
            public void Builder_CustomEntry_BuildsCorrectly()
            {
                var extId = new ExtendedDeviceIdentificationBuilder()
                    .WithManufacturer("ACME")
                    .WithCustomEntry(0x0A, "Vendor Extension")
                    .WithSerialNumber("12345")
                    .Build();

                Assert.That(extId.Entries.Count, Is.EqualTo(3));
                Assert.That(extId.Manufacturer, Is.EqualTo("ACME"));
                Assert.That(extId.SerialNumber, Is.EqualTo("12345"));

                var unknown = extId.UnknownEntries.ToList();
                Assert.That(unknown.Count, Is.EqualTo(1));
                Assert.That(unknown[0].TagByte, Is.EqualTo(0x0A));
                Assert.That(unknown[0].Value, Is.EqualTo("Vendor Extension"));
            }

            [Test]
            public void Builder_MultipleCustomEntries_BuildsCorrectly()
            {
                var extId = new ExtendedDeviceIdentificationBuilder()
                    .WithManufacturer("Test Corp")
                    .WithCustomEntry(0x0A, "Custom Field 1")
                    .WithCustomEntry(0x0B, "Custom Field 2")
                    .WithCustomEntry(0xFF, "Vendor Specific")
                    .Build();

                var unknown = extId.UnknownEntries.ToList();
                Assert.That(unknown.Count, Is.EqualTo(3));
                Assert.That(unknown[0].TagByte, Is.EqualTo(0x0A));
                Assert.That(unknown[0].Value, Is.EqualTo("Custom Field 1"));
                Assert.That(unknown[1].TagByte, Is.EqualTo(0x0B));
                Assert.That(unknown[1].Value, Is.EqualTo("Custom Field 2"));
                Assert.That(unknown[2].TagByte, Is.EqualTo(0xFF));
                Assert.That(unknown[2].Value, Is.EqualTo("Vendor Specific"));
            }

            [Test]
            public void Builder_WithEntry_UsingTag_BuildsCorrectly()
            {
                var extId = new ExtendedDeviceIdentificationBuilder()
                    .WithEntry(ExtendedIdTag.Manufacturer, "ACME")
                    .WithEntry(ExtendedIdTag.ProductName, "Widget")
                    .Build();

                Assert.That(extId.Manufacturer, Is.EqualTo("ACME"));
                Assert.That(extId.ProductName, Is.EqualTo("Widget"));
            }

            [Test]
            public void Builder_WithEntry_UsingExtendedIdEntry_BuildsCorrectly()
            {
                var entry = new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "ACME");
                var extId = new ExtendedDeviceIdentificationBuilder()
                    .WithEntry(entry)
                    .Build();

                Assert.That(extId.Manufacturer, Is.EqualTo("ACME"));
            }

            [Test]
            public void Builder_EmptyBuilder_BuildsEmptyIdentification()
            {
                var extId = new ExtendedDeviceIdentificationBuilder().Build();

                Assert.That(extId.Entries.Count, Is.EqualTo(0));
                Assert.That(extId.Manufacturer, Is.Null);
            }

            [Test]
            public void Builder_RoundTrip_PreservesAllData()
            {
                var extId = new ExtendedDeviceIdentificationBuilder()
                    .WithManufacturer("Test Manufacturer Ltd.")
                    .WithProductName("RX Series")
                    .WithSerialNumber("EFF569CB89B600140000")
                    .WithFirmwareVersion("Core -> AVx90 5.00.47")
                    .WithFirmwareVersion("Keypad -> RGB4 Rev. 3")
                    .WithHardwareDescription("PRX60BLE")
                    .WithUrl("https://www.example.com")
                    .WithConfigurationReference("86FE via OSDP")
                    .WithCustomEntry(0x0A, "Custom Data")
                    .Build();

                var data = extId.BuildData();
                var parsed = ExtendedDeviceIdentification.ParseData(data);

                Assert.That(parsed.Manufacturer, Is.EqualTo(extId.Manufacturer));
                Assert.That(parsed.ProductName, Is.EqualTo(extId.ProductName));
                Assert.That(parsed.SerialNumber, Is.EqualTo(extId.SerialNumber));
                Assert.That(parsed.FirmwareVersions.ToList(), Is.EqualTo(extId.FirmwareVersions.ToList()));
                Assert.That(parsed.HardwareDescription, Is.EqualTo(extId.HardwareDescription));
                Assert.That(parsed.Url, Is.EqualTo(extId.Url));
                Assert.That(parsed.ConfigurationReference, Is.EqualTo(extId.ConfigurationReference));

                var unknownParsed = parsed.UnknownEntries.ToList();
                var unknownOriginal = extId.UnknownEntries.ToList();
                Assert.That(unknownParsed.Count, Is.EqualTo(1));
                Assert.That(unknownParsed[0].TagByte, Is.EqualTo(unknownOriginal[0].TagByte));
                Assert.That(unknownParsed[0].Value, Is.EqualTo(unknownOriginal[0].Value));
            }

            [Test]
            public void Builder_MethodChaining_WorksCorrectly()
            {
                // Test that all builder methods return the builder instance for chaining
                var builder = new ExtendedDeviceIdentificationBuilder();
                var result = builder
                    .WithManufacturer("Test")
                    .WithProductName("Product")
                    .WithSerialNumber("SN")
                    .WithFirmwareVersion("1.0")
                    .WithHardwareDescription("HW")
                    .WithUrl("URL")
                    .WithConfigurationReference("Config")
                    .WithEntry(ExtendedIdTag.Manufacturer, "Override")
                    .WithCustomEntry(0x0A, "Custom")
                    .WithEntry(new ExtendedIdEntry(ExtendedIdTag.ProductName, "Entry"));

                Assert.That(result, Is.SameAs(builder));
            }

            [Test]
            public void Builder_ComparisonWithDirectConstruction_ProducesSameResult()
            {
                // Build using the builder
                var fromBuilder = new ExtendedDeviceIdentificationBuilder()
                    .WithManufacturer("ACME Inc")
                    .WithProductName("Widget Pro")
                    .WithSerialNumber("SN123456")
                    .Build();

                // Build using direct construction
                var fromDirect = new ExtendedDeviceIdentification(new[]
                {
                    new ExtendedIdEntry(ExtendedIdTag.Manufacturer, "ACME Inc"),
                    new ExtendedIdEntry(ExtendedIdTag.ProductName, "Widget Pro"),
                    new ExtendedIdEntry(ExtendedIdTag.SerialNumber, "SN123456")
                });

                // Verify they produce the same data
                var builderData = fromBuilder.BuildData();
                var directData = fromDirect.BuildData();

                Assert.That(builderData, Is.EqualTo(directData));
            }
        }
    }
}
