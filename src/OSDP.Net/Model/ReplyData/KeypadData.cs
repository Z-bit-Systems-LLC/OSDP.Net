using System;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// A keypad reply.
    /// </summary>
    public class KeypadData : PayloadData
    {
        private const int DataStartIndex = 2;

        /// <summary>
        /// Creates a new instance of KeypadData.
        /// </summary>
        /// <param name="readerNumber">Reader number (0=First Reader, 1=Second Reader)</param>
        /// <param name="data">Data returned from the keypad as a string</param>
        public KeypadData(byte readerNumber, string data)
        {
            ReaderNumber = readerNumber;
            Data = ConvertKeypadStringToBytes(data);
            DigitCount = (ushort)Data.Length;
        }

        /// <summary>
        /// Reader number 0=First Reader 1=Second Reader
        /// </summary>
        public byte ReaderNumber { get; private set; }

        /// <summary>
        /// Number of digits in the return data
        /// </summary>
        public ushort DigitCount { get; private set; }

        /// <summary>
        /// <para>Data returned from keypad</para>
        /// <para>The key encoding uses the following data representation:</para>
        /// <list type="bullet">
        ///     <item>
        ///         <description>Digits 0 through 9 are reported as ASCII characters 0x30 through 0x39</description>
        ///     </item>
        ///     <item>
        ///         <description>The clear/delete/'*' key is reported as ASCII DELETE, 0x7F</description>
        ///     </item>
        ///     <item>
        ///         <description>The enter/'#' key is reported as ASCII return, 0x0D</description>
        ///     </item>
        /// </list>
        /// <para>Special/function keys are reported as upper case ASCII:</para>
        /// <list type="bullet">
        ///     <item>
        ///         <description>A or F1 = 0x41</description>
        ///     </item>
        ///     <item>
        ///         <description>B or F2 = 0x42</description>
        ///     </item>
        ///     <item>
        ///         <description>C or F3 = 0x43</description>
        ///     </item>
        ///     <item>
        ///         <description>D or F4 = 0x44</description>
        ///     </item>
        ///     <item>
        ///         <description>F1 and F2 = 0x45</description>
        ///     </item>
        ///     <item>
        ///         <description>F2 and F3 = 0x46</description>
        ///     </item>
        ///     <item>
        ///         <description>F3 and F4 = 0x47</description>
        ///     </item>
        ///     <item>
        ///         <description>F1 and F4 = 0x48</description>
        ///     </item>
        /// </list>
        /// </summary>
        public byte[] Data { get; private set; }

        /// <inheritdoc/>
        public override byte Code => (byte)ReplyType.KeypadData;

        /// <inheritdoc/>
        public override ReadOnlySpan<byte> SecurityControlBlock()
        {
            return SecurityBlock.ReplyMessageWithDataSecurity;
        }

        /// <inheritdoc/>
        public override byte[] BuildData()
        {
            var length = 2 + Data.Length;
            var buffer = new byte[length];
            buffer[0] = ReaderNumber;
            buffer[1] = (byte)DigitCount;
            Data.CopyTo(buffer, 2);

            return buffer;
        }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of KeypadData representing the message payload</returns>
        public static KeypadData ParseData(ReadOnlySpan<byte> data)
        {
            if (data.Length < DataStartIndex)
            {
                throw new Exception("Invalid size for the data");
            }

            var dataBytes = data.Slice(DataStartIndex, data.Length - DataStartIndex).ToArray();

            // Convert encoded bytes to human-readable string
            var keypadString = ConvertBytesToKeypadString(dataBytes);

            return new KeypadData(data[0], keypadString);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var build = new StringBuilder();
            build.AppendLine($"Reader Number: {ReaderNumber}");
            build.AppendLine($"  Digit Count: {DigitCount}");
            build.AppendLine($"         Data: {ConvertBytesToKeypadString(Data)}");
            return build.ToString();
        }

        private static byte[] ConvertKeypadStringToBytes(string data)
        {
            var bytes = new byte[data.Length];
            for (int index = 0; index < data.Length; index++)
            {
                bytes[index] = data[index] switch
                {
                    '#' => 0x0D, // Enter/'#' key as ASCII return
                    '*' => 0x7F, // Clear/delete/'*' key as ASCII DELETE
                    _ => (byte)data[index] // All other characters as-is
                };
            }
            return bytes;
        }

        private static string ConvertBytesToKeypadString(byte[] data)
        {
            var build = new StringBuilder();
            foreach (var digit in data)
            {
                switch (digit)
                {
                    case 0x0D:
                        build.Append('#');
                        break;
                    case 0x7F:
                        build.Append('*');
                        break;
                    default:
                        build.Append(Convert.ToChar(digit));
                        break;
                }
            }
            return build.ToString();
        }
    }
}
