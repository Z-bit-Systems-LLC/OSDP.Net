using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Model.ReplyData;

[TestFixture]
[Category("Unit")]
public class InitialRMacTest
{
    [Test]
    public void SecurityControlBlock_WhenAccepted_UsesScs14WithSuccessData()
    {
        var reply = new InitialRMac(new byte[16], serverCryptogramAccepted: true);

        Assert.That(reply.SecurityControlBlock().ToArray(), Is.EqualTo(new byte[] { 0x03, 0x14, 0x01 }));
    }

    [Test]
    public void SecurityControlBlock_WhenRejected_UsesScs14WithFailureMarker()
    {
        var reply = new InitialRMac(new byte[16], serverCryptogramAccepted: false);

        Assert.That(reply.SecurityControlBlock().ToArray(), Is.EqualTo(new byte[] { 0x03, 0x14, 0xFF }));
    }

    [Test]
    public void Code_IsInitialRMac()
    {
        var reply = new InitialRMac(new byte[16], serverCryptogramAccepted: true);

        Assert.That(reply.Code, Is.EqualTo((byte)ReplyType.InitialRMac));
    }
}
