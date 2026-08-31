using System;

namespace PDConsole.FileTransfer
{
    /// <summary>
    /// Reports the state of an in-progress or completed osdp_FILETRANSFER to the console.
    /// </summary>
    public class FileTransferEvent : EventArgs
    {
        /// <summary>
        /// Gets the osdp_FILETRANSFER type declared by the ACU. See OSDP v2.2.2 Table 34.
        /// </summary>
        public byte FileType { get; init; }

        /// <summary>
        /// Gets the total size of the file being transferred, in bytes.
        /// </summary>
        public int TotalSize { get; init; }

        /// <summary>
        /// Gets the number of contiguous bytes received so far.
        /// </summary>
        public int ReceivedSize { get; init; }

        /// <summary>
        /// Gets a value indicating whether the whole file has been received.
        /// </summary>
        public bool IsComplete { get; init; }

        /// <summary>
        /// Gets the full path the completed file was written to, or null when the transfer is still
        /// in progress or failed.
        /// </summary>
        public string SavedFilePath { get; init; }

        /// <summary>
        /// Gets the reason the transfer was rejected, or null when the fragment was accepted.
        /// </summary>
        public string Error { get; init; }

        /// <summary>
        /// Gets the portion of the file received so far, as a percentage.
        /// </summary>
        public double PercentComplete => TotalSize > 0 ? ReceivedSize * 100.0 / TotalSize : 0;

        /// <inheritdoc />
        public override string ToString()
        {
            if (Error != null)
            {
                return $"Type 0x{FileType:X2} - failed at {ReceivedSize:N0} of {TotalSize:N0} bytes: {Error}";
            }

            return IsComplete
                ? $"Type 0x{FileType:X2} - {TotalSize:N0} bytes received, saved to {SavedFilePath}"
                : $"Type 0x{FileType:X2} - {ReceivedSize:N0} of {TotalSize:N0} bytes ({PercentComplete:F1}%)";
        }
    }
}
