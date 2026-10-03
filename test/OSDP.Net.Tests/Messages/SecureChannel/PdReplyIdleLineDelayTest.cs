using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.Tests.Utilities;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tests.Messages.SecureChannel;

/// <summary>
/// Verifies the PD observes the idle line time OSDP v2.2.2 subclause 5.7 requires before it drives
/// the channel with a reply. Replying sooner beats an ACU's RS-485 transceiver turnaround, which
/// shows up on a real bus as the ACU retransmitting commands it never got a usable reply to.
/// The delay is always derived from the connection's line rate; it is not configurable.
/// </summary>
[TestFixture]
[Category("Unit")]
internal class PdReplyIdleLineDelayTest
{
    private const byte TestAddress = 0;

    private static readonly byte[] ClientUid = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

    private static byte[] BuildPollCommand(byte sequence) =>
        new OutgoingMessage(TestAddress, new Control(sequence, useCrc: true, hasSecurityControlBlock: false),
            new NoPayloadCommandData(CommandType.Poll)).BuildMessage(null);

    /// <summary>
    /// Drives one command/reply exchange over a loopback pair at the given line rate and returns how
    /// long the PD waited between the command landing and the reply appearing on the wire.
    /// </summary>
    private static async Task<TimeSpan> MeasureReplyDelay(int baudRate)
    {
        var (acuConnection, deviceConnection) = LoopbackOsdpConnection.CreatePair(baudRate);
        using (acuConnection)
        using (deviceConnection)
        {
            // Only the device side of the pair starts open; an unopened connection drops writes.
            await acuConnection.Open();

            var channel = new PdMessageSecureChannel(deviceConnection, securityKey: null, ClientUid)
            {
                Address = TestAddress,
                SecurityMode = SecurityMode.Unsecured
            };

            await acuConnection.WriteAsync(BuildPollCommand(1));

            // Timed from the moment the command is fully on the wire, which is when the line goes
            // idle and the PD's obligation to wait begins. The channel takes its own stamp inside
            // ReadNextCommand, so measuring from any later point understates the delay it applied.
            long lineWentIdle = Stopwatch.GetTimestamp();

            var command = await channel.ReadNextCommand();
            Assert.That(command, Is.Not.Null);

            await channel.SendReply(new OutgoingReply(command, new Ack()));

            // Read the reply back so the elapsed time covers the write path, not just the helper.
            var buffer = new byte[64];
            await acuConnection.ReadAsync(buffer, default);

            return TimeSpan.FromSeconds(
                (Stopwatch.GetTimestamp() - lineWentIdle) / (double)Stopwatch.Frequency);
        }
    }

    [Test]
    [TestCase(9600)]
    [TestCase(19200)]
    public async Task ReplyWaitsTwoCharacterTimesAtTheConnectionsLineRate(int baudRate)
    {
        // Arrange
        var expected = LineTiming.IdleLine(baudRate);

        // Act
        var elapsed = await MeasureReplyDelay(baudRate);

        // Assert
        Assert.That(elapsed, Is.GreaterThanOrEqualTo(expected),
            $"PD replied after {elapsed.TotalMilliseconds:F3} ms, inside the " +
            $"{expected.TotalMilliseconds:F3} ms idle line time required at {baudRate} baud");
    }

    [Test]
    public async Task FasterLineRateShortensTheDelay()
    {
        // Arrange - a first exchange absorbs JIT so the comparison reflects steady-state timing
        await MeasureReplyDelay(115200);

        // Act
        var slow = await MeasureReplyDelay(9600);
        var fast = await MeasureReplyDelay(115200);

        // Assert - two characters is 2.083 ms at 9600 but only 0.174 ms at 115200, so the delay must
        // track the line rate rather than being a fixed pause
        Assert.That(fast, Is.LessThan(slow));
        Assert.That(fast, Is.LessThan(LineTiming.IdleLine(9600)));
    }
}
