using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData;

[Category("Unit")]
internal class CommunicationConfigurationTest
{
    private byte[] TestData => [0x02, 0x80, 0x25, 0x00, 0x00];

    private CommunicationConfiguration TestCommunicationConfiguration => new(2, 9600);

    [Test]
    public void CheckConstantValues()
    {
        // Arrange Act Assert
        Assert.That(TestCommunicationConfiguration.CommandType, Is.EqualTo(CommandType.CommunicationSet));
        Assert.That(TestCommunicationConfiguration.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        // Act
        var actual = TestCommunicationConfiguration.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(TestData));
    }

    [Test]
    public void ParseData()
    {
        var actual = CommunicationConfiguration.ParseData(TestData);

        Assert.That(actual.Address, Is.EqualTo(TestCommunicationConfiguration.Address));
        Assert.That(actual.BaudRate, Is.EqualTo(TestCommunicationConfiguration.BaudRate));
    }
}