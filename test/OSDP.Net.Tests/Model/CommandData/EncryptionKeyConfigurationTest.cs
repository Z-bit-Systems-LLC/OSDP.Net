using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData;

[Category("Unit")]
internal class EncryptionKeyConfigurationTest
{
    private byte[] TestData =>
    [
        0x01, 0x10, 0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08, 0x07, 0x06, 0x05, 0x05, 0x03, 0x02, 0x01, 0x00
    ];

    private EncryptionKeyConfiguration TestEncryptionKeyConfiguration => new(KeyType.SecureChannelBaseKey,
        [0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08, 0x07, 0x06, 0x05, 0x05, 0x03, 0x02, 0x01, 0x00]);

    [Test]
    public void CheckConstantValues()
    {
        // Arrange Act Assert
        Assert.That(TestEncryptionKeyConfiguration.CommandType, Is.EqualTo(CommandType.KeySet));
        Assert.That(TestEncryptionKeyConfiguration.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        // Act
        var actual = TestEncryptionKeyConfiguration.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(TestData));
    }

    [Test]
    public void ParseData()
    {
        var actual = EncryptionKeyConfiguration.ParseData(TestData);

        Assert.That(actual.KeyType, Is.EqualTo(TestEncryptionKeyConfiguration.KeyType));
        Assert.That(actual.KeyData, Is.EqualTo(TestEncryptionKeyConfiguration.KeyData));
    }
}