using System;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Command data to send a request to the PD to perform a biometric scan and match.
    /// </summary>
    public class BiometricReadData : CommandData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BiometricReadData"/> class.
        /// </summary>
        /// <param name="readerNumber">The reader number starting at 0.</param>
        /// <param name="biometricType">body part to scan.</param>
        /// <param name="biometricFormatType">The format of the attached template.</param>
        /// <param name="quality"></param>
        public BiometricReadData(byte readerNumber, BiometricType biometricType, BiometricFormat biometricFormatType,
            byte quality)
        {
            ReaderNumber = readerNumber;
            BiometricType = biometricType;
            BiometricFormatType = biometricFormatType;
            Quality = quality;
        }

        /// <summary>
        /// Gets the reader number starting at 0.
        /// </summary>
        public byte ReaderNumber { get; }

        /// <summary>
        /// Gets the type/body part to scan.
        /// </summary>
        public BiometricType BiometricType { get; }

        /// <summary>
        /// Gets the format of the attached template.
        /// </summary>
        public BiometricFormat BiometricFormatType { get; }

        /// <summary>
        /// Gets the ?.
        /// </summary>
        public byte Quality { get; }
        
        /// <inheritdoc />
        public override CommandType CommandType => CommandType.BioRead;

        /// <inheritdoc />
        public override byte Code => (byte)CommandType;
        
        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            return new []
            {
                ReaderNumber,
                (byte)BiometricType,
                (byte)BiometricFormatType,
                Quality
            };
        }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of BiometricReadData representing the message payload</returns>
        public static BiometricReadData ParseData(ReadOnlySpan<byte> data)
        {
            return new BiometricReadData(
                data[0], 
                (BiometricType)data[1], 
                (BiometricFormat)data[2], 
                data[3]);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($" Reader #: {ReaderNumber}");
            sb.AppendLine($"Bio Type: {BiometricType}");
            sb.AppendLine($"  Format: {BiometricFormatType}");
            sb.AppendLine($" Quality: {Quality}");
            return sb.ToString();
        }
    }
}