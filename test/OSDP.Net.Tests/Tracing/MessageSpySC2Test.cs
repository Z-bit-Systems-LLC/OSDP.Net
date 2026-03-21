using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.Model;
using OSDP.Net.Tracing;

namespace OSDP.Net.Tests.Tracing;

[TestFixture]
[Category("Unit")]
public class MessageSpySC2Test
{
    // Test vectors matching SC2MessageTest
    private static readonly byte[] TestSCBK = HexToBytes(
        "303132333435363738393A3B3C3D3E3F404142434445464748494A4B4C4D4E4F");

    private static readonly byte[] TestRndA = HexToBytes("A0A1A2A3A4A5A6A7A8A9AAABACADAEAF");
    private static readonly byte[] TestRndB = HexToBytes("B0B1B2B3B4B5B6B7B8B9BABBBCBDBEBF");
    private static readonly byte[] TestCUID = HexToBytes("C0C1C2C3C4C5C6C7");

    // Encrypted test vectors (from SC2MessageTest)
    private static readonly byte[] Counter0TXEncrypted = HexToBytes(
        "53001A000D021780198CBFBF8DEAE07AF582C05744F9890EFF00");

    private static readonly byte[] Counter1RXEncrypted = HexToBytes(
        "53801A000D021877294E82C8D37790496B94F94E6D580E8C0EF4");

    private static readonly byte[] Counter2TXEncrypted = HexToBytes(
        "53001A000E021762FD900209F99284CA87EBD3DC1D58333C1670");

    private static readonly byte[] Counter3RXEncrypted = HexToBytes(
        "53801A000E02185E95A072CC9F228A8BB0846FC21F2C850E1558");

    /// <summary>
    /// Builds a complete OSDP packet using OutgoingMessage, stripping the driver byte.
    /// </summary>
    private static byte[] BuildHandshakePacket(byte address, byte sequence, PayloadData payload)
    {
        var control = new Control(sequence, true, false);
        var msg = new OutgoingMessage(address, control, payload);
        var bytes = msg.BuildMessage(null);
        // Strip driver byte (0xFF) at position 0
        return bytes.AsSpan(1).ToArray();
    }

    /// <summary>
    /// Runs the full SC2 handshake on a MessageSpy and returns it ready for encrypted messages.
    /// </summary>
    private static MessageSpy CreateHandshakedSpy()
    {
        var spy = new MessageSpy(TestSCBK);

        // Step 1: CHLNG (ACU → PD)
        var chlngPacket = BuildHandshakePacket(0x00, 0,
            new SecurityInitialization(TestRndA, SecureChannelVersion.V2));
        spy.ParseCommand(chlngPacket);

        // Step 2: CCRYPT (PD → ACU)
        // Compute the client cryptogram using a temporary SC2 context
        var tempContext = new SC2SecurityContext(TestSCBK);
        tempContext.DeriveSessionKeys(TestRndA, TestRndB);
        var clientCryptogram = tempContext.ComputeCryptogram(TestRndA, TestRndB);
        var ccryptPacket = BuildHandshakePacket(0x80, 0,
            new ChallengeResponse(TestCUID, TestRndB, clientCryptogram, false));
        spy.ParseReply(ccryptPacket);

        // Step 3: SCRYPT (ACU → PD)
        var serverCryptogram = tempContext.ComputeCryptogram(TestRndB, TestRndA);
        var scryptPacket = BuildHandshakePacket(0x00, 1,
            new ServerCryptogramData(serverCryptogram, SecureChannelVersion.V2));
        spy.ParseCommand(scryptPacket);

        // Step 4: RMAC_I (PD → ACU)
        var rmacPacket = BuildHandshakePacket(0x80, 1,
            new InitialRMac(Array.Empty<byte>(), true));
        spy.ParseReply(rmacPacket);

        return spy;
    }

    [Test]
    public void Constructor_WithSC2Key_ShouldCreateInstance()
    {
        var spy = new MessageSpy(TestSCBK);
        Assert.That(spy, Is.Not.Null);
    }

    [Test]
    public void SC2Handshake_CHLNG_ShouldDetectSC2()
    {
        var spy = new MessageSpy(TestSCBK);
        var chlngPacket = BuildHandshakePacket(0x00, 0,
            new SecurityInitialization(TestRndA, SecureChannelVersion.V2));

        var message = spy.ParseCommand(chlngPacket);

        Assert.That((CommandType)message.Type, Is.EqualTo(CommandType.SessionChallenge));
        // SC2 CHLNG has 16-byte RndA payload
        Assert.That(message.Payload.Length, Is.EqualTo(16));
    }

    [Test]
    public void SC2Handshake_CCRYPT_ShouldParseCorrectly()
    {
        var spy = new MessageSpy(TestSCBK);

        // CHLNG first
        var chlngPacket = BuildHandshakePacket(0x00, 0,
            new SecurityInitialization(TestRndA, SecureChannelVersion.V2));
        spy.ParseCommand(chlngPacket);

        // CCRYPT
        var tempContext = new SC2SecurityContext(TestSCBK);
        tempContext.DeriveSessionKeys(TestRndA, TestRndB);
        var clientCryptogram = tempContext.ComputeCryptogram(TestRndA, TestRndB);
        var ccryptPacket = BuildHandshakePacket(0x80, 0,
            new ChallengeResponse(TestCUID, TestRndB, clientCryptogram, false));

        var message = spy.ParseReply(ccryptPacket);

        Assert.That((ReplyType)message.Type, Is.EqualTo(ReplyType.CrypticData));
        // SC2 CCRYPT: cUID(8) + RndB(16) + clientCryptogram(32) = 56 bytes
        Assert.That(message.Payload.Length, Is.EqualTo(56));
    }

    [Test]
    public void SC2_AfterHandshake_DecryptsEncryptedPoll()
    {
        var spy = CreateHandshakedSpy();

        var pollResult = spy.ParseCommand(Counter0TXEncrypted);

        Assert.Multiple(() =>
        {
            Assert.That(pollResult.Type, Is.EqualTo((byte)CommandType.Poll),
                "Decrypted type should be Poll (0x60)");
            Assert.That(pollResult.Payload, Is.Empty,
                "Poll has no payload");
            Assert.That(pollResult.IsValidMac, Is.True,
                "GCM authentication should pass");
            Assert.That(pollResult.IsDataCorrect, Is.True,
                "CRC should be valid");
        });
    }

    [Test]
    public void SC2_AfterHandshake_DecryptsEncryptedAck()
    {
        var spy = CreateHandshakedSpy();

        // Counter 0: Parse Poll first (increments counter)
        spy.ParseCommand(Counter0TXEncrypted);

        // Counter 1: Parse Ack
        var ackResult = spy.ParseReply(Counter1RXEncrypted);

        Assert.Multiple(() =>
        {
            Assert.That(ackResult.Type, Is.EqualTo((byte)ReplyType.Ack),
                "Decrypted type should be Ack (0x40)");
            Assert.That(ackResult.Payload, Is.Empty,
                "Ack has no payload");
            Assert.That(ackResult.IsValidMac, Is.True,
                "GCM authentication should pass");
            Assert.That(ackResult.IsDataCorrect, Is.True,
                "CRC should be valid");
        });
    }

    [Test]
    public void SC2_FullSequence_DecryptsMultiplePackets()
    {
        var spy = CreateHandshakedSpy();

        // Counter 0: Poll
        var poll0 = spy.ParseCommand(Counter0TXEncrypted);
        Assert.That(poll0.Type, Is.EqualTo((byte)CommandType.Poll));

        // Counter 1: Ack
        var ack1 = spy.ParseReply(Counter1RXEncrypted);
        Assert.That(ack1.Type, Is.EqualTo((byte)ReplyType.Ack));

        // Counter 2: Poll
        var poll2 = spy.ParseCommand(Counter2TXEncrypted);
        Assert.That(poll2.Type, Is.EqualTo((byte)CommandType.Poll));

        // Counter 3: Ack
        var ack3 = spy.ParseReply(Counter3RXEncrypted);
        Assert.That(ack3.Type, Is.EqualTo((byte)ReplyType.Ack));
    }

    [Test]
    public void SC2_ParsePacket_AutoDetectsDirectionAndDecrypts()
    {
        var spy = CreateHandshakedSpy();

        // ParsePacket auto-detects command vs reply from address byte
        var pollPacket = spy.ParsePacket(Counter0TXEncrypted);
        Assert.That(pollPacket.CommandType, Is.EqualTo(CommandType.Poll));

        var ackPacket = spy.ParsePacket(Counter1RXEncrypted);
        Assert.That(ackPacket.ReplyType, Is.EqualTo(ReplyType.Ack));
    }

    [Test]
    public void SC2_TryParsePacket_ReturnsTrue()
    {
        var spy = CreateHandshakedSpy();

        Assert.That(spy.TryParsePacket(Counter0TXEncrypted, out var packet), Is.True);
        Assert.That(packet, Is.Not.Null);
        Assert.That(packet.CommandType, Is.EqualTo(CommandType.Poll));
    }

    [Test]
    public void SC2_WithoutKey_DoesNotCrash()
    {
        // SC2 handshake without the correct key — spy should handle gracefully
        var spy = new MessageSpy(); // no key

        var chlngPacket = BuildHandshakePacket(0x00, 0,
            new SecurityInitialization(TestRndA, SecureChannelVersion.V2));
        spy.ParseCommand(chlngPacket);

        // CCRYPT without SC2 key derivation (key is null, so SC2 channel won't be created)
        var tempContext = new SC2SecurityContext(TestSCBK);
        tempContext.DeriveSessionKeys(TestRndA, TestRndB);
        var clientCryptogram = tempContext.ComputeCryptogram(TestRndA, TestRndB);
        var ccryptPacket = BuildHandshakePacket(0x80, 0,
            new ChallengeResponse(TestCUID, TestRndB, clientCryptogram, false));
        spy.ParseReply(ccryptPacket);

        // Without a key, TryParsePacket still succeeds but the payload won't be decrypted
        // (falls through to SC1 path which parses the packet structure without decryption)
        Assert.That(spy.TryParsePacket(Counter0TXEncrypted, out var packet), Is.True);
        Assert.That(packet, Is.Not.Null);
    }

    [Test]
    public void SC2_SessionReset_HandlesNewHandshake()
    {
        var spy = CreateHandshakedSpy();

        // Verify initial session works
        var poll0 = spy.ParseCommand(Counter0TXEncrypted);
        Assert.That(poll0.Type, Is.EqualTo((byte)CommandType.Poll));

        // Start a new CHLNG (session reset)
        var newRndA = HexToBytes("1011121314151617181920212223242526");
        // Just verify it doesn't crash
        var chlngPacket = BuildHandshakePacket(0x00, 0,
            new SecurityInitialization(newRndA.Take(16).ToArray(), SecureChannelVersion.V2));
        var chlngResult = spy.ParseCommand(chlngPacket);
        Assert.That((CommandType)chlngResult.Type, Is.EqualTo(CommandType.SessionChallenge));
    }

    private static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }
}
