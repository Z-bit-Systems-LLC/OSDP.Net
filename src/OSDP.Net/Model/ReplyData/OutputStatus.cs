using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using System;
using System.Linq;
using System.Text;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// A output status report reply.
    /// </summary>
    public class OutputStatus : PayloadData
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="OutputStatus"/> class from being created.
        /// </summary>
        public OutputStatus(bool[] statuses)
        {
            OutputStatuses = statuses;
        }

        /// <inheritdoc />
        public override byte Code => (byte)ReplyType.OutputStatusReport;

        /// <summary>
        /// Gets the all the PDs output statuses as an array ordered by output number.
        /// </summary>
        public bool[] OutputStatuses { get; }

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.ReplyMessageWithDataSecurity;

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of OutputStatus representing the message payload</returns>
        public static OutputStatus ParseData(ReadOnlySpan<byte> data)
        {
            return new OutputStatus(data.ToArray().Select(Convert.ToBoolean).ToArray());
        }

        /// <inheritdoc />
        public override byte[] BuildData() => OutputStatuses.Select(x => x ? (byte)0x00 : (byte)0x01).ToArray();

        /// <inheritdoc />
        public override string ToString()
        {
            byte outputNumber = 0;
            var build = new StringBuilder();
            foreach (bool outputStatus in OutputStatuses)
            {
                build.AppendLine($"Output Number {outputNumber++:00}: {outputStatus}");
            }
            return build.ToString();
        }
    }
}