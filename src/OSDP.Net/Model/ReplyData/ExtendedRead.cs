using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// An extended read (transparent mode) reply.
    /// </summary>
    public class ExtendedRead
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="ExtendedRead"/> class from being created.
        /// </summary>
        private ExtendedRead()
        {
        }

        /// <summary>
        /// Gets the extended READ/WRITE mode.
        /// </summary>
        public byte Mode { get; private set; }

        /// <summary>
        /// Gets the mode-specific reply code.
        /// </summary>
        public byte PReply { get; private set; }

        /// <summary>
        /// Gets the reply data.
        /// </summary>
        public IEnumerable<byte> PData { get; private set; }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of ExtendedRead representing the message payload</returns>
        /// <remarks>
        /// Leniently handles short payloads: the smart-card flow legitimately produces
        /// replies shorter than two bytes during PD state transitions (e.g., while
        /// scanning for a card), so missing fields default to zero/empty rather than
        /// throwing.
        /// </remarks>
        public static ExtendedRead ParseData(ReadOnlySpan<byte> data)
        {
            var dataArray = data.ToArray();
            return new ExtendedRead
            {
                Mode = dataArray.Length > 0 ? dataArray[0] : (byte)0,
                PReply = dataArray.Length > 1 ? dataArray[1] : (byte)0,
                PData = dataArray.Length > 2 ? dataArray.Skip(2).ToArray() : []
            };
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"  Mode: {Mode}");
            sb.AppendLine($"PReply: {PReply}");
            sb.AppendLine($" PData: {BitConverter.ToString(PData.ToArray())}");
            return sb.ToString();
        }
    }
}
