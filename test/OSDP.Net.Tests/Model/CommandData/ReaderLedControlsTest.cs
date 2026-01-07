using System.Linq;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData;

[TestFixture]
[Category("Unit")]
public class ReaderLedControlsTest
{
    private byte[] TestData => [0x02, 0x03, 0x02, 0x01, 0x02, 0x06, 0x00, 0x04, 0x00, 0x01, 0x02, 0x06, 0x04, 0x03, 
                                0x05, 0x06, 0x02, 0x01, 0x02, 0x06, 0x00, 0x04, 0x00, 0x01, 0x02, 0x06, 0x04, 0x03];
    
    private ReaderLedControl[] TestReaderLedControls =>
    [
        new ReaderLedControl(2, 3, 
            TemporaryReaderControlCode.SetTemporaryAndStartTimer, 1, 2, LedColor.Cyan, LedColor.Black, 4, 
            PermanentReaderControlCode.SetPermanentState, 2, 6, LedColor.Blue, LedColor.Amber),
        new ReaderLedControl(5, 6, 
            TemporaryReaderControlCode.SetTemporaryAndStartTimer, 1, 2, LedColor.Cyan, LedColor.Black, 4, 
            PermanentReaderControlCode.SetPermanentState, 2, 6, LedColor.Blue, LedColor.Amber)
    ];
    
    [Test]
    public void CheckConstantValues()
    {
        // Arrange Act
        var actual = new ReaderLedControls(TestReaderLedControls);

        // Assert
        Assert.That(actual.CommandType, Is.EqualTo(CommandType.LEDControl));
        Assert.That(actual.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        var readerLedControls = new ReaderLedControls(TestReaderLedControls);

        // Act
        var actual = readerLedControls.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(TestData));
    }
    
    [Test]
    public void ParseData()
    {
        // Arrange
        // Act
        var actual = ReaderLedControls.ParseData(TestData);

        // Assert
        var actualControls = actual.Controls.ToArray();
        for (var index = 0; index < actualControls.Length; index++)
        {
            Assert.That(actualControls[index].ReaderNumber, Is.EqualTo(TestReaderLedControls[index].ReaderNumber));
            Assert.That(actualControls[index].LedNumber, Is.EqualTo(TestReaderLedControls[index].LedNumber));
            Assert.That(actualControls[index].TemporaryMode, Is.EqualTo(TestReaderLedControls[index].TemporaryMode));
            Assert.That(actualControls[index].TemporaryOnTime,
                Is.EqualTo(TestReaderLedControls[index].TemporaryOnTime));
            Assert.That(actualControls[index].TemporaryOffTime,
                Is.EqualTo(TestReaderLedControls[index].TemporaryOffTime));
            Assert.That(actualControls[index].TemporaryOnColor,
                Is.EqualTo(TestReaderLedControls[index].TemporaryOnColor));
            Assert.That(actualControls[index].TemporaryOffColor,
                Is.EqualTo(TestReaderLedControls[index].TemporaryOffColor));
            Assert.That(actualControls[index].TemporaryTimer, Is.EqualTo(TestReaderLedControls[index].TemporaryTimer));
            Assert.That(actualControls[index].PermanentMode, Is.EqualTo(TestReaderLedControls[index].PermanentMode));
            Assert.That(actualControls[index].PermanentOnTime,
                Is.EqualTo(TestReaderLedControls[index].PermanentOnTime));
            Assert.That(actualControls[index].PermanentOffTime,
                Is.EqualTo(TestReaderLedControls[index].PermanentOffTime));
            Assert.That(actualControls[index].PermanentOnColor,
                Is.EqualTo(TestReaderLedControls[index].PermanentOnColor));
            Assert.That(actualControls[index].PermanentOffColor,
                Is.EqualTo(TestReaderLedControls[index].PermanentOffColor));
        }
    }
}