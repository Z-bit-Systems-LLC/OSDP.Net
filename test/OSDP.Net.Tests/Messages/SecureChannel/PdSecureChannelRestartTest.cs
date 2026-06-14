using System.Linq;
using Moq;
using NUnit.Framework;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Tracing;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tests.Messages.SecureChannel;

/// <summary>
/// Tests for the PD dropping a stale secure channel session when the ACU restarts the connection
/// (a command with sequence number 0). This lets the ACU re-discover and re-establish the secure
/// channel - e.g. after osdp_KEYSET - without the PD resetting the session off the back of KEYSET.
/// </summary>
[TestFixture]
[Category("Unit")]
public class PdSecureChannelRestartTest
{
    // osdp_POLL with CRC: sequence 0 (control byte 0x04) and sequence 1 (control byte 0x05).
    private const string ClearPollSequenceZero = "53-00-08-00-04-60-00-C0-B9";
    private const string ClearPollSequenceOne = "53-00-08-00-05-60-00-00-00";

    private static IncomingMessage ParseCommand(string hex) =>
        new MessageSpy().ParseCommand(BinaryUtils.HexToBytes(hex).ToArray());

    [Test]
    public void DropStaleSecureSession_ClearSequenceZeroOnEstablishedSession_DropsSession()
    {
        var channel = new TestChannel();
        channel.ForceSecurityEstablished();

        channel.DropStaleSecureSessionOnConnectionRestart(ParseCommand(ClearPollSequenceZero));

        Assert.That(channel.IsSecurityEstablished, Is.False);
    }

    [Test]
    public void DropStaleSecureSession_ClearNonZeroSequenceOnEstablishedSession_KeepsSession()
    {
        var channel = new TestChannel();
        channel.ForceSecurityEstablished();

        channel.DropStaleSecureSessionOnConnectionRestart(ParseCommand(ClearPollSequenceOne));

        Assert.That(channel.IsSecurityEstablished, Is.True);
    }

    [Test]
    public void DropStaleSecureSession_ClearSequenceZeroWhenNotEstablished_RemainsNotEstablished()
    {
        var channel = new TestChannel();

        channel.DropStaleSecureSessionOnConnectionRestart(ParseCommand(ClearPollSequenceZero));

        Assert.That(channel.IsSecurityEstablished, Is.False);
    }

    private sealed class TestChannel : PdMessageSecureChannel
    {
        public TestChannel()
            : base(new Mock<IOsdpConnection>().Object, SecurityContext.DefaultKey, new byte[8])
        {
        }

        public void ForceSecurityEstablished() => Context.IsSecurityEstablished = true;
    }
}
