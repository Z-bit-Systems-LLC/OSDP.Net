using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData;

[Category("Unit")]
internal class BiometricTemplateDataTest
{
    private byte[] TestData => [0x00, 0x07, 0x02, 0x04, 0x03, 0x00, 0x01, 0x02, 0x05];

    private BiometricTemplateData TestBiometricTemplateData => new(0, BiometricType.LeftIndexFingerPrint,
        BiometricFormat.FingerPrintTemplate, 4, [0x01, 0x02, 0x05]);

    [Test]
    public void CheckConstantValues()
    {
        // Arrange Act Assert
        Assert.That(TestBiometricTemplateData.CommandType, Is.EqualTo(CommandType.BioMatch));
        Assert.That(TestBiometricTemplateData.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        // Act
        var actual = TestBiometricTemplateData.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(TestData));
    }

    [Test]
    public void ParseData()
    {
        var actual = BiometricTemplateData.ParseData(TestData);

        Assert.That(actual.ReaderNumber, Is.EqualTo(TestBiometricTemplateData.ReaderNumber));
        Assert.That(actual.BiometricType, Is.EqualTo(TestBiometricTemplateData.BiometricType));
        Assert.That(actual.BiometricFormatType, Is.EqualTo(TestBiometricTemplateData.BiometricFormatType));
        Assert.That(actual.QualityThreshold, Is.EqualTo(TestBiometricTemplateData.QualityThreshold));
        Assert.That(actual.TemplateData, Is.EqualTo(TestBiometricTemplateData.TemplateData));
    }
}