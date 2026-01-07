using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData;

[Category("Unit")]
internal class BiometricReadDataTest
{
    private byte[] TestData => [0x00, 0x07, 0x02, 0x04];

    private BiometricReadData TestBiometricReadData => new(0, BiometricType.LeftIndexFingerPrint,
        BiometricFormat.FingerPrintTemplate, 4);

    [Test]
    public void CheckConstantValues()
    {
        // Arrange Act Assert
        Assert.That(TestBiometricReadData.CommandType, Is.EqualTo(CommandType.BioRead));
        Assert.That(TestBiometricReadData.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        // Act
        var actual = TestBiometricReadData.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(TestData));
    }

    [Test]
    public void ParseData()
    {
        var actual = BiometricReadData.ParseData(TestData);

        Assert.That(actual.ReaderNumber, Is.EqualTo(TestBiometricReadData.ReaderNumber));
        Assert.That(actual.BiometricType, Is.EqualTo(TestBiometricReadData.BiometricType));
        Assert.That(actual.BiometricFormatType, Is.EqualTo(TestBiometricReadData.BiometricFormatType));
        Assert.That(actual.Quality, Is.EqualTo(TestBiometricReadData.Quality));
    }
}