using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.IntegrationTests;

/// <summary>
/// OSDP 2.2.2 Compliance Tests - File Transfer (subclauses 6.26 and 7.25)
///
/// Drives a real <see cref="ControlPanel.FileTransfer"/> against a PD that reassembles the
/// fragments with <see cref="FileTransferReceiver"/>, covering the whole osdp_FILETRANSFER /
/// osdp_FTSTAT exchange over the loopback connection.
/// </summary>
[TestFixture]
[Category("Integration")]
public class FileTransferTests : IntegrationTestFixtureBase
{
    private const byte OpaqueFileContents = 0x01;

    private static byte[] TestFile(int size) =>
        Enumerable.Range(0, size).Select(index => (byte)(index % 251)).ToArray();

    [Test]
    public async Task PdReassemblesAFileSentAcrossMultipleFragments()
    {
        // Arrange
        await InitTestTargets(cfg => cfg.RequireSecurity = false);
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var receiver = new FileTransferReceiver();
        TargetDevice.FileTransferReceiver = receiver;

        var file = TestFile(700);
        var reportedStatuses = new List<ControlPanel.FileTransferStatus>();

        // Act
        var result = await TargetPanel.FileTransfer(ConnectionId, DeviceAddress, OpaqueFileContents, file,
            fragmentSize: 128, reportedStatuses.Add);

        // Assert
        Assert.That(result, Is.EqualTo(FileTransferStatus.StatusDetail.FileContentsProcessed));
        Assert.That(receiver.IsComplete, Is.True);
        Assert.That(receiver.GetFile(), Is.EqualTo(file));
        Assert.That(receiver.FileType, Is.EqualTo(OpaqueFileContents));

        // The ACU should have been told to proceed on every fragment but the last.
        Assert.That(reportedStatuses.Count, Is.GreaterThan(1));
        Assert.That(reportedStatuses.Take(reportedStatuses.Count - 1).Select(status => status.Status),
            Is.All.EqualTo(FileTransferStatus.StatusDetail.OkToProceed));
        Assert.That(reportedStatuses.Last().CurrentOffset, Is.EqualTo(file.Length));
    }

    [Test]
    public async Task PdReassemblesAFileThatFitsInASingleFragment()
    {
        // Arrange
        await InitTestTargets(cfg => cfg.RequireSecurity = false);
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var receiver = new FileTransferReceiver();
        TargetDevice.FileTransferReceiver = receiver;

        var file = TestFile(64);

        // Act
        var result = await TargetPanel.FileTransfer(ConnectionId, DeviceAddress, OpaqueFileContents, file,
            fragmentSize: 128, _ => { });

        // Assert
        Assert.That(result, Is.EqualTo(FileTransferStatus.StatusDetail.FileContentsProcessed));
        Assert.That(receiver.GetFile(), Is.EqualTo(file));
    }

    [Test]
    public async Task PdTransfersOverAnEstablishedSecureChannel()
    {
        // Arrange
        await InitTestTargets(cfg =>
        {
            cfg.RequireSecurity = true;
            cfg.SecurityKey = IntegrationConsts.DefaultSCBK;
        });
        AddDeviceToPanel(IntegrationConsts.DefaultSCBK);
        await WaitForDeviceOnlineStatus();

        var receiver = new FileTransferReceiver();
        TargetDevice.FileTransferReceiver = receiver;

        var file = TestFile(500);

        // Act
        var result = await TargetPanel.FileTransfer(ConnectionId, DeviceAddress, OpaqueFileContents, file,
            fragmentSize: 128, _ => { });

        // Assert
        Assert.That(result, Is.EqualTo(FileTransferStatus.StatusDetail.FileContentsProcessed));
        Assert.That(receiver.GetFile(), Is.EqualTo(file));
    }

    [Test]
    public async Task AcuAbortsWhenThePdRejectsTheFile()
    {
        // Arrange - a receiver capped below the file size rejects the very first fragment
        await InitTestTargets(cfg => cfg.RequireSecurity = false);
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        TargetDevice.FileTransferReceiver = new FileTransferReceiver(maximumFileSize: 16);

        var file = TestFile(200);

        // Act
        var exception = Assert.ThrowsAsync<ControlPanel.FileTransferException>(
            () => TargetPanel.FileTransfer(ConnectionId, DeviceAddress, OpaqueFileContents, file,
                fragmentSize: 128, _ => { }));

        // Assert
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Status.Status,
            Is.EqualTo(FileTransferStatus.StatusDetail.FileDataUnacceptable));
    }

    [Test]
    public async Task PdNaksFileTransferWhenNoReceiverIsConfigured()
    {
        // Arrange - the base Device declares that it accepts no file transfers
        await InitTestTargets(cfg => cfg.RequireSecurity = false);
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var file = TestFile(64);

        // Act
        var exception = Assert.ThrowsAsync<ControlPanel.FileTransferException>(
            () => TargetPanel.FileTransfer(ConnectionId, DeviceAddress, OpaqueFileContents, file,
                fragmentSize: 128, _ => { }));

        // Assert
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Status.Nak, Is.Not.Null);
        Assert.That(exception.Status.Nak.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode));
    }
}
