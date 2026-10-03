using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;

namespace OSDP.Net.LineQuality
{
    /// <summary>
    /// A Baud Rate Change Acknowledgment (Reply ID 0x02) from the Line Quality Test Procedure,
    /// section 3.7. Sent at the current rate, immediately before the responder switches.
    /// </summary>
    public class BaudRateChangeAck : PayloadData
    {
        /// <summary>Number of payload bytes in a baud rate change acknowledgment.</summary>
        public const int PayloadLength = 6;

        /// <summary>
        /// Initializes a Baud Rate Change Acknowledgment.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number from the command.</param>
        /// <param name="status">The result status.</param>
        public BaudRateChangeAck(byte sequenceNumber, BaudRateChangeStatus status)
        {
            SequenceNumber = sequenceNumber;
            Status = status;
        }

        /// <summary>Gets the echoed test sequence number.</summary>
        public byte SequenceNumber { get; }

        /// <summary>Gets the result status.</summary>
        public BaudRateChangeStatus Status { get; }

        /// <inheritdoc />
        public override byte Code => (byte)ReplyType.ManufactureSpecific;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.ReplyMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new byte[PayloadLength];
            LineQualityProtocol.VendorCodeBytes.CopyTo(data, 0);
            data[3] = LineQualityProtocol.BaudRateChangeCommandId;
            data[4] = SequenceNumber;
            data[5] = (byte)Status;
            return data;
        }

        /// <summary>
        /// Attempts to parse a Baud Rate Change Acknowledgment from an osdp_MFGREP payload.
        /// </summary>
        /// <param name="payload">The reply payload, starting at the vendor code.</param>
        /// <param name="ack">On success, the parsed acknowledgment.</param>
        /// <returns><c>true</c> when the payload is a line quality baud rate change acknowledgment.</returns>
        public static bool TryParse(ReadOnlySpan<byte> payload, out BaudRateChangeAck ack)
        {
            ack = null;

            if (payload.Length < PayloadLength) return false;
            if (!LineQualityProtocol.IsLineQualityVendorCode(payload)) return false;
            if (payload[3] != LineQualityProtocol.BaudRateChangeCommandId) return false;

            ack = new BaudRateChangeAck(payload[4], (BaudRateChangeStatus)payload[5]);
            return true;
        }
    }
}
