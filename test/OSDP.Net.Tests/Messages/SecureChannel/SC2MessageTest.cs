using System;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Tests.Messages.SecureChannel;

[TestFixture]
[Category("Unit")]
public class SC2MessageTest
{
    // Test vectors from the SC2 specification
    private static readonly byte[] TestSCBK = HexToBytes(
        "303132333435363738393A3B3C3D3E3F404142434445464748494A4B4C4D4E4F");

    private static readonly byte[] TestRndA = HexToBytes("A0A1A2A3A4A5A6A7A8A9AAABACADAEAF");
    private static readonly byte[] TestRndB = HexToBytes("B0B1B2B3B4B5B6B7B8B9BABBBCBDBEBF");
    private static readonly byte[] TestCUID = HexToBytes("C0C1C2C3C4C5C6C7");

    // Counter 0 TX (Poll command): plain = 53 00 08 00 05 60 DA 99
    private static readonly byte[] Counter0TXEncrypted = HexToBytes(
        "53001A000D021780198CBFBF8DEAE07AF582C05744F9890EFF00");

    // Counter 1 RX (Ack reply): plain = 53 80 08 00 05 40 68 9F
    private static readonly byte[] Counter1RXEncrypted = HexToBytes(
        "53801A000D021877294E82C8D37790496B94F94E6D580E8C0EF4");

    // Counter 2 TX
    private static readonly byte[] Counter2TXEncrypted = HexToBytes(
        "53001A000E021762FD900209F99284CA87EBD3DC1D58333C1670");

    // Counter 3 RX
    private static readonly byte[] Counter3RXEncrypted = HexToBytes(
        "53801A000E02185E95A072CC9F228A8BB0846FC21F2C850E1558");

    // Header bytes (AAD) for each test vector: SOM + ADDR + LEN(2) + CTRL + SCB(2)
    private const int HeaderSize = 7; // 5 (SOM+ADDR+LEN+CTRL) + 2 (SCB)

    private SC2SecurityContext CreateInitializedContext()
    {
        var context = new SC2SecurityContext(TestSCBK);
        // Override the server random to match test vector
        Array.Copy(TestRndA, context.ServerRandomNumber, TestRndA.Length);
        context.ClientUID = (byte[])TestCUID.Clone();
        context.DeriveSessionKeys(TestRndA, TestRndB);
        context.Establish();
        return context;
    }

    [Test]
    public void Counter0TX_PollCommand_MatchesTestVector()
    {
        var context = CreateInitializedContext();
        var channel = new SC2ACUMessageSecureChannel(context);

        // Poll command: code=0x60, no payload
        var plaintext = new byte[] { 0x60 }; // command byte only
        var aad = Counter0TXEncrypted.AsSpan(0, HeaderSize);

        var ciphertext = new byte[plaintext.Length];
        channel.EncodePayload(plaintext, ciphertext, aad);

        // Verify ciphertext byte (index 7 of the full message)
        Assert.That(ciphertext[0], Is.EqualTo(Counter0TXEncrypted[7]),
            $"Ciphertext mismatch: expected 0x{Counter0TXEncrypted[7]:X2}, got 0x{ciphertext[0]:X2}");

        // Verify GCM tag
        var tag = channel.GenerateMac(ReadOnlySpan<byte>.Empty, false);
        var expectedTag = Counter0TXEncrypted.AsSpan(8, 16).ToArray();
        Assert.That(tag.ToArray(), Is.EqualTo(expectedTag), "GCM tag mismatch for counter 0 TX");
    }

    [Test]
    public void Counter1RX_AckReply_DecryptsCorrectly()
    {
        var context = CreateInitializedContext();
        var channel = new SC2ACUMessageSecureChannel(context);

        // First, skip counter 0 (was used by TX)
        context.IncrementCounter();

        // Extract ciphertext + tag from test vector: bytes 7..(len-2) exclude CRC
        var ciphertextWithTag = Counter1RXEncrypted.AsSpan(7, Counter1RXEncrypted.Length - 7 - 2).ToArray();
        var aad = Counter1RXEncrypted.AsSpan(0, HeaderSize);

        var decrypted = channel.DecodePayload(ciphertextWithTag, aad);

        Assert.Multiple(() =>
        {
            Assert.That(decrypted[0], Is.EqualTo(0x40), "Decrypted reply code should be 0x40 (Ack)");
            Assert.That(decrypted.Length, Is.EqualTo(1), "Ack has no payload beyond the reply code");
        });
    }

    [Test]
    public void FullSequence_Counter0Through3_MatchesTestVectors()
    {
        var context = CreateInitializedContext();
        var channel = new SC2ACUMessageSecureChannel(context);

        // Counter 0: TX Poll (0x60)
        {
            var plaintext = new byte[] { 0x60 };
            var ciphertext = new byte[1];
            var aad = Counter0TXEncrypted.AsSpan(0, HeaderSize);
            channel.EncodePayload(plaintext, ciphertext, aad);
            var tag = channel.GenerateMac(ReadOnlySpan<byte>.Empty, false).ToArray();

            Assert.That(ciphertext[0], Is.EqualTo(Counter0TXEncrypted[7]),
                "Counter 0 TX ciphertext mismatch");
            Assert.That(tag, Is.EqualTo(Counter0TXEncrypted.AsSpan(8, 16).ToArray()),
                "Counter 0 TX tag mismatch");
        }

        // Counter 1: RX Ack (0x40)
        {
            var ciphertextWithTag = Counter1RXEncrypted.AsSpan(7, Counter1RXEncrypted.Length - 7 - 2).ToArray();
            var aad = Counter1RXEncrypted.AsSpan(0, HeaderSize);
            var decrypted = channel.DecodePayload(ciphertextWithTag, aad);
            Assert.That(decrypted[0], Is.EqualTo(0x40), "Counter 1 RX decrypted code mismatch");
        }

        // Counter 2: TX Poll (0x60)
        {
            var plaintext = new byte[] { 0x60 };
            var ciphertext = new byte[1];
            var aad = Counter2TXEncrypted.AsSpan(0, HeaderSize);
            channel.EncodePayload(plaintext, ciphertext, aad);
            var tag = channel.GenerateMac(ReadOnlySpan<byte>.Empty, false).ToArray();

            Assert.That(ciphertext[0], Is.EqualTo(Counter2TXEncrypted[7]),
                "Counter 2 TX ciphertext mismatch");
            Assert.That(tag, Is.EqualTo(Counter2TXEncrypted.AsSpan(8, 16).ToArray()),
                "Counter 2 TX tag mismatch");
        }

        // Counter 3: RX Ack (0x40)
        {
            var ciphertextWithTag = Counter3RXEncrypted.AsSpan(7, Counter3RXEncrypted.Length - 7 - 2).ToArray();
            var aad = Counter3RXEncrypted.AsSpan(0, HeaderSize);
            var decrypted = channel.DecodePayload(ciphertextWithTag, aad);
            Assert.That(decrypted[0], Is.EqualTo(0x40), "Counter 3 RX decrypted code mismatch");
        }
    }

    [Test]
    public void OutgoingMessage_SC2Poll_ProducesCorrectFormat()
    {
        var context = CreateInitializedContext();
        var channel = new SC2ACUMessageSecureChannel(context);

        // Build a Poll command through OutgoingMessage
        var payloadData = new OSDP.Net.Model.CommandData.NoPayloadCommandData(CommandType.Poll);
        var control = new Control(5, true, true); // seq=5, CRC, security
        var outgoing = new OutgoingMessage(0x00, control, payloadData);

        var message = outgoing.BuildMessage(channel);

        // message includes driver byte at [0]
        var messageWithoutDriver = message.AsSpan(1).ToArray();

        // Verify against test vector (Counter 0 TX)
        Assert.That(messageWithoutDriver, Is.EqualTo(Counter0TXEncrypted),
            "Full SC2 Poll message mismatch with test vector");
    }

    [Test]
    public void IncomingMessage_SC2Ack_ParsesCorrectly()
    {
        var context = CreateInitializedContext();
        var channel = new SC2ACUMessageSecureChannel(context);

        // Skip counter 0 (used by TX)
        context.IncrementCounter();

        var incoming = new IncomingMessage(Counter1RXEncrypted, channel);

        Assert.Multiple(() =>
        {
            Assert.That(incoming.Type, Is.EqualTo(0x40), "Reply type should be Ack (0x40)");
            Assert.That(incoming.Payload, Is.Empty, "Ack should have empty payload");
            Assert.That(incoming.IsValidMac, Is.True, "GCM validation should pass");
            Assert.That(incoming.IsDataCorrect, Is.True, "CRC should be valid");
        });
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
