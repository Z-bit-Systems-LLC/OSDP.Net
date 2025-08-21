using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData;

[Category("Unit")]
internal class FileTransferFragmentTest
{
    private byte[] TestData =>
    [
        0x01, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x09, 0x08, 0x07, 0x06, 0x05
    ];

    private FileTransferFragment TestFileTransferFragment => new(0x01, 10, 0, 5, [0x09, 0x08, 0x07, 0x06, 0x05]);

    [Test]
    public void CheckConstantValues()
    {
        // Arrange Act Assert
        Assert.That(TestFileTransferFragment.CommandType, Is.EqualTo(CommandType.FileTransfer));
        Assert.That(TestFileTransferFragment.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        // Act
        var actual = TestFileTransferFragment.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(TestData));
    }

    [Test]
    public void ParseData()
    {
        var actual = FileTransferFragment.ParseData(TestData);

        Assert.That(actual.Type, Is.EqualTo(TestFileTransferFragment.Type));
        Assert.That(actual.TotalSize, Is.EqualTo(TestFileTransferFragment.TotalSize));
        Assert.That(actual.Offset, Is.EqualTo(TestFileTransferFragment.Offset));
        Assert.That(actual.FragmentSize, Is.EqualTo(TestFileTransferFragment.FragmentSize));
        Assert.That(actual.DataFragment, Is.EqualTo(TestFileTransferFragment.DataFragment));
    }
}