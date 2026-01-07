using System.Linq;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Model.CommandData;

[TestFixture]
[Category("Unit")]
public class OutputControlsTest
{
    private byte[] TestData => [0x03, 0x01, 0xC4, 0x09, 0x05, 0x04, 0x10, 0x27];
    
    private OutputControl[] TestOutputControls =>
    [
        new OutputControl(3, OutputControlCode.PermanentStateOffAbortTimedOperation, 2500),
        new OutputControl(5, OutputControlCode.PermanentStateOnAllowTimedOperation, 10000)
    ];
    
    [Test]
    public void CheckConstantValues()
    {
        // Arrange Act
        var actual = new OutputControls(TestOutputControls);

        // Assert
        Assert.That(actual.CommandType, Is.EqualTo(CommandType.OutputControl));
        Assert.That(actual.SecurityControlBlock().ToArray(),
            Is.EqualTo(SecurityBlock.CommandMessageWithDataSecurity.ToArray()));
    }

    [Test]
    public void BuildData()
    {
        // Arrange
        var outputControls = new OutputControls(TestOutputControls);

        // Act
        var actual = outputControls.BuildData();

        // Assert
        Assert.That(actual, Is.EqualTo(TestData));
    }
    
    [Test]
    public void ParseData()
    {
        // Arrange
        // Act
        var actual = OutputControls.ParseData(TestData);

        // Assert
        var actualControls = actual.Controls.ToArray();
        for (var index = 0; index < actualControls.Length; index++)
        {
            Assert.That(actualControls[index].OutputNumber, Is.EqualTo(TestOutputControls[index].OutputNumber));
            Assert.That(actualControls[index].OutputControlCode, Is.EqualTo(TestOutputControls[index].OutputControlCode));
            Assert.That(actualControls[index].Timer, Is.EqualTo(TestOutputControls[index].Timer));
        }
    }
}