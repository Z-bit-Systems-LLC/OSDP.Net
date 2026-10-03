using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// An Echo Request (Command ID 0x01) from the Line Quality Test Procedure, section 3.5. The
    /// responder echoes the test data back so the controller can count what survived the wire.
    /// </summary>
    public class EchoRequest : CommandData
    {
        /// <summary>
        /// Initializes an Echo Request carrying freshly generated pattern data.
        /// </summary>
        /// <param name="sequenceNumber">Test sequence number, rolling 0x00 to 0xFF. This is
        /// unrelated to the OSDP control byte sequence.</param>
        /// <param name="pattern">The pattern to generate.</param>
        /// <param name="payloadLength">Length of test data, 0 to
        /// <see cref="LineQualityProtocol.MaxPayloadLength"/>.</param>
        public EchoRequest(byte sequenceNumber, TestPattern pattern, int payloadLength)
            : this(sequenceNumber, pattern, LineQualityProtocol.GeneratePattern(pattern, payloadLength),
                payloadLength)
        {
        }

        private EchoRequest(byte sequenceNumber, TestPattern pattern, byte[] data, int declaredPayloadLength)
        {
            SequenceNumber = sequenceNumber;
            Pattern = pattern;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            DeclaredPayloadLength = declaredPayloadLength;
        }

        /// <summary>Gets the test sequence number, echoed back by the responder.</summary>
        public byte SequenceNumber { get; }

        /// <summary>Gets the requested test pattern.</summary>
        public TestPattern Pattern { get; }

        /// <summary>Gets the test data carried by the request.</summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets the payload length declared in the header, which may differ from the number of
        /// bytes actually present when a frame has been truncated.
        /// </summary>
        public int DeclaredPayloadLength { get; }

        /// <summary>
        /// Gets a value indicating whether the declared payload length matches the bytes present.
        /// </summary>
        public bool IsLengthConsistent => DeclaredPayloadLength == Data.Length;

        /// <inheritdoc />
        public override CommandType CommandType => CommandType.ManufacturerSpecific;

        /// <inheritdoc />
        public override byte Code => (byte)CommandType;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new byte[LineQualityProtocol.EchoHeaderLength + Data.Length];
            LineQualityProtocol.VendorCodeBytes.CopyTo(data, 0);
            data[3] = LineQualityProtocol.EchoCommandId;
            data[4] = SequenceNumber;
            data[5] = (byte)Pattern;
            data[6] = (byte)DeclaredPayloadLength;
            Data.CopyTo(data, LineQualityProtocol.EchoHeaderLength);
            return data;
        }

        /// <summary>
        /// Attempts to parse an Echo Request from an osdp_MFG payload.
        /// </summary>
        /// <param name="payload">The message payload, starting at the vendor code.</param>
        /// <param name="request">On success, the parsed request.</param>
        /// <returns><c>true</c> when the payload is a structurally valid line quality echo request.</returns>
        /// <remarks>
        /// A declared length that exceeds the bytes present, or that exceeds
        /// <see cref="LineQualityProtocol.MaxPayloadLength"/>, still parses. The caller decides
        /// whether that warrants a status code or a NAK, which it can determine from
        /// <see cref="IsLengthConsistent"/> and <see cref="DeclaredPayloadLength"/>.
        /// </remarks>
        public static bool TryParse(ReadOnlySpan<byte> payload, out EchoRequest request)
        {
            request = null;

            if (payload.Length < LineQualityProtocol.EchoHeaderLength) return false;
            if (!LineQualityProtocol.IsLineQualityVendorCode(payload)) return false;
            if (payload[3] != LineQualityProtocol.EchoCommandId) return false;

            int available = payload.Length - LineQualityProtocol.EchoHeaderLength;
            int declared = payload[6];
            var data = payload.Slice(LineQualityProtocol.EchoHeaderLength,
                Math.Min(declared, available)).ToArray();

            request = new EchoRequest(payload[4], (TestPattern)payload[5], data, declared);
            return true;
        }
    }
}
