using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// An Echo Response (Reply ID 0x01) from the Line Quality Test Procedure, section 3.7.
    /// </summary>
    /// <remarks>
    /// The responder echoes the bytes it received rather than regenerating them from the pattern.
    /// Regenerating would repair a corrupted payload on the way back and hide exactly the bit
    /// errors the test exists to find.
    /// </remarks>
    public class EchoResponse : PayloadData
    {
        private static readonly byte[] NoData = new byte[0];

        /// <summary>
        /// Initializes a successful Echo Response carrying echoed test data.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number from the request.</param>
        /// <param name="data">The test data exactly as received.</param>
        public EchoResponse(byte sequenceNumber, byte[] data)
            : this(sequenceNumber, EchoStatus.Success, data ?? throw new ArgumentNullException(nameof(data)))
        {
        }

        /// <summary>
        /// Initializes a failed Echo Response. A non-zero status carries no test data.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number from the request.</param>
        /// <param name="status">The failure status.</param>
        public EchoResponse(byte sequenceNumber, EchoStatus status)
            : this(sequenceNumber, status, NoData)
        {
        }

        private EchoResponse(byte sequenceNumber, EchoStatus status, byte[] data)
        {
            SequenceNumber = sequenceNumber;
            Status = status;
            Data = status == EchoStatus.Success ? data : NoData;
        }

        /// <summary>Gets the echoed test sequence number.</summary>
        public byte SequenceNumber { get; }

        /// <summary>Gets the result status.</summary>
        public EchoStatus Status { get; }

        /// <summary>Gets the echoed test data. Always empty when <see cref="Status"/> is non-zero.</summary>
        public byte[] Data { get; }

        /// <inheritdoc />
        public override byte Code => (byte)ReplyType.ManufactureSpecific;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.ReplyMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new byte[LineQualityProtocol.EchoHeaderLength + Data.Length];
            LineQualityProtocol.VendorCodeBytes.CopyTo(data, 0);
            data[3] = LineQualityProtocol.EchoCommandId;
            data[4] = SequenceNumber;
            data[5] = (byte)Status;
            data[6] = (byte)Data.Length;
            Data.CopyTo(data, LineQualityProtocol.EchoHeaderLength);
            return data;
        }

        /// <summary>
        /// Attempts to parse an Echo Response from an osdp_MFGREP payload.
        /// </summary>
        /// <param name="payload">The reply payload, starting at the vendor code.</param>
        /// <param name="response">On success, the parsed response.</param>
        /// <returns><c>true</c> when the payload is a well-formed line quality echo response.</returns>
        public static bool TryParse(ReadOnlySpan<byte> payload, out EchoResponse response)
        {
            response = null;

            if (payload.Length < LineQualityProtocol.EchoHeaderLength) return false;
            if (!LineQualityProtocol.IsLineQualityVendorCode(payload)) return false;
            if (payload[3] != LineQualityProtocol.EchoCommandId) return false;

            int available = payload.Length - LineQualityProtocol.EchoHeaderLength;
            int declared = payload[6];
            if (declared != available) return false;

            var status = (EchoStatus)payload[5];
            var data = payload.Slice(LineQualityProtocol.EchoHeaderLength, available).ToArray();

            response = new EchoResponse(payload[4], status, data);
            return true;
        }
    }
}
