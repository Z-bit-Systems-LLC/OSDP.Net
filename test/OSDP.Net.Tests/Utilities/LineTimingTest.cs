using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tests.Utilities;

[Category("Unit")]
internal class LineTimingTest
{
    [Test]
    [TestCase(9600, 1, 1.0417)]
    [TestCase(9600, 12, 12.5)]
    [TestCase(115200, 128, 11.1111)]
    public void ForCharactersIsTheWireTimeOfThoseCharacters(int baudRate, int characterCount,
        double expectedMilliseconds)
    {
        // Arrange Act
        var time = LineTiming.ForCharacters(baudRate, characterCount);

        // Assert - 10 bits per character at 8-N-1
        Assert.That(time.TotalMilliseconds, Is.EqualTo(expectedMilliseconds).Within(0.001));
    }

    [Test]
    [TestCase(9600, 0)]
    [TestCase(9600, -1)]
    [TestCase(0, 4)]
    public void ForCharactersIsZeroForANonPositiveRateOrCount(int baudRate, int characterCount)
    {
        // Arrange Act Assert
        Assert.That(LineTiming.ForCharacters(baudRate, characterCount), Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    [TestCase(9600, 2.0833)]
    [TestCase(19200, 1.0417)]
    [TestCase(38400, 0.5208)]
    [TestCase(115200, 0.1736)]
    public void IdleLineIsTwoCharacterTimes(int baudRate, double expectedMilliseconds)
    {
        // Arrange Act
        var delay = LineTiming.IdleLine(baudRate);

        // Assert - two characters of 10 bits each (OSDP v2.2.2, 5.7)
        Assert.That(delay.TotalMilliseconds, Is.EqualTo(expectedMilliseconds).Within(0.001));
    }

    [Test]
    [TestCase(0)]
    [TestCase(-1)]
    public void IdleLineIsZeroWhenTheRateIsUnknown(int baudRate)
    {
        // Arrange Act Assert
        Assert.That(LineTiming.IdleLine(baudRate), Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task WaitAsyncHoldsForTheFullDelay()
    {
        // Arrange
        var delay = LineTiming.IdleLine(9600);
        long start = Stopwatch.GetTimestamp();

        // Act
        await LineTiming.WaitAsync(delay, start);

        // Assert - the wait must not finish early, or the reply still beats the line going idle
        var elapsed = TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency);
        Assert.That(elapsed, Is.GreaterThanOrEqualTo(delay));
    }

    [Test]
    public async Task WaitAsyncReturnsImmediatelyWhenTheDelayAlreadyElapsed()
    {
        // Arrange - a timestamp far enough in the past that the idle time is long since satisfied
        long longAgo = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
        long start = Stopwatch.GetTimestamp();

        // Act
        await LineTiming.WaitAsync(LineTiming.IdleLine(9600), longAgo);

        // Assert
        var elapsed = TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency);
        Assert.That(elapsed, Is.LessThan(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public async Task WaitAsyncDoesNothingForAZeroDelay()
    {
        // Arrange
        long start = Stopwatch.GetTimestamp();

        // Act
        await LineTiming.WaitAsync(TimeSpan.Zero, start);

        // Assert
        var elapsed = TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency);
        Assert.That(elapsed, Is.LessThan(TimeSpan.FromMilliseconds(1)));
    }
}
