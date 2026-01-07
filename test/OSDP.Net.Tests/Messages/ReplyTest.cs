using NUnit.Framework;
using OSDP.Net.Messages;
using System;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Messages
{
    [TestFixture]
    [Category("Unit")]
    public class ReplyTest
    {
        [Test]
        public void ParseAckToPollCommandTest()
        {
            const byte address = 0;

            var controlBlock = new Control(0, true, false);
            var command = new OutgoingMessage(address, controlBlock, new IdReport());
            var device = new DeviceProxy(address, true, false);
            var connectionId = new Guid();

            // Raw bytes taken off the wire from actual reader responding to poll
            byte[] rawResponse = new byte[] {83, 128, 8, 0, 5, 64, 104, 159};

            var reply = new ReplyTracker(connectionId, new IncomingMessage(rawResponse, new ACUMessageSecureChannel()), command, device);

            Assert.That(reply.ReplyMessage.Type, Is.EqualTo((byte)ReplyType.Ack));
            Assert.That(reply.IsValidReply, Is.True);
            Assert.That(reply.ReplyMessage.Sequence, Is.EqualTo(1));
            Assert.That(reply.ReplyMessage.Address, Is.EqualTo(address));
        }
    }
}