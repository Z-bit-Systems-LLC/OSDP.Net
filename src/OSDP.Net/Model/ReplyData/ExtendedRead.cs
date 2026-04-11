using System;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// An extended read (transparent mode) reply.
    /// </summary>
    /// <remarks>
    /// Transparent mode uses paired XWR/XRD messages to tunnel ISO 7816-4 smart-card APDUs
    /// across the OSDP link. This class is the PD-side reply payload and can be both built
    /// (PD sending) and parsed (ACU receiving).
    /// </remarks>
    public class ExtendedRead : PayloadData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExtendedRead"/> class.
        /// </summary>
        /// <param name="mode">Extended READ/WRITE mode (0 = configuration, 1 = transparent APDU).</param>
        /// <param name="pReply">Mode-specific reply code.</param>
        /// <param name="pData">Reply data.</param>
        /// <exception cref="ArgumentNullException">pData</exception>
        public ExtendedRead(byte mode, byte pReply, byte[] pData)
        {
            Mode = mode;
            PReply = pReply;
            PData = pData ?? throw new ArgumentNullException(nameof(pData));
        }

        /// <summary>
        /// Gets the extended READ/WRITE mode.
        /// </summary>
        public byte Mode { get; }

        /// <summary>
        /// Gets the mode-specific reply code.
        /// </summary>
        public byte PReply { get; }

        /// <summary>
        /// Gets the reply data.
        /// </summary>
        public byte[] PData { get; }

        /// <inheritdoc />
        public override byte Code => (byte)ReplyType.ExtendedRead;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.ReplyMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var buffer = new byte[2 + PData.Length];
            buffer[0] = Mode;
            buffer[1] = PReply;
            Array.Copy(PData, 0, buffer, 2, PData.Length);
            return buffer;
        }

        /// <summary>Parses the message payload bytes.</summary>
        /// <param name="data">Message payload as bytes.</param>
        /// <returns>An instance of ExtendedRead representing the message payload.</returns>
        /// <remarks>
        /// Leniently handles short payloads: the smart-card flow legitimately produces
        /// replies shorter than two bytes during PD state transitions (e.g., while
        /// scanning for a card), so missing fields default to zero/empty rather than
        /// throwing.
        /// </remarks>
        public static ExtendedRead ParseData(ReadOnlySpan<byte> data)
        {
            byte mode = data.Length > 0 ? data[0] : (byte)0;
            byte pReply = data.Length > 1 ? data[1] : (byte)0;
            byte[] pData = data.Length > 2 ? data.Slice(2).ToArray() : [];
            return new ExtendedRead(mode, pReply, pData);
        }

        /// <summary>
        /// Builds a Mode-0 setting report reply (response to <see cref="CommandData.ExtendedWrite.ReadModeSetting"/>).
        /// </summary>
        /// <param name="currentMode">The PD's currently configured transparent mode (0 or 1).</param>
        /// <param name="enabled">Whether transparent mode is enabled.</param>
        public static ExtendedRead ModeZeroSettingReport(byte currentMode, bool enabled) =>
            new(0, 1, [currentMode, (byte)(enabled ? 1 : 0)]);

        /// <summary>
        /// Builds an unsolicited card-present notification on the specified reader.
        /// </summary>
        /// <param name="readerNumber">The reader number on the PD.</param>
        public static ExtendedRead CardPresent(byte readerNumber) =>
            new(1, 1, [readerNumber]);

        /// <summary>
        /// Builds a Mode-1 APDU response reply.
        /// </summary>
        /// <param name="readerNumber">The reader number on the PD.</param>
        /// <param name="response">The APDU response bytes returned from the smart card.</param>
        /// <exception cref="ArgumentNullException">response</exception>
        public static ExtendedRead ApduResponse(byte readerNumber, byte[] response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            var data = new byte[response.Length + 1];
            data[0] = readerNumber;
            Array.Copy(response, 0, data, 1, response.Length);
            return new ExtendedRead(1, 1, data);
        }

        /// <summary>
        /// Builds a Mode-1 smart-card session-terminated reply.
        /// </summary>
        /// <param name="readerNumber">The reader number on the PD.</param>
        public static ExtendedRead SessionTerminated(byte readerNumber) =>
            new(1, 2, [readerNumber]);

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"  Mode: {Mode}");
            sb.AppendLine($"PReply: {PReply}");
            sb.AppendLine($" PData: {BitConverter.ToString(PData)}");
            return sb.ToString();
        }
    }
}
