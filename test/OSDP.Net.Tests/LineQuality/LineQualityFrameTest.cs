using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.LineQuality;
using OSDP.Net.Messages;
using OSDP.Net.Model;

namespace OSDP.Net.Tests.LineQuality
{
    /// <summary>
    /// Byte-for-byte checks against the worked examples in section 3.9 of the OSDP Line Quality
    /// Test Procedure. These are the specification's own vectors, CRCs included, so a failure here
    /// means this implementation would not interoperate with any other conforming implementation.
    /// </summary>
    [TestFixture]
    public class LineQualityFrameTest
    {
        private const byte TestAddress = LineQualityProtocol.TestAddress;

        [Test]
        public void MinimalEchoRequest_MatchesSpecificationExample()
        {
            // Section 3.9 example 1: pattern 0xAA, zero-length payload, control byte SQN 1.
            var request = new EchoRequest(0x00, TestPattern.AlternatingA, 0);

            var frame = BuildCommand(request, sequence: 1);

            Assert.That(ToHex(frame), Is.EqualTo("53 7D 0F 00 05 80 02 00 0A 01 00 02 00 5E 6B"));
        }

        [Test]
        public void MinimalEchoResponse_MatchesSpecificationExample()
        {
            var command = ParseCommand(new EchoRequest(0x00, TestPattern.AlternatingA, 0), sequence: 1);

            var frame = BuildReply(command, new EchoResponse(0x00, Array.Empty<byte>()));

            Assert.That(ToHex(frame), Is.EqualTo("53 FD 0F 00 05 90 02 00 0A 01 00 00 00 29 9A"));
        }

        [Test]
        public void SequentialEchoRequest_MatchesSpecificationExample()
        {
            // Section 3.9 example 2: sequential pattern, 16 bytes, test sequence 0x07, SQN 2.
            var request = new EchoRequest(0x07, TestPattern.Sequential, 16);

            var frame = BuildCommand(request, sequence: 2);

            Assert.That(ToHex(frame), Is.EqualTo(
                "53 7D 1F 00 06 80 02 00 0A 01 07 04 10 " +
                "00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 03 5B"));
        }

        [Test]
        public void SequentialEchoResponse_MatchesSpecificationExample()
        {
            var request = new EchoRequest(0x07, TestPattern.Sequential, 16);
            var command = ParseCommand(request, sequence: 2);

            var frame = BuildReply(command, new EchoResponse(0x07, request.Data));

            Assert.That(ToHex(frame), Is.EqualTo(
                "53 FD 1F 00 06 90 02 00 0A 01 07 00 10 " +
                "00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 84 93"));
        }

        [Test]
        public void BaudRateChangeCommand_MatchesSpecificationExample()
        {
            // Section 3.9 example 3: switch to 115200, test sequence 0x08, SQN 3.
            var command = new BaudRateChange(0x08, LineQualityBaudRate.Baud115200);

            var frame = BuildCommand(command, sequence: 3);

            Assert.That(ToHex(frame), Is.EqualTo("53 7D 0E 00 07 80 02 00 0A 02 08 04 44 C7"));
        }

        [Test]
        public void BaudRateChangeAcknowledgment_MatchesSpecificationExample()
        {
            var command = ParseCommand(new BaudRateChange(0x08, LineQualityBaudRate.Baud115200), sequence: 3);

            var frame = BuildReply(command, new BaudRateChangeAck(0x08, BaudRateChangeStatus.Success));

            Assert.That(ToHex(frame), Is.EqualTo("53 FD 0E 00 07 90 02 00 0A 02 08 00 11 39"));
        }

        [Test]
        public void LargestPayload_ProducesExactlyTheGuaranteedBufferSize()
        {
            // Section 3.8: 113 bytes is chosen so command and reply are both exactly 128 bytes,
            // the smallest receive buffer OSDP section 5.6 guarantees.
            var request = new EchoRequest(0x00, TestPattern.Sequential, LineQualityProtocol.MaxPayloadLength);

            var commandFrame = BuildCommand(request, sequence: 1);
            var command = ParseCommand(request, sequence: 1);
            var replyFrame = BuildReply(command, new EchoResponse(0x00, request.Data));

            Assert.Multiple(() =>
            {
                Assert.That(commandFrame.Length, Is.EqualTo(128));
                Assert.That(replyFrame.Length, Is.EqualTo(128));
            });
        }

        [Test]
        public void EchoRequest_RoundTripsThroughTheWire()
        {
            var original = new EchoRequest(0x42, TestPattern.WalkingOne, 48);
            var command = ParseCommand(original, sequence: 1);

            Assert.That(EchoRequest.TryParse(command.Payload.AsSpan(), out var parsed), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(parsed.SequenceNumber, Is.EqualTo(0x42));
                Assert.That(parsed.Pattern, Is.EqualTo(TestPattern.WalkingOne));
                Assert.That(parsed.DeclaredPayloadLength, Is.EqualTo(48));
                Assert.That(parsed.IsLengthConsistent, Is.True);
                Assert.That(parsed.Data, Is.EqualTo(original.Data));
            });
        }

        [Test]
        public void ReplyAddress_CarriesTheReplyFlag()
        {
            var command = ParseCommand(new EchoRequest(0, TestPattern.AllZeros, 0), sequence: 1);

            var frame = BuildReply(command, new EchoResponse(0, Array.Empty<byte>()));

            // 0x7D | 0x80 = 0xFD, per section 3.1.
            Assert.That(frame[1], Is.EqualTo(0xFD));
        }

        /// <summary>Builds a command frame and strips the leading driver byte.</summary>
        private static byte[] BuildCommand(PayloadData payload, byte sequence)
        {
            var message = new OutgoingMessage(TestAddress, new Control(sequence, true, false), payload)
                .BuildMessage(ClearTextChannel.Instance);

            // BuildMessage prepends 0xFF so line drivers can sense an idle bus; it is not part of
            // the packet the specification's examples describe.
            Assert.That(message[0], Is.EqualTo(0xFF));
            return message.Skip(1).ToArray();
        }

        private static byte[] BuildReply(IncomingMessage command, PayloadData payload)
        {
            var message = new OutgoingReply(command, payload).BuildMessage(ClearTextChannel.Instance);
            return message.Skip(1).ToArray();
        }

        private static IncomingMessage ParseCommand(PayloadData payload, byte sequence) =>
            new IncomingMessage(BuildCommand(payload, sequence), ClearTextChannel.Instance);

        private static string ToHex(byte[] data) =>
            string.Join(" ", data.Select(value => value.ToString("X2")));
    }
}
