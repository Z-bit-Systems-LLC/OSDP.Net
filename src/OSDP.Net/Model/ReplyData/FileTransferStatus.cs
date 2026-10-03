using System;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// The PD file transfer status sent as a reply.
    /// </summary>
    /// <remarks>
    /// Sent by a PD as osdp_FTSTAT (0x7A) in response to an
    /// <see cref="CommandData.FileTransferFragment"/>. See OSDP v2.2.2 subclause 7.25.
    /// An ACU parses an incoming reply with <see cref="ParseData"/>; a PD builds an outgoing
    /// reply with the public constructor.
    /// </remarks>
    public class FileTransferStatus : PayloadData
    {
        /// <summary>
        /// Number of bytes in an osdp_FTSTAT data block.
        /// </summary>
        private const int DataLength = 7;

        /// <summary>
        /// Control Flags
        /// </summary>
        [Flags]
        public enum ControlFlags
        {
            /// <summary>
            /// No control flags set; the PD is dedicated to the file transfer, stays in the secure
            /// channel, and has no separate poll response available.
            /// </summary>
            None = 0x0,

            /// <summary>
            /// The ACU may interleave other messages with the file transfer.
            /// </summary>
            Interleave = 0x1,

            /// <summary>
            /// The ACU shall leave the secure channel for the duration of the file transfer.
            /// </summary>
            LeaveSecureChannel = 0x2,

            /// <summary>
            /// A separate poll response is available.
            /// </summary>
            PollResponseAvailable = 0x4
        }

        /// <summary>
        /// Status Detail
        /// </summary>
        public enum StatusDetail
        {
            /// <summary>
            /// The error is unknown
            /// </summary>
            UnknownError = -4,
            /// <summary>
            /// The file data unacceptable (malformed).
            /// </summary>
            FileDataUnacceptable = -3,
            /// <summary>
            /// Unrecognized file contents.
            /// </summary>
            UnrecognizedFileContents = -2,
            /// <summary>
            /// Abort file transfer.
            /// </summary>
            AbortFileTransfer = -1,
            /// <summary>
            /// OK to proceed.
            /// </summary>
            OkToProceed = 0,
            /// <summary>
            /// The File contents processed.
            /// </summary>
            FileContentsProcessed = 1,
            /// <summary>
            /// Rebooting now, expect full communications reset.
            /// </summary>
            RebootingNow = 2,
            /// <summary>
            /// PD is finishing file transfer.
            /// </summary>
            FinishingFileTransfer = 3,
            /// <summary>
            /// The status is unknown
            /// </summary>
            UnknownStatus = 4
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="FileTransferStatus"/> class from being created.
        /// </summary>
        private FileTransferStatus()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileTransferStatus"/> class to be sent by a PD
        /// in reply to an osdp_FILETRANSFER command.
        /// </summary>
        /// <param name="detail">The status of the file transfer.</param>
        /// <param name="action">Control flags requesting a change in the ACU's communication procedure.</param>
        /// <param name="requestedDelay">
        /// Delay in milliseconds the ACU should wait before sending the next osdp_FILETRANSFER command,
        /// or zero for no delay.
        /// </param>
        /// <param name="updateMessageMaximum">
        /// Alternate maximum fragment size the ACU should use for the remainder of the transfer,
        /// or zero to request no change.
        /// </param>
        public FileTransferStatus(StatusDetail detail, ControlFlags action = ControlFlags.None,
            ushort requestedDelay = 0, ushort updateMessageMaximum = 0)
        {
            Detail = detail;
            Action = action;
            RequestedDelay = requestedDelay;
            UpdateMessageMaximum = updateMessageMaximum;
        }

        /// <summary>Gets the control flags.</summary>
        public ControlFlags Action { get; private set; }

        /// <summary>Gets the request ACU time delay in milliseconds before next osdp_FILETRANSFER command.</summary>
        public ushort RequestedDelay { get;private set;  }

        /// <summary>Gets the file transfer status.</summary>
        public StatusDetail Detail { get; private set; }

        /// <summary>Gets the alternative maximum message size.</summary>
        public ushort UpdateMessageMaximum { get; private set; }

        /// <inheritdoc />
        public override byte Code => (byte)ReplyType.FileTransferStatus;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.ReplyMessageWithDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            var data = new byte[DataLength];
            data[0] = (byte)Action;

            var delay = Message.ConvertShortToBytes(RequestedDelay);
            data[1] = delay[0];
            data[2] = delay[1];

            // StatusDetail travels as a signed little-endian short, so the enum value is reinterpreted
            // rather than clamped; negative values are the PD's error codes.
            var detail = Message.ConvertShortToBytes(unchecked((ushort)(short)Detail));
            data[3] = detail[0];
            data[4] = detail[1];

            var messageMaximum = Message.ConvertShortToBytes(UpdateMessageMaximum);
            data[5] = messageMaximum[0];
            data[6] = messageMaximum[1];

            return data;
        }

        /// <summary>Parses the message payload bytes</summary>
        /// <param name="data">Message payload as bytes</param>
        /// <returns>An instance of FileTransferStatus representing the message payload</returns>
        internal static FileTransferStatus ParseData(ReadOnlySpan<byte> data)
        {
            var dataArray = data.ToArray();
            if (dataArray.Length != DataLength)
            {
                throw new Exception("Invalid size for the data");
            }

            return new FileTransferStatus
            {
                Action = (ControlFlags)dataArray[0],
                RequestedDelay = Message.ConvertBytesToUnsignedShort(dataArray.Skip(1).Take(2).ToArray(), true),
                Detail = SetStatusDetailDefault(Message.ConvertBytesToShort(dataArray.Skip(3).Take(2).ToArray(), true)),
                UpdateMessageMaximum = Message.ConvertBytesToUnsignedShort(dataArray.Skip(5).Take(2).ToArray(), true)
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var build = new StringBuilder();
            build.AppendLine($"             Action: {Action:G}");
            build.AppendLine($"    Requested Delay: {RequestedDelay}");
            build.AppendLine($"      Status Detail: {Detail}");
            build.AppendLine($" Update Message Max: {UpdateMessageMaximum}");

            return build.ToString();
        }

        private static StatusDetail SetStatusDetailDefault(short value)
        {
            return value switch
            {
                < -3 => StatusDetail.UnknownError,
                > 3 => StatusDetail.UnknownStatus,
                _ => (StatusDetail)value
            };
        }
    }
}
