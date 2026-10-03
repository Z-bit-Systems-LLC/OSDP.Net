using System;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net;

/// <summary>
/// Reassembles a file sent by an ACU through a sequence of osdp_FILETRANSFER commands and produces
/// the osdp_FTSTAT reply that each fragment requires.
/// </summary>
/// <remarks>
/// <para>
/// A PD implementation calls <see cref="AcceptFragment"/> from its
/// <c>Device.HandleFileTransfer</c> override and returns the result. The receiver validates each
/// fragment against OSDP v2.2.2 subclauses 6.26 and 7.25, copies its data into a buffer sized from
/// the total size declared by the ACU, and reports <see cref="FileTransferStatus.StatusDetail.OkToProceed"/>
/// until the final byte arrives, at which point it reports
/// <see cref="FileTransferStatus.StatusDetail.FileContentsProcessed"/> and exposes the assembled file
/// through <see cref="GetFile"/>.
/// </para>
/// <para>
/// Fragments are written at the offset the ACU declares, so a retransmitted or overlapping fragment is
/// absorbed without corrupting the file. A fragment that would leave a gap is rejected, because the
/// spec requires offsets to be monotonically increasing and the PD has no way to request a resend.
/// </para>
/// <para>
/// This class is not thread safe. A <see cref="Device"/> processes commands one at a time on a single
/// connection loop, so a receiver owned by one device needs no external locking.
/// </para>
/// </remarks>
public class FileTransferReceiver
{
    /// <summary>
    /// Largest file accepted when the caller does not specify a limit. A transfer declaring a total
    /// size beyond the limit is rejected before any buffer is allocated, so a malformed or hostile
    /// header cannot exhaust memory.
    /// </summary>
    public const int DefaultMaximumFileSize = 16 * 1024 * 1024;

    private readonly int _maximumFileSize;

    private byte[] _buffer;
    private int _highWaterMark;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransferReceiver"/> class.
    /// </summary>
    /// <param name="maximumFileSize">
    /// Largest total file size to accept, in bytes. Defaults to <see cref="DefaultMaximumFileSize"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The maximum file size is not positive.</exception>
    public FileTransferReceiver(int maximumFileSize = DefaultMaximumFileSize)
    {
        if (maximumFileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFileSize), maximumFileSize,
                "The maximum file size must be greater than zero.");
        }

        _maximumFileSize = maximumFileSize;
    }

    /// <summary>
    /// Gets a value indicating whether a transfer has been started and has not yet been reset.
    /// </summary>
    public bool IsTransferInProgress => _buffer != null;

    /// <summary>
    /// Gets a value indicating whether every byte of the current transfer has been received.
    /// </summary>
    public bool IsComplete => _buffer != null && _highWaterMark >= _buffer.Length;

    /// <summary>
    /// Gets the file transfer type declared by the ACU, or zero when no transfer is in progress.
    /// See OSDP v2.2.2 Table 34.
    /// </summary>
    public byte FileType { get; private set; }

    /// <summary>
    /// Gets the total size of the file being transferred, or zero when no transfer is in progress.
    /// </summary>
    public int TotalSize => _buffer?.Length ?? 0;

    /// <summary>
    /// Gets the number of contiguous bytes received so far.
    /// </summary>
    public int ReceivedSize => _highWaterMark;

    /// <summary>
    /// Gets or sets the control flags reported to the ACU with every status reply. See OSDP v2.2.2
    /// Table 68.
    /// </summary>
    public FileTransferStatus.ControlFlags Action { get; set; } = FileTransferStatus.ControlFlags.None;

    /// <summary>
    /// Gets or sets the delay in milliseconds the ACU is asked to wait before sending the next
    /// osdp_FILETRANSFER command. Zero requests no delay.
    /// </summary>
    public ushort RequestedDelay { get; set; }

    /// <summary>
    /// Gets or sets the alternate maximum fragment size reported to the ACU. Zero requests no change,
    /// leaving the ACU on the size it derived from osdp_PDCAP.
    /// </summary>
    public ushort UpdateMessageMaximum { get; set; }

    /// <summary>
    /// Accepts one file transfer fragment and produces the status reply to send back to the ACU.
    /// </summary>
    /// <param name="fragment">The incoming osdp_FILETRANSFER payload.</param>
    /// <returns>
    /// The osdp_FTSTAT reply for this fragment: a negative
    /// <see cref="FileTransferStatus.StatusDetail"/> aborts the transfer at the ACU,
    /// <see cref="FileTransferStatus.StatusDetail.OkToProceed"/> asks for the next fragment, and
    /// <see cref="FileTransferStatus.StatusDetail.FileContentsProcessed"/> reports the file complete.
    /// </returns>
    /// <exception cref="ArgumentNullException">The fragment is null.</exception>
    public FileTransferStatus AcceptFragment(FileTransferFragment fragment)
    {
        if (fragment == null) throw new ArgumentNullException(nameof(fragment));

        var data = fragment.Fragment;
        var fragmentData = data.DataFragment ?? [];

        // A fragment that is internally inconsistent is malformed, regardless of transfer state.
        if (data.TotalSize < 0 || data.Offset < 0 || data.TotalSize > _maximumFileSize ||
            data.Offset > data.TotalSize || data.FragmentSize > data.TotalSize - data.Offset ||
            data.FragmentSize > fragmentData.Length)
        {
            Reset();
            return Status(FileTransferStatus.StatusDetail.FileDataUnacceptable);
        }

        // Offset zero starts a transfer, which also makes an ACU retry from the beginning recover
        // cleanly rather than colliding with the abandoned buffer.
        if (data.Offset == 0)
        {
            _buffer = new byte[data.TotalSize];
            _highWaterMark = 0;
            FileType = fragment.Type;
        }
        else if (_buffer == null)
        {
            // The ACU resumed mid-file with no transfer established, so there is nothing to append to.
            return Status(FileTransferStatus.StatusDetail.FileDataUnacceptable);
        }

        // Changing the declared size or type mid-transfer would silently corrupt the assembled file.
        if (data.TotalSize != _buffer.Length || fragment.Type != FileType)
        {
            Reset();
            return Status(FileTransferStatus.StatusDetail.FileDataUnacceptable);
        }

        // A fragment starting past the contiguous region would leave a hole the PD cannot fill, since
        // osdp_FTSTAT has no way to request a specific offset.
        if (data.Offset > _highWaterMark)
        {
            Reset();
            return Status(FileTransferStatus.StatusDetail.FileDataUnacceptable);
        }

        if (data.FragmentSize > 0)
        {
            Array.Copy(fragmentData, 0, _buffer, data.Offset, data.FragmentSize);
            _highWaterMark = Math.Max(_highWaterMark, data.Offset + data.FragmentSize);
        }

        return Status(IsComplete
            ? FileTransferStatus.StatusDetail.FileContentsProcessed
            : FileTransferStatus.StatusDetail.OkToProceed);
    }

    /// <summary>
    /// Returns a copy of the assembled file.
    /// </summary>
    /// <returns>The complete file contents.</returns>
    /// <exception cref="InvalidOperationException">The transfer has not completed.</exception>
    public byte[] GetFile()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException(
                "The file transfer has not completed, so the assembled contents are not available.");
        }

        return (byte[])_buffer.Clone();
    }

    /// <summary>
    /// Discards any transfer in progress and returns the receiver to its initial state.
    /// </summary>
    public void Reset()
    {
        _buffer = null;
        _highWaterMark = 0;
        FileType = 0;
    }

    private FileTransferStatus Status(FileTransferStatus.StatusDetail detail) =>
        new(detail, Action, RequestedDelay, UpdateMessageMaximum);
}
