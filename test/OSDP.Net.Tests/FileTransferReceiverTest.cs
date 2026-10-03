using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests;

[Category("Unit")]
internal class FileTransferReceiverTest
{
    private const byte FileType = 0x01;

    private static FileTransferFragment Fragment(int totalSize, int offset, byte[] data, byte type = FileType) =>
        new(type, new MessageDataFragment(totalSize, offset, (ushort)data.Length, data,
            MessageDataFragmentFieldSize.FourBytes));

    private static byte[] Bytes(int count, byte seed = 0) =>
        Enumerable.Range(0, count).Select(index => (byte)(index + seed)).ToArray();

    [Test]
    public void AcceptsSingleFragmentTransfer()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        var file = Bytes(10);

        // Act
        var status = receiver.AcceptFragment(Fragment(file.Length, 0, file));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileContentsProcessed));
        Assert.That(receiver.IsComplete, Is.True);
        Assert.That(receiver.GetFile(), Is.EqualTo(file));
    }

    [Test]
    public void ReassemblesSequentialFragmentsInOrder()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        var file = Bytes(300);

        // Act
        var first = receiver.AcceptFragment(Fragment(file.Length, 0, file.Take(128).ToArray()));
        var second = receiver.AcceptFragment(Fragment(file.Length, 128, file.Skip(128).Take(128).ToArray()));
        var third = receiver.AcceptFragment(Fragment(file.Length, 256, file.Skip(256).ToArray()));

        // Assert
        Assert.That(first.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.OkToProceed));
        Assert.That(second.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.OkToProceed));
        Assert.That(third.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileContentsProcessed));
        Assert.That(receiver.GetFile(), Is.EqualTo(file));
    }

    [Test]
    public void ReportsProgressWhileTransferIsIncomplete()
    {
        // Arrange
        var receiver = new FileTransferReceiver();

        // Act
        receiver.AcceptFragment(Fragment(300, 0, Bytes(128)));

        // Assert
        Assert.That(receiver.IsTransferInProgress, Is.True);
        Assert.That(receiver.IsComplete, Is.False);
        Assert.That(receiver.TotalSize, Is.EqualTo(300));
        Assert.That(receiver.ReceivedSize, Is.EqualTo(128));
        Assert.That(receiver.FileType, Is.EqualTo(FileType));
    }

    [Test]
    public void AbsorbsRetransmittedFragmentWithoutCorruptingTheFile()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        var file = Bytes(20);

        // Act - the ACU resends the first fragment before continuing
        receiver.AcceptFragment(Fragment(file.Length, 0, file.Take(10).ToArray()));
        var repeat = receiver.AcceptFragment(Fragment(file.Length, 0, file.Take(10).ToArray()));
        var final = receiver.AcceptFragment(Fragment(file.Length, 10, file.Skip(10).ToArray()));

        // Assert
        Assert.That(repeat.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.OkToProceed));
        Assert.That(final.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileContentsProcessed));
        Assert.That(receiver.GetFile(), Is.EqualTo(file));
    }

    [Test]
    public void RejectsFragmentThatWouldLeaveAGap()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        receiver.AcceptFragment(Fragment(300, 0, Bytes(128)));

        // Act - offset 200 skips past the 128 bytes received so far
        var status = receiver.AcceptFragment(Fragment(300, 200, Bytes(100)));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileDataUnacceptable));
        Assert.That(receiver.IsTransferInProgress, Is.False);
    }

    [Test]
    public void RejectsResumingWithNoTransferInProgress()
    {
        // Arrange
        var receiver = new FileTransferReceiver();

        // Act
        var status = receiver.AcceptFragment(Fragment(300, 128, Bytes(128)));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileDataUnacceptable));
        Assert.That(receiver.IsTransferInProgress, Is.False);
    }

    [Test]
    public void RejectsFragmentExtendingPastTheDeclaredTotalSize()
    {
        // Arrange
        var receiver = new FileTransferReceiver();

        // Act
        var status = receiver.AcceptFragment(Fragment(10, 0, Bytes(20)));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileDataUnacceptable));
    }

    [Test]
    public void RejectsFragmentDeclaringMoreDataThanItCarries()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        var fragment = new FileTransferFragment(FileType,
            new MessageDataFragment(100, 0, 50, Bytes(10), MessageDataFragmentFieldSize.FourBytes));

        // Act
        var status = receiver.AcceptFragment(fragment);

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileDataUnacceptable));
    }

    [Test]
    public void RejectsTotalSizeBeyondTheConfiguredMaximum()
    {
        // Arrange
        var receiver = new FileTransferReceiver(maximumFileSize: 64);

        // Act
        var status = receiver.AcceptFragment(Fragment(128, 0, Bytes(32)));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileDataUnacceptable));
    }

    [Test]
    public void RejectsTotalSizeChangingMidTransfer()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        receiver.AcceptFragment(Fragment(300, 0, Bytes(128)));

        // Act
        var status = receiver.AcceptFragment(Fragment(400, 128, Bytes(128)));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileDataUnacceptable));
        Assert.That(receiver.IsTransferInProgress, Is.False);
    }

    [Test]
    public void RejectsFileTypeChangingMidTransfer()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        receiver.AcceptFragment(Fragment(300, 0, Bytes(128)));

        // Act
        var status = receiver.AcceptFragment(Fragment(300, 128, Bytes(128), type: 0x03));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileDataUnacceptable));
        Assert.That(receiver.IsTransferInProgress, Is.False);
    }

    [Test]
    public void RestartingAtOffsetZeroBeginsANewTransfer()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        receiver.AcceptFragment(Fragment(300, 0, Bytes(128)));
        var replacement = Bytes(20, seed: 99);

        // Act - the ACU abandons the first attempt and starts a different file
        var status = receiver.AcceptFragment(Fragment(replacement.Length, 0, replacement));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileContentsProcessed));
        Assert.That(receiver.GetFile(), Is.EqualTo(replacement));
    }

    [Test]
    public void CompletesAnEmptyFileImmediately()
    {
        // Arrange
        var receiver = new FileTransferReceiver();

        // Act
        var status = receiver.AcceptFragment(Fragment(0, 0, []));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileContentsProcessed));
        Assert.That(receiver.GetFile(), Is.Empty);
    }

    [Test]
    public void IdleFragmentAfterCompletionKeepsReportingProcessed()
    {
        // Arrange - the spec's "idling" message carries the total size, an offset at the end, and no data
        var receiver = new FileTransferReceiver();
        var file = Bytes(10);
        receiver.AcceptFragment(Fragment(file.Length, 0, file));

        // Act
        var status = receiver.AcceptFragment(Fragment(file.Length, file.Length, []));

        // Assert
        Assert.That(status.Detail, Is.EqualTo(FileTransferStatus.StatusDetail.FileContentsProcessed));
    }

    [Test]
    public void StatusRepliesCarryTheConfiguredControlFields()
    {
        // Arrange
        var receiver = new FileTransferReceiver
        {
            Action = FileTransferStatus.ControlFlags.Interleave,
            RequestedDelay = 100,
            UpdateMessageMaximum = 256
        };

        // Act
        var status = receiver.AcceptFragment(Fragment(300, 0, Bytes(128)));

        // Assert
        Assert.That(status.Action, Is.EqualTo(FileTransferStatus.ControlFlags.Interleave));
        Assert.That(status.RequestedDelay, Is.EqualTo(100));
        Assert.That(status.UpdateMessageMaximum, Is.EqualTo(256));
    }

    [Test]
    public void GetFileThrowsWhileTheTransferIsIncomplete()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        receiver.AcceptFragment(Fragment(300, 0, Bytes(128)));

        // Act Assert
        Assert.Throws<InvalidOperationException>(() => receiver.GetFile());
    }

    [Test]
    public void ResetDiscardsTheTransferInProgress()
    {
        // Arrange
        var receiver = new FileTransferReceiver();
        receiver.AcceptFragment(Fragment(300, 0, Bytes(128)));

        // Act
        receiver.Reset();

        // Assert
        Assert.That(receiver.IsTransferInProgress, Is.False);
        Assert.That(receiver.ReceivedSize, Is.EqualTo(0));
        Assert.That(receiver.TotalSize, Is.EqualTo(0));
    }

    [Test]
    public void ThrowsWhenTheMaximumFileSizeIsNotPositive()
    {
        // Arrange Act Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FileTransferReceiver(maximumFileSize: 0));
    }

    [Test]
    public void ThrowsWhenTheFragmentIsNull()
    {
        // Arrange
        var receiver = new FileTransferReceiver();

        // Act Assert
        Assert.Throws<ArgumentNullException>(() => receiver.AcceptFragment(null));
    }
}
