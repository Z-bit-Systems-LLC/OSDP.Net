using System;
using System.Text;
using OSDP.Net.Messages;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// Represents a single TLV (Tag-Length-Value) entry in the extended device identification response.
    /// </summary>
    public class ExtendedIdEntry
    {
        /// <summary>
        /// Creates a new instance of ExtendedIdEntry.
        /// </summary>
        /// <param name="tag">The tag type for this entry.</param>
        /// <param name="value">The UTF-8 string value for this entry.</param>
        public ExtendedIdEntry(ExtendedIdTag tag, string value)
        {
            Tag = tag;
            Value = value ?? string.Empty;
        }

        /// <summary>
        /// Creates a new instance of ExtendedIdEntry with a raw tag byte.
        /// </summary>
        /// <param name="tagByte">The raw tag byte value.</param>
        /// <param name="value">The UTF-8 string value for this entry.</param>
        public ExtendedIdEntry(byte tagByte, string value)
        {
            TagByte = tagByte;
            Value = value ?? string.Empty;
        }

        /// <summary>
        /// Gets the tag type for this entry.
        /// </summary>
        public ExtendedIdTag Tag
        {
            get => (ExtendedIdTag)TagByte;
            private set => TagByte = (byte)value;
        }

        /// <summary>
        /// Gets the raw tag byte value.
        /// </summary>
        public byte TagByte { get; private set; }

        /// <summary>
        /// Gets the UTF-8 string value for this entry.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Builds the TLV byte array for this entry.
        /// </summary>
        /// <returns>The TLV byte array: 1-byte tag, 2-byte length (LSB/MSB), N-byte UTF-8 data.</returns>
        public byte[] BuildData()
        {
            var valueBytes = Encoding.UTF8.GetBytes(Value);
            var length = (ushort)valueBytes.Length;
            var result = new byte[3 + length];

            result[0] = TagByte;
            result[1] = (byte)(length & 0xFF);
            result[2] = (byte)((length >> 8) & 0xFF);
            Array.Copy(valueBytes, 0, result, 3, length);

            return result;
        }

        /// <summary>
        /// Parses a TLV entry from the given data span.
        /// </summary>
        /// <param name="data">The data span containing the TLV entry.</param>
        /// <param name="bytesConsumed">The number of bytes consumed from the span.</param>
        /// <returns>The parsed ExtendedIdEntry, or null if there is insufficient data.</returns>
        public static ExtendedIdEntry ParseData(ReadOnlySpan<byte> data, out int bytesConsumed)
        {
            bytesConsumed = 0;

            if (data.Length < 3)
            {
                return null;
            }

            byte tag = data[0];
            ushort length = Message.ConvertBytesToUnsignedShort(data.Slice(1, 2));

            if (data.Length < 3 + length)
            {
                return null;
            }

            string value = Encoding.UTF8.GetString(data.Slice(3, length).ToArray());
            bytesConsumed = 3 + length;

            return new ExtendedIdEntry(tag, value);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Tag}: {Value}";
        }
    }
}
