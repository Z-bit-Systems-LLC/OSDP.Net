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

    [Test]
    public void SingleControlWithNopModes()
    {
        // Arrange - Test NOP control codes
        var control = new ReaderLedControl(0, 0,
            TemporaryReaderControlCode.Nop, 0, 0, LedColor.Black, LedColor.Black, 0,
            PermanentReaderControlCode.Nop, 0, 0, LedColor.Black, LedColor.Black);
        var controls = new ReaderLedControls([control]);

        // Act
        var data = controls.BuildData();
        var parsed = ReaderLedControls.ParseData(data);

        // Assert
        var parsedControl = parsed.Controls.First();
        Assert.That(parsedControl.TemporaryMode, Is.EqualTo(TemporaryReaderControlCode.Nop));
        Assert.That(parsedControl.PermanentMode, Is.EqualTo(PermanentReaderControlCode.Nop));
    }

    [Test]
    public void SingleControlWithCancelTemporaryMode()
    {
        // Arrange - Test Cancel temporary and display permanent mode
        var control = new ReaderLedControl(0, 1,
            TemporaryReaderControlCode.CancelAnyTemporaryAndDisplayPermanent, 1, 0, LedColor.Red, LedColor.Black, 0,
            PermanentReaderControlCode.SetPermanentState, 1, 0, LedColor.Green, LedColor.Black);
        var controls = new ReaderLedControls([control]);

        // Act
        var data = controls.BuildData();
        var parsed = ReaderLedControls.ParseData(data);

        // Assert
        var parsedControl = parsed.Controls.First();
        Assert.That(parsedControl.TemporaryMode, Is.EqualTo(TemporaryReaderControlCode.CancelAnyTemporaryAndDisplayPermanent));
        Assert.That(parsedControl.PermanentMode, Is.EqualTo(PermanentReaderControlCode.SetPermanentState));
        Assert.That(parsedControl.PermanentOnColor, Is.EqualTo(LedColor.Green));
    }

    [Test]
    public void ControlWithMaxTemporaryTimer()
    {
        // Arrange - Test max ushort value for temporary timer
        var control = new ReaderLedControl(0, 0,
            TemporaryReaderControlCode.SetTemporaryAndStartTimer, 1, 1, LedColor.Red, LedColor.Black, ushort.MaxValue,
            PermanentReaderControlCode.SetPermanentState, 1, 0, LedColor.Red, LedColor.Black);
        var controls = new ReaderLedControls([control]);

        // Act
        var data = controls.BuildData();
        var parsed = ReaderLedControls.ParseData(data);

        // Assert
        var parsedControl = parsed.Controls.First();
        Assert.That(parsedControl.TemporaryTimer, Is.EqualTo(ushort.MaxValue));
    }

    [Test]
    public void ControlWithZeroTemporaryTimer()
    {
        // Arrange - Timer value 0 means "forever" per OSDP spec
        var control = new ReaderLedControl(0, 0,
            TemporaryReaderControlCode.SetTemporaryAndStartTimer, 1, 1, LedColor.Red, LedColor.Black, 0,
            PermanentReaderControlCode.SetPermanentState, 1, 0, LedColor.Red, LedColor.Black);
        var controls = new ReaderLedControls([control]);

        // Act
        var data = controls.BuildData();
        var parsed = ReaderLedControls.ParseData(data);

        // Assert
        var parsedControl = parsed.Controls.First();
        Assert.That(parsedControl.TemporaryTimer, Is.EqualTo(0));
    }

    [Test]
    public void AllStandardColorsRoundTrip()
    {
        // Arrange - Test all standard LED colors
        var colors = new[] { LedColor.Black, LedColor.Red, LedColor.Green, LedColor.Amber,
                             LedColor.Blue, LedColor.Magenta, LedColor.Cyan, LedColor.White };

        foreach (var color in colors)
        {
            var control = new ReaderLedControl(0, 0,
                TemporaryReaderControlCode.SetTemporaryAndStartTimer, 1, 1, color, color, 10,
                PermanentReaderControlCode.SetPermanentState, 1, 1, color, color);
            var controls = new ReaderLedControls([control]);

            // Act
            var data = controls.BuildData();
            var parsed = ReaderLedControls.ParseData(data);

            // Assert
            var parsedControl = parsed.Controls.First();
            Assert.That(parsedControl.TemporaryOnColor, Is.EqualTo(color), $"TemporaryOnColor mismatch for {color}");
            Assert.That(parsedControl.TemporaryOffColor, Is.EqualTo(color), $"TemporaryOffColor mismatch for {color}");
            Assert.That(parsedControl.PermanentOnColor, Is.EqualTo(color), $"PermanentOnColor mismatch for {color}");
            Assert.That(parsedControl.PermanentOffColor, Is.EqualTo(color), $"PermanentOffColor mismatch for {color}");
        }
    }

    [Test]
    public void MaxReaderAndLedNumbers()
    {
        // Arrange - Test max byte values for reader and LED numbers
        var control = new ReaderLedControl(byte.MaxValue, byte.MaxValue,
            TemporaryReaderControlCode.SetTemporaryAndStartTimer, 1, 1, LedColor.Red, LedColor.Black, 10,
            PermanentReaderControlCode.SetPermanentState, 1, 1, LedColor.Red, LedColor.Black);
        var controls = new ReaderLedControls([control]);

        // Act
        var data = controls.BuildData();
        var parsed = ReaderLedControls.ParseData(data);

        // Assert
        var parsedControl = parsed.Controls.First();
        Assert.That(parsedControl.ReaderNumber, Is.EqualTo(byte.MaxValue));
        Assert.That(parsedControl.LedNumber, Is.EqualTo(byte.MaxValue));
    }

    [Test]
    public void MaxTimeValues()
    {
        // Arrange - Test max byte values for on/off times
        var control = new ReaderLedControl(0, 0,
            TemporaryReaderControlCode.SetTemporaryAndStartTimer, byte.MaxValue, byte.MaxValue, LedColor.Red, LedColor.Black, 10,
            PermanentReaderControlCode.SetPermanentState, byte.MaxValue, byte.MaxValue, LedColor.Red, LedColor.Black);
        var controls = new ReaderLedControls([control]);

        // Act
        var data = controls.BuildData();
        var parsed = ReaderLedControls.ParseData(data);

        // Assert
        var parsedControl = parsed.Controls.First();
        Assert.That(parsedControl.TemporaryOnTime, Is.EqualTo(byte.MaxValue));
        Assert.That(parsedControl.TemporaryOffTime, Is.EqualTo(byte.MaxValue));
        Assert.That(parsedControl.PermanentOnTime, Is.EqualTo(byte.MaxValue));
        Assert.That(parsedControl.PermanentOffTime, Is.EqualTo(byte.MaxValue));
    }

    [Test]
    public void EmptyControlsArrayThrowsException()
    {
        // Arrange & Act & Assert
        // ReSharper disable once ObjectCreationAsStatement
        Assert.Throws<System.Exception>(() => new ReaderLedControls([]));
    }

    [Test]
    public void MultipleControlsPreserveOrder()
    {
        // Arrange - Test multiple controls preserve order
        var control1 = new ReaderLedControl(0, 0,
            TemporaryReaderControlCode.Nop, 1, 0, LedColor.Red, LedColor.Black, 0,
            PermanentReaderControlCode.SetPermanentState, 1, 0, LedColor.Red, LedColor.Black);
        var control2 = new ReaderLedControl(1, 1,
            TemporaryReaderControlCode.Nop, 2, 0, LedColor.Green, LedColor.Black, 0,
            PermanentReaderControlCode.SetPermanentState, 2, 0, LedColor.Green, LedColor.Black);
        var control3 = new ReaderLedControl(2, 2,
            TemporaryReaderControlCode.Nop, 3, 0, LedColor.Blue, LedColor.Black, 0,
            PermanentReaderControlCode.SetPermanentState, 3, 0, LedColor.Blue, LedColor.Black);
        var controls = new ReaderLedControls([control1, control2, control3]);

        // Act
        var data = controls.BuildData();
        var parsed = ReaderLedControls.ParseData(data);

        // Assert
        var parsedControls = parsed.Controls.ToArray();
        Assert.That(parsedControls.Length, Is.EqualTo(3));
        Assert.That(parsedControls[0].ReaderNumber, Is.EqualTo(0));
        Assert.That(parsedControls[0].LedNumber, Is.EqualTo(0));
        Assert.That(parsedControls[1].ReaderNumber, Is.EqualTo(1));
        Assert.That(parsedControls[1].LedNumber, Is.EqualTo(1));
        Assert.That(parsedControls[2].ReaderNumber, Is.EqualTo(2));
        Assert.That(parsedControls[2].LedNumber, Is.EqualTo(2));
    }
}