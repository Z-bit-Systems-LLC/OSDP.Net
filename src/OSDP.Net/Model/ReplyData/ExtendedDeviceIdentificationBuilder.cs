using System.Collections.Generic;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// Fluent builder for creating ExtendedDeviceIdentification instances.
    /// Provides a clean API for constructing extended ID responses with support for both standard and custom TLV entries.
    /// </summary>
    public class ExtendedDeviceIdentificationBuilder
    {
        private readonly List<ExtendedIdEntry> _entries = new();

        /// <summary>
        /// Sets the manufacturer name.
        /// </summary>
        /// <param name="manufacturer">The manufacturer name.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithManufacturer(string manufacturer)
        {
            return WithEntry(ExtendedIdTag.Manufacturer, manufacturer);
        }

        /// <summary>
        /// Sets the product name.
        /// </summary>
        /// <param name="productName">The product name.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithProductName(string productName)
        {
            return WithEntry(ExtendedIdTag.ProductName, productName);
        }

        /// <summary>
        /// Sets the serial number.
        /// </summary>
        /// <param name="serialNumber">The serial number.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithSerialNumber(string serialNumber)
        {
            return WithEntry(ExtendedIdTag.SerialNumber, serialNumber);
        }

        /// <summary>
        /// Adds a firmware version. Multiple firmware versions can be added for devices with multiple microcontrollers.
        /// </summary>
        /// <param name="firmwareVersion">The firmware version string.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithFirmwareVersion(string firmwareVersion)
        {
            return WithEntry(ExtendedIdTag.FirmwareVersion, firmwareVersion);
        }

        /// <summary>
        /// Sets the hardware description.
        /// </summary>
        /// <param name="hardwareDescription">The hardware description.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithHardwareDescription(string hardwareDescription)
        {
            return WithEntry(ExtendedIdTag.HardwareDescription, hardwareDescription);
        }

        /// <summary>
        /// Sets the URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithUrl(string url)
        {
            return WithEntry(ExtendedIdTag.Url, url);
        }

        /// <summary>
        /// Sets the configuration reference.
        /// </summary>
        /// <param name="configurationReference">The configuration reference.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithConfigurationReference(string configurationReference)
        {
            return WithEntry(ExtendedIdTag.ConfigurationReference, configurationReference);
        }

        /// <summary>
        /// Adds a standard entry using a known tag.
        /// </summary>
        /// <param name="tag">The standard tag type.</param>
        /// <param name="value">The value for this entry.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithEntry(ExtendedIdTag tag, string value)
        {
            _entries.Add(new ExtendedIdEntry(tag, value));
            return this;
        }

        /// <summary>
        /// Adds a custom/vendor-specific entry using a raw tag byte.
        /// Use this for vendor-specific extensions or tags not defined in the OSDP specification.
        /// </summary>
        /// <param name="tagByte">The custom tag byte value.</param>
        /// <param name="value">The value for this entry.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithCustomEntry(byte tagByte, string value)
        {
            _entries.Add(new ExtendedIdEntry(tagByte, value));
            return this;
        }

        /// <summary>
        /// Adds a pre-constructed ExtendedIdEntry.
        /// </summary>
        /// <param name="entry">The entry to add.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public ExtendedDeviceIdentificationBuilder WithEntry(ExtendedIdEntry entry)
        {
            _entries.Add(entry);
            return this;
        }

        /// <summary>
        /// Builds the immutable ExtendedDeviceIdentification instance.
        /// </summary>
        /// <returns>A new ExtendedDeviceIdentification instance with all configured entries.</returns>
        public ExtendedDeviceIdentification Build()
        {
            return new ExtendedDeviceIdentification(_entries);
        }
    }
}
