using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData;

[Category("Unit")]
internal class FileTransferStatusTest
{
    [Test]
    public void CheckConstantValues()
    {
        var status = new FileTransferStatus(FileTransferStatus.StatusDetail.OkToProceed);

        Assert.That(status.Code, Is.EqualTo((byte)ReplyType.FileTransferStatus));
        Assert.That(status.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.ReplyMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        var status = new FileTransferStatus(FileTransferStatus.StatusDetail.FileContentsProcessed,
            FileTransferStatus.ControlFlags.Interleave, requestedDelay: 500, updateMessageMaximum: 1024);

        // Act
        var actual = status.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(new byte[]
        {
            0x01,       // FtAction - interleave
            0xF4, 0x01, // FtDelay - 500 ms, little endian
            0x01, 0x00, // FtStatusDetail - file contents processed
            0x00, 0x04  // FtUpdateMsgMax - 1024, little endian
        }));
    }

    [Test]
    public void BuildDataEncodesNegativeStatusAsSignedLittleEndian()
    {
        // Arrange
        var status = new FileTransferStatus(FileTransferStatus.StatusDetail.FileDataUnacceptable);

        // Act
        var actual = status.BuildData();

        // Assert
        Assert.That(actual.Length, Is.EqualTo(7));
        Assert.That(new[] { actual[3], actual[4] }, Is.EqualTo(new byte[] { 0xFD, 0xFF }), "-3 as a signed short");
    }

    [Test]
    public void BuildDataRoundTripsThroughParseData()
    {
        // Arrange
        var status = new FileTransferStatus(FileTransferStatus.StatusDetail.AbortFileTransfer,
            FileTransferStatus.ControlFlags.LeaveSecureChannel | FileTransferStatus.ControlFlags.PollResponseAvailable,
            requestedDelay: 250, updateMessageMaximum: 128);

        // Act
        var actual = FileTransferStatus.ParseData(status.BuildData());

        // Assert
        Assert.That(actual.Detail, Is.EqualTo(status.Detail));
        Assert.That(actual.Action, Is.EqualTo(status.Action));
        Assert.That(actual.RequestedDelay, Is.EqualTo(status.RequestedDelay));
        Assert.That(actual.UpdateMessageMaximum, Is.EqualTo(status.UpdateMessageMaximum));
    }
}
