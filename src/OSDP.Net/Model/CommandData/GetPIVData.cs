using System;
using System.Collections.Generic;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData
{
    /// <summary>
    /// Command data to get the PIV data
    /// </summary>
    public class GetPIVData : CommandData
    {
        private bool _useSingleByteOffset;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetPIVData"/> class.
        /// </summary>
        /// <param name="objectId">The object identifier.</param>
        /// <param name="elementId">The element identifier.</param>
        /// <param name="dataOffset">The data offset.</param>
        [Obsolete("Single byte offset no longer supported with future versions of OSDP")]
        public GetPIVData(ObjectId objectId, byte elementId, byte dataOffset)
        {
            ObjectId = objectId switch
            {
                Model.CommandData.ObjectId.CardholderUniqueIdentifier => [0x5F, 0xC1, 0x02],
                Model.CommandData.ObjectId.CertificateForPIVAuthentication => [0x5F, 0xC1, 0x05],
                Model.CommandData.ObjectId.CertificateForCardAuthentication => [0xDF, 0xC1, 0x01],
                Model.CommandData.ObjectId.CardholderFingerprintTemplate => [0xDF, 0xC1, 0x03],
                _ => throw new ArgumentOutOfRangeException()
            };

            ElementId = elementId;
            DataOffset = dataOffset;

            _useSingleByteOffset = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetPIVData"/> class.
        /// </summary>
        /// <param name="objectId">The object identifier with a length of 3.</param>
        /// <param name="elementId">The element identifier.</param>
        /// <param name="dataOffset">The data offset.</param>
        public GetPIVData(byte[] objectId, byte elementId, ushort dataOffset)
        {
            if (objectId.Length != 3)
            {
                throw new ArgumentException("Object ID byte length must be 3", nameof(objectId));
            }
            
            ObjectId = objectId;
            ElementId = elementId;
            DataOffset = dataOffset;
        }

        /// <summary>
        /// Gets the object identifier.
        /// </summary>
        public byte[] ObjectId { get; }

        /// <summary>
        /// Gets the element identifier.
        /// </summary>
        public byte ElementId { get; }

        /// <summary>
        /// Gets the data offset.
        /// </summary>
        public ushort DataOffset { get; }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of GetPIVData representing the message payload</returns>
        public static GetPIVData ParseData(ReadOnlySpan<byte> data)
        {
            if (data.Length < 5 || data.Length > 6)
            {
                throw new ArgumentException("Invalid data length, must either be 5 or 6 bytes", nameof(data));
            }

            var isSingleByteOffset = data.Length == 5;
            ushort offset = isSingleByteOffset ? data[4] :
                Message.ConvertBytesToUnsignedShort(data.Slice(4, 2));

            return new GetPIVData(data.Slice(0, 3).ToArray(), data[3], offset)
            {
                _useSingleByteOffset = isSingleByteOffset
            };
        }
        
        /// <inheritdoc />
        public override CommandType CommandType => CommandType.PivData;

        /// <inheritdoc />
        public override byte Code => (byte)CommandType;
        
        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new List<byte>();
            data.AddRange(ObjectId);
            data.Add(ElementId);
            
            if (_useSingleByteOffset) data.Add(Message.ConvertShortToBytes(DataOffset)[0]);
            else data.AddRange(Message.ConvertShortToBytes(DataOffset));
            
            return data.ToArray();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"  Object ID: {BitConverter.ToString(ObjectId)}");
            sb.AppendLine($" Element ID: {ElementId}");
            sb.AppendLine($"Data Offset: {DataOffset}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Enum ObjectId
    /// </summary>
    public enum ObjectId
    {
        /// <summary>
        /// The cardholder unique identifier
        /// </summary>
        CardholderUniqueIdentifier,
        /// <summary>
        /// The certificate for piv authentication
        /// </summary>
        CertificateForPIVAuthentication,
        /// <summary>
        /// The certificate for card authentication
        /// </summary>
        CertificateForCardAuthentication,
        /// <summary>
        /// The cardholder fingerprint template
        /// </summary>
        CardholderFingerprintTemplate
    }
}