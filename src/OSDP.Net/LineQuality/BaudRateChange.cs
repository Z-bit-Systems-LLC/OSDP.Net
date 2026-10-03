using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// A Baud Rate Change command (Command ID 0x02) from the Line Quality Test Procedure,
    /// section 3.5. The responder acknowledges at the current rate and both ends then switch.
    /// </summary>
    public class BaudRateChange : CommandData
    {
        /// <summary>Number of payload bytes in a baud rate change command.</summary>
        public const int PayloadLength = 6;

        /// <summary>
        /// Initializes a Baud Rate Change command.
        /// </summary>
        /// <param name="sequenceNumber">Test sequence number, echoed back by the responder.</param>
        /// <param name="baudRateId">The rate to switch to.</param>
        public BaudRateChange(byte sequenceNumber, LineQualityBaudRate baudRateId)
        {
            SequenceNumber = sequenceNumber;
            BaudRateId = baudRateId;
        }

        /// <summary>Gets the test sequence number.</summary>
        public byte SequenceNumber { get; }

        /// <summary>Gets the requested baud rate identifier.</summary>
        public LineQualityBaudRate BaudRateId { get; }

        /// <inheritdoc />
        public override CommandType CommandType => CommandType.ManufacturerSpecific;

        /// <inheritdoc />
        public override byte Code => (byte)CommandType;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new byte[PayloadLength];
            LineQualityProtocol.VendorCodeBytes.CopyTo(data, 0);
            data[3] = LineQualityProtocol.BaudRateChangeCommandId;
            data[4] = SequenceNumber;
            data[5] = (byte)BaudRateId;
            return data;
        }

        /// <summary>
        /// Attempts to parse a Baud Rate Change command from an osdp_MFG payload.
        /// </summary>
        /// <param name="payload">The message payload, starting at the vendor code.</param>
        /// <param name="command">On success, the parsed command.</param>
        /// <returns><c>true</c> when the payload is a line quality baud rate change command.</returns>
        public static bool TryParse(ReadOnlySpan<byte> payload, out BaudRateChange command)
        {
            command = null;

            if (payload.Length < PayloadLength) return false;
            if (!LineQualityProtocol.IsLineQualityVendorCode(payload)) return false;
            if (payload[3] != LineQualityProtocol.BaudRateChangeCommandId) return false;

            command = new BaudRateChange(payload[4], (LineQualityBaudRate)payload[5]);
            return true;
        }
    }
}
