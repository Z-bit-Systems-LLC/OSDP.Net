using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData;

[Category("Unit")]
internal class ExtendedWriteTest
{
    private ExtendedWrite TestExtendedWrite => new(1, 2, [0xAA, 0xBB, 0xCC]);

    [Test]
    public void CheckConstantValues()
    {
        // Arrange Act Assert
        Assert.That(TestExtendedWrite.CommandType, Is.EqualTo(CommandType.ExtendedWrite));
        Assert.That((byte)TestExtendedWrite.CommandType, Is.EqualTo(0xA1));
        Assert.That(TestExtendedWrite.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        var expected = new byte[] { 0x01, 0x02, 0xAA, 0xBB, 0xCC };

        // Act
        var actual = TestExtendedWrite.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void ReadModeSetting_ProducesExpectedBytes()
    {
        var command = ExtendedWrite.ReadModeSetting();

        Assert.That(command.Mode, Is.EqualTo(0));
        Assert.That(command.PCommand, Is.EqualTo(1));
        Assert.That(command.PData, Is.Empty);
    }

    [Test]
    public void ModeZeroConfiguration_Enabled_ProducesExpectedBytes()
    {
        var command = ExtendedWrite.ModeZeroConfiguration(true);

        Assert.That(command.Mode, Is.EqualTo(0));
        Assert.That(command.PCommand, Is.EqualTo(2));
        Assert.That(command.PData, Is.EqualTo(new byte[] { 0, 1 }));
    }

    [Test]
    public void ModeZeroConfiguration_Disabled_ProducesExpectedBytes()
    {
        var command = ExtendedWrite.ModeZeroConfiguration(false);

        Assert.That(command.Mode, Is.EqualTo(0));
        Assert.That(command.PCommand, Is.EqualTo(2));
        Assert.That(command.PData, Is.EqualTo(new byte[] { 0, 0 }));
    }

    [Test]
    public void ModeOneConfiguration_ProducesExpectedBytes()
    {
        var command = ExtendedWrite.ModeOneConfiguration();

        Assert.That(command.Mode, Is.EqualTo(0));
        Assert.That(command.PCommand, Is.EqualTo(2));
        Assert.That(command.PData, Is.EqualTo(new byte[] { 1, 0 }));
    }

    [Test]
    public void ModeOnePassAPDUCommand_ProducesExpectedBytes()
    {
        var apdu = new byte[] { 0x00, 0xA4, 0x04, 0x00 };

        var command = ExtendedWrite.ModeOnePassAPDUCommand(0x03, apdu);

        Assert.That(command.Mode, Is.EqualTo(1));
        Assert.That(command.PCommand, Is.EqualTo(1));
        Assert.That(command.PData, Is.EqualTo(new byte[] { 0x03, 0x00, 0xA4, 0x04, 0x00 }));
    }

    [Test]
    public void ModeOneTerminateSmartCardConnection_ProducesExpectedBytes()
    {
        var command = ExtendedWrite.ModeOneTerminateSmartCardConnection(0x02);

        Assert.That(command.Mode, Is.EqualTo(1));
        Assert.That(command.PCommand, Is.EqualTo(2));
        Assert.That(command.PData, Is.EqualTo(new byte[] { 0x02 }));
    }

    [Test]
    public void ModeOneSmartCardScan_ProducesExpectedBytes()
    {
        var command = ExtendedWrite.ModeOneSmartCardScan(0x01);

        Assert.That(command.Mode, Is.EqualTo(1));
        Assert.That(command.PCommand, Is.EqualTo(4));
        Assert.That(command.PData, Is.EqualTo(new byte[] { 0x01 }));
    }
}
