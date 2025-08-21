using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData;

[Category("Unit")]
internal class ManufacturerSpecificTest
{
    private byte[] TestData =>
    [
        0x04, 0x06, 0x08, 0x09, 0x08, 0x07, 0x06, 0x05
    ];

    private ManufacturerSpecific TestManufacturerSpecific => new([0x04, 0x06, 0x08], [0x09, 0x08, 0x07, 0x06, 0x05]);

    [Test]
    public void CheckConstantValues()
    {
        // Arrange Act Assert
        Assert.That(TestManufacturerSpecific.CommandType, Is.EqualTo(CommandType.ManufacturerSpecific));
        Assert.That(TestManufacturerSpecific.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        // Act
        var actual = TestManufacturerSpecific.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(TestData));
    }

    [Test]
    public void ParseData()
    {
        var actual = ManufacturerSpecific.ParseData(TestData);

        Assert.That(actual.VendorCode, Is.EqualTo(TestManufacturerSpecific.VendorCode));
        Assert.That(actual.Data, Is.EqualTo(TestManufacturerSpecific.Data));
    }
}