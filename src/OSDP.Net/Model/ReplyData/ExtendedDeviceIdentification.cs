using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// The extended PD identification data sent as a reply to an osdp_ID command with data 0x01.
    /// Contains a TLV (Tag-Length-Value) stream with detailed device information.
    /// </summary>
    public class ExtendedDeviceIdentification : PayloadData
    {
        private readonly List<ExtendedIdEntry> _entries;

        /// <summary>
        /// Creates a new instance of ExtendedDeviceIdentification with the specified entries.
        /// </summary>
        /// <param name="entries">The TLV entries for this identification.</param>
        public ExtendedDeviceIdentification(IEnumerable<ExtendedIdEntry> entries)
        {
            _entries = entries?.ToList() ?? new List<ExtendedIdEntry>();
        }

        /// <summary>
        /// Gets all TLV entries in this identification.
        /// </summary>
        public IReadOnlyList<ExtendedIdEntry> Entries => _entries;

        /// <summary>
        /// Gets the manufacturer name, or null if not present.
        /// </summary>
        public string Manufacturer => GetFirstValue(ExtendedIdTag.Manufacturer);

        /// <summary>
        /// Gets the product name, or null if not present.
        /// </summary>
        public string ProductName => GetFirstValue(ExtendedIdTag.ProductName);

        /// <summary>
        /// Gets the serial number, or null if not present.
        /// </summary>
        public string SerialNumber => GetFirstValue(ExtendedIdTag.SerialNumber);

        /// <summary>
        /// Gets all firmware version entries. Multiple entries are allowed for different microcontrollers.
        /// </summary>
        public IEnumerable<string> FirmwareVersions => GetValues(ExtendedIdTag.FirmwareVersion);

        /// <summary>
        /// Gets the hardware description, or null if not present.
        /// </summary>
        public string HardwareDescription => GetFirstValue(ExtendedIdTag.HardwareDescription);

        /// <summary>
        /// Gets the URL, or null if not present.
        /// </summary>
        public string Url => GetFirstValue(ExtendedIdTag.Url);

        /// <summary>
        /// Gets the configuration reference, or null if not present.
        /// </summary>
        public string ConfigurationReference => GetFirstValue(ExtendedIdTag.ConfigurationReference);

        /// <inheritdoc/>
        public override byte Code => (byte)ReplyType.ExtendedPdIdReport;

        /// <inheritdoc/>
        public override ReadOnlySpan<byte> SecurityControlBlock()
        {
            return SecurityBlock.ReplyMessageWithDataSecurity;
        }

        /// <summary>
        /// Parses the TLV stream from the given data.
        /// </summary>
        /// <param name="data">The complete TLV stream data (without multi-part message header).</param>
        /// <returns>The parsed ExtendedDeviceIdentification.</returns>
        public static ExtendedDeviceIdentification ParseData(ReadOnlySpan<byte> data)
        {
            var entries = new List<ExtendedIdEntry>();
            int offset = 0;

            while (offset < data.Length)
            {
                var entry = ExtendedIdEntry.ParseData(data.Slice(offset), out int bytesConsumed);
                if (entry == null || bytesConsumed == 0)
                {
                    if (offset < data.Length)
                    {
                        throw new Exception($"Failed to parse TLV entry at offset {offset}, {data.Length - offset} bytes remaining");
                    }
                    break;
                }

                entries.Add(entry);
                offset += bytesConsumed;
            }

            return new ExtendedDeviceIdentification(entries);
        }

        /// <inheritdoc/>
        public override byte[] BuildData()
        {
            var result = new List<byte>();

            foreach (var entry in _entries)
            {
                result.AddRange(entry.BuildData());
            }

            return result.ToArray();
        }

        /// <inheritdoc/>
        public override string ToString(int indent)
        {
            var padding = new string(' ', indent);
            var build = new StringBuilder();

            if (Manufacturer != null)
            {
                build.AppendLine($"{padding}    Manufacturer: {Manufacturer}");
            }

            if (ProductName != null)
            {
                build.AppendLine($"{padding}    Product Name: {ProductName}");
            }

            if (SerialNumber != null)
            {
                build.AppendLine($"{padding}   Serial Number: {SerialNumber}");
            }

            var firmwareVersions = FirmwareVersions.ToList();
            for (int i = 0; i < firmwareVersions.Count; i++)
            {
                var label = i == 0 ? "Firmware Version" : $"Firmware Version {i + 1}";
                build.AppendLine($"{padding}{label,16}: {firmwareVersions[i]}");
            }

            if (HardwareDescription != null)
            {
                build.AppendLine($"{padding}        Hardware: {HardwareDescription}");
            }

            if (Url != null)
            {
                build.AppendLine($"{padding}             URL: {Url}");
            }

            if (ConfigurationReference != null)
            {
                build.AppendLine($"{padding}   Configuration: {ConfigurationReference}");
            }

            return build.ToString();
        }

        private string GetFirstValue(ExtendedIdTag tag)
        {
            return _entries.FirstOrDefault(e => e.Tag == tag)?.Value;
        }

        private IEnumerable<string> GetValues(ExtendedIdTag tag)
        {
            return _entries.Where(e => e.Tag == tag).Select(e => e.Value);
        }
    }
}
