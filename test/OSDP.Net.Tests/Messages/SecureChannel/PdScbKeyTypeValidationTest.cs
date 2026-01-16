using System;
using Moq;
using NUnit.Framework;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Messages.SecureChannel;

/// <summary>
/// Tests for PD-side SCB key type validation in osdp_CHLNG processing.
///
/// The PD must validate that it can fulfill the ACU's key type request:
/// - If ACU requests SCBK-D but PD has non-default key → NAK
/// - If ACU requests SCBK but PD has only default key → NAK
/// </summary>
[TestFixture]
[Category("Unit")]
public class PdScbKeyTypeValidationTest
{
    private static readonly byte[] NonDefaultKey =
        { 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x01, 0x02, 0x03, 0x04, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f };

    /// <summary>
    /// Verifies that when PD has SCBK and ACU requests SCBK, initialization succeeds.
    /// </summary>
    [Test]
    public void GivenPdHasSCBK_WhenAcuRequestsSCBK_ReturnsValidChallengeResponse()
    {
        // Arrange
        var channel = CreateTestChannel(NonDefaultKey);
        var command = BuildSessionChallengeMessage(requestDefaultKey: false);

        // Act
        var result = channel.TestHandleSessionChallenge(command);

        // Assert
        Assert.That(result, Is.TypeOf<ChallengeResponse>());
        var response = (ChallengeResponse)result;
        Assert.That(response.IsUsingDefaultKey, Is.False, "Response should indicate SCBK");
    }

    /// <summary>
    /// Verifies that when PD has SCBK-D and ACU requests SCBK-D, initialization succeeds.
    /// </summary>
    [Test]
    public void GivenPdHasSCBKD_WhenAcuRequestsSCBKD_ReturnsValidChallengeResponse()
    {
        // Arrange
        var channel = CreateTestChannel(SecurityContext.DefaultKey);
        var command = BuildSessionChallengeMessage(requestDefaultKey: true);

        // Act
        var result = channel.TestHandleSessionChallenge(command);

        // Assert
        Assert.That(result, Is.TypeOf<ChallengeResponse>());
        var response = (ChallengeResponse)result;
        Assert.That(response.IsUsingDefaultKey, Is.True, "Response should indicate SCBK-D");
    }

    /// <summary>
    /// Verifies that when PD has SCBK but ACU requests SCBK-D, PD returns NAK.
    /// This was already implemented before this fix.
    /// </summary>
    [Test]
    public void GivenPdHasSCBK_WhenAcuRequestsSCBKD_ReturnsNak()
    {
        // Arrange
        var channel = CreateTestChannel(NonDefaultKey);
        var command = BuildSessionChallengeMessage(requestDefaultKey: true);

        // Act
        var result = channel.TestHandleSessionChallenge(command);

        // Assert
        Assert.That(result, Is.TypeOf<Nak>());
        var nak = (Nak)result;
        Assert.That(nak.ErrorCode, Is.EqualTo(ErrorCode.DoesNotSupportSecurityBlock));
    }

    /// <summary>
    /// Verifies that when PD has only SCBK-D but ACU requests SCBK, PD returns NAK.
    /// This is the new validation added by this fix.
    /// </summary>
    [Test]
    public void GivenPdHasSCBKD_WhenAcuRequestsSCBK_ReturnsNak()
    {
        // Arrange
        var channel = CreateTestChannel(SecurityContext.DefaultKey);
        var command = BuildSessionChallengeMessage(requestDefaultKey: false);

        // Act
        var result = channel.TestHandleSessionChallenge(command);

        // Assert
        Assert.That(result, Is.TypeOf<Nak>());
        var nak = (Nak)result;
        Assert.That(nak.ErrorCode, Is.EqualTo(ErrorCode.DoesNotSupportSecurityBlock));
    }

    /// <summary>
    /// Creates a test channel with the specified security key.
    /// </summary>
    private static TestPdMessageSecureChannel CreateTestChannel(byte[] securityKey)
    {
        var mockConnection = new Mock<IOsdpConnection>();
        return new TestPdMessageSecureChannel(mockConnection.Object, securityKey);
    }

    /// <summary>
    /// Builds a mock osdp_CHLNG (SessionChallenge) incoming message.
    /// </summary>
    private static IncomingMessage BuildSessionChallengeMessage(bool requestDefaultKey)
    {
        // Build raw message bytes for osdp_CHLNG
        // Format: SOM + ADDR + LEN_LSB + LEN_MSB + CTRL + SEC_BLK + CMD + DATA + CRC

        var rndA = new byte[8]; // Server random number (RND.A)
        new Random(42).NextBytes(rndA);

        // Security Control Block for osdp_CHLNG (SCS_11):
        // Byte 0: Length (3)
        // Byte 1: Type (0x11 = BeginNewSecureConnectionSequence)
        // Byte 2: Key type (0x00 = SCBK-D, 0x01 = SCBK)
        byte scbKeyType = (byte)(requestDefaultKey ? 0x00 : 0x01);

        // Build the message
        // Header: SOM(1) + ADDR(1) + LEN(2) + CTRL(1) = 5 bytes
        // SCB: LEN(1) + TYPE(1) + DATA(1) = 3 bytes
        // CMD: 1 byte (0x76 = SessionChallenge)
        // Payload: 8 bytes (RND.A)
        // CRC: 2 bytes
        // Total: 5 + 3 + 1 + 8 + 2 = 19 bytes

        var message = new byte[19];
        message[0] = 0x53; // SOM
        message[1] = 0x00; // Address (command, so < 0x80)
        message[2] = 19;   // LEN LSB
        message[3] = 0x00; // LEN MSB
        message[4] = 0x0C; // CTRL: seq=0, CRC=1, SCB=1 (0x04 | 0x08)
        message[5] = 0x03; // SCB Length
        message[6] = 0x11; // SCB Type (BeginNewSecureConnectionSequence)
        message[7] = scbKeyType; // SCB Key Type
        message[8] = 0x76; // CMD (SessionChallenge)
        Array.Copy(rndA, 0, message, 9, 8); // RND.A payload

        // Calculate CRC
        var crc = CalculateCrc(message.AsSpan(0, 17));
        message[17] = (byte)(crc & 0xFF);
        message[18] = (byte)(crc >> 8);

        // Create IncomingMessage - include full message with SOM
        var mockChannel = new Mock<IMessageSecureChannel>();
        mockChannel.Setup(x => x.IsUsingDefaultKey).Returns(requestDefaultKey);
        mockChannel.Setup(x => x.IsSecurityEstablished).Returns(false);

        return new IncomingMessage(message.AsSpan(), mockChannel.Object);
    }

    /// <summary>
    /// Calculate CRC-16 for OSDP message.
    /// </summary>
    private static ushort CalculateCrc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0x1D0F;
        foreach (var b in data)
        {
            crc = (ushort)(((crc >> 8) | (crc << 8)) ^ b);
            crc ^= (byte)((crc & 0xFF) >> 4);
            crc ^= (ushort)((crc << 8) << 4);
            crc ^= (ushort)(((crc & 0xFF) << 4) << 1);
        }
        return crc;
    }

    /// <summary>
    /// Test wrapper for PdMessageSecureChannel that exposes protected methods.
    /// </summary>
    private class TestPdMessageSecureChannel : PdMessageSecureChannel
    {
        public TestPdMessageSecureChannel(IOsdpConnection connection, byte[] securityKey)
            : base(connection, securityKey)
        {
        }

        public PayloadData TestHandleSessionChallenge(IncomingMessage command)
        {
            return HandleSessionChallenge(command);
        }
    }
}
