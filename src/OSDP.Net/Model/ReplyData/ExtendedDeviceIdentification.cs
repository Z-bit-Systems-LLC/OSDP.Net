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
        /// Gets all entries with unknown/undefined tag types.
        /// These may be vendor-specific extensions or future OSDP specification additions.
        /// </summary>
        public IEnumerable<ExtendedIdEntry> UnknownEntries => _entries.Where(e => !e.IsKnown);

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
        /// <param name="data">The complete message data which may include multi-part message header.</param>
        /// <returns>The parsed ExtendedDeviceIdentification.</returns>
        public static ExtendedDeviceIdentification ParseData(ReadOnlySpan<byte> data)
        {
            // Check if data starts with multi-part message header (6 bytes minimum)
            // If so, skip the header and parse only the TLV data
            ReadOnlySpan<byte> tlvData = data;

            if (data.Length >= 6)
            {
                // Check if this looks like a multi-part message header by validating structure
                ushort offset = (ushort)(data[2] | (data[3] << 8));
                ushort lengthOfFragment = (ushort)(data[4] | (data[5] << 8));

                // If the multi-part header is present and valid, skip it
                if (data.Length == 6 + lengthOfFragment && offset == 0)
                {
                    tlvData = data.Slice(6);
                }
            }

            var entries = new List<ExtendedIdEntry>();
            int parseOffset = 0;

            while (parseOffset < tlvData.Length)
            {
                var entry = ExtendedIdEntry.ParseData(tlvData.Slice(parseOffset), out int bytesConsumed);
                if (entry == null || bytesConsumed == 0)
                {
                    if (parseOffset < tlvData.Length)
                    {
                        throw new Exception($"Failed to parse TLV entry at offset {parseOffset}, {tlvData.Length - parseOffset} bytes remaining");
                    }
                    break;
                }

                entries.Add(entry);
                parseOffset += bytesConsumed;
            }

            return new ExtendedDeviceIdentification(entries);
        }

        /// <inheritdoc/>
        public override byte[] BuildData()
        {
            // Build the TLV entries
            var tlvData = new List<byte>();
            foreach (var entry in _entries)
            {
                tlvData.AddRange(entry.BuildData());
            }

            // Extended ID Report must be sent as a multi-part message format:
            // 2 bytes: Whole Message Length (little-endian)
            // 2 bytes: Offset (little-endian)
            // 2 bytes: Length of Fragment (little-endian)
            // N bytes: Data (the TLV stream)
            var wholeMessageLength = (ushort)tlvData.Count;
            var result = new List<byte>
            {
                // Whole Message Length (LSB, MSB)
                (byte)(wholeMessageLength & 0xFF),
                (byte)((wholeMessageLength >> 8) & 0xFF),

                // Offset (LSB, MSB) - always 0 for single fragment
                0x00,
                0x00,

                // Length of Fragment (LSB, MSB) - same as whole message for single fragment
                (byte)(wholeMessageLength & 0xFF),
                (byte)((wholeMessageLength >> 8) & 0xFF)
            };

            // Add the TLV data
            result.AddRange(tlvData);

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

            // Display any unknown/undefined tags
            var unknownEntries = UnknownEntries.ToList();
            if (unknownEntries.Any())
            {
                foreach (var entry in unknownEntries)
                {
                    build.AppendLine($"{padding}  Unknown Tag {entry.TagByte}: {entry.Value}");
                }
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
