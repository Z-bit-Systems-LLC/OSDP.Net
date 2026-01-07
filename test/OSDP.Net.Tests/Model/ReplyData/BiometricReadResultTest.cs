using System.Collections.Generic;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData;

[Category("Unit")]
public class BiometricReadResultTest
{
    [Test]
    public void ParseData()
    {
        // Arrange
        var data = new List<byte> { 0x00, 0x00, 0x01, 0x50 };

        data.AddRange(Message.ConvertShortToBytes(5));
        data.AddRange(new byte[] {0x00, 0x01, 0x02, 0x03, 0x04});

        // Act
        var biometricReadResults = BiometricReadResult.ParseData(data.ToArray());

        // Assert
        Assert.That(0, Is.EqualTo(biometricReadResults.ReaderNumber));
        Assert.That(BiometricStatus.Success, Is.EqualTo(biometricReadResults.Status));
        Assert.That(BiometricType.RightThumbPrint, Is.EqualTo(biometricReadResults.Type));
        Assert.That(0x50, Is.EqualTo(biometricReadResults.Quality));
        Assert.That(5, Is.EqualTo(biometricReadResults.Length));
        Assert.That(new byte[] {0x00, 0x01, 0x02, 0x03, 0x04}, Is.EqualTo(biometricReadResults.TemplateData));
    }
}