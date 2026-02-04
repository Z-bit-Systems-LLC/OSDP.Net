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
/// Tests for PD-side Client UID (cUID) in osdp_CCRYPT response.
///
/// Per OSDP specification, the cUID in the challenge response should contain
/// the device's unique identifier (vendor code + serial number).
///
/// See GitHub issue #191: https://github.com/Z-bit-Systems-LLC/OSDP.Net/issues/191
/// </summary>
[TestFixture]
[Category("Unit")]
public class PdClientUIDTest
{
    private static readonly byte[] TestSecurityKey =
        { 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x01, 0x02, 0x03, 0x04, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f };

    /// <summary>
    /// Verifies that the ChallengeResponse includes the configured Client UID.
    /// </summary>
    [Test]
    public void GivenConfiguredClientUID_WhenSessionChallengeReceived_ChallengeResponseContainsClientUID()
    {
        // Arrange
        var expectedClientUID = new byte[] { 0x2C, 0x17, 0xE0, 0x04, 0x01, 0x29, 0x24, 0x4B };
        var channel = CreateTestChannel(TestSecurityKey, expectedClientUID);
        var command = BuildSessionChallengeMessage(requestDefaultKey: false);

        // Act
        var result = channel.TestHandleSessionChallenge(command);

        // Assert
        Assert.That(result, Is.TypeOf<ChallengeResponse>());
        var response = (ChallengeResponse)result;
        Assert.That(response.ClientUID, Is.EqualTo(expectedClientUID),
            "ChallengeResponse should contain the configured Client UID");
    }

    /// <summary>
    /// Verifies that ClientIdentification struct correctly produces the expected 8-byte cUID.
    /// </summary>
    [Test]
    public void GivenClientIdentification_WhenToBytesCalled_ReturnsCorrectFormat()
    {
        // Arrange
        var vendorCode = new byte[] { 0x2C, 0x17, 0xE0 };
        uint serialNumber = 0x4B242901; // Little-endian: 01 29 24 4B
        var identification = new ClientIdentification(vendorCode, serialNumber);

        // Act
        var clientUID = identification.ToBytes();

        // Assert
        // Format: VendorCode (3 bytes) + SerialNumber (4 bytes LE) + Padding (1 byte)
        Assert.That(clientUID.Length, Is.EqualTo(8));
        Assert.That(clientUID[0], Is.EqualTo(0x2C), "Vendor code byte 0");
        Assert.That(clientUID[1], Is.EqualTo(0x17), "Vendor code byte 1");
        Assert.That(clientUID[2], Is.EqualTo(0xE0), "Vendor code byte 2");
        Assert.That(clientUID[3], Is.EqualTo(0x01), "Serial number byte 0 (LSB)");
        Assert.That(clientUID[4], Is.EqualTo(0x29), "Serial number byte 1");
        Assert.That(clientUID[5], Is.EqualTo(0x24), "Serial number byte 2");
        Assert.That(clientUID[6], Is.EqualTo(0x4B), "Serial number byte 3 (MSB)");
        Assert.That(clientUID[7], Is.EqualTo(0x00), "Padding byte");
    }

    /// <summary>
    /// Verifies that Client UID must be exactly 8 bytes.
    /// </summary>
    [Test]
    public void GivenClientUIDWrongSize_WhenCreatingChannel_ThrowsArgumentException()
    {
        // Arrange
        var invalidClientUID = new byte[] { 0x01, 0x02, 0x03 }; // Only 3 bytes, should be 8

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CreateTestChannel(TestSecurityKey, invalidClientUID),
            "Creating channel with wrong-sized Client UID should throw ArgumentException");
    }

    /// <summary>
    /// Verifies that null Client UID throws ArgumentNullException.
    /// </summary>
    [Test]
    public void GivenNullClientUID_WhenCreatingChannel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CreateTestChannel(TestSecurityKey, null!),
            "Creating channel with null Client UID should throw ArgumentNullException");
    }

    /// <summary>
    /// Verifies that ClientIdentification requires exactly 3-byte vendor code.
    /// </summary>
    [Test]
    public void GivenInvalidVendorCodeLength_WhenCreatingClientIdentification_ThrowsArgumentException()
    {
        // Arrange
        var invalidVendorCode = new byte[] { 0x01, 0x02 }; // Only 2 bytes, should be 3

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _ = new ClientIdentification(invalidVendorCode, 12345),
            "Creating ClientIdentification with wrong-sized vendor code should throw ArgumentException");
    }

    /// <summary>
    /// Creates a test channel with the specified security key and Client UID.
    /// </summary>
    private static TestPdMessageSecureChannel CreateTestChannel(byte[] securityKey, byte[] clientUID)
    {
        var mockConnection = new Mock<IOsdpConnection>();
        return new TestPdMessageSecureChannel(mockConnection.Object, securityKey, clientUID);
    }

    /// <summary>
    /// Builds a mock osdp_CHLNG (SessionChallenge) incoming message.
    /// </summary>
    private static IncomingMessage BuildSessionChallengeMessage(bool requestDefaultKey)
    {
        var rndA = new byte[8];
        new Random(42).NextBytes(rndA);

        byte scbKeyType = (byte)(requestDefaultKey ? 0x00 : 0x01);

        var message = new byte[19];
        message[0] = 0x53; // SOM
        message[1] = 0x00; // Address
        message[2] = 19;   // LEN LSB
        message[3] = 0x00; // LEN MSB
        message[4] = 0x0C; // CTRL: seq=0, CRC=1, SCB=1
        message[5] = 0x03; // SCB Length
        message[6] = 0x11; // SCB Type (BeginNewSecureConnectionSequence)
        message[7] = scbKeyType; // SCB Key Type
        message[8] = 0x76; // CMD (SessionChallenge)
        Array.Copy(rndA, 0, message, 9, 8);

        var crc = CalculateCrc(message.AsSpan(0, 17));
        message[17] = (byte)(crc & 0xFF);
        message[18] = (byte)(crc >> 8);

        var mockChannel = new Mock<IMessageSecureChannel>();
        mockChannel.Setup(x => x.IsUsingDefaultKey).Returns(requestDefaultKey);
        mockChannel.Setup(x => x.IsSecurityEstablished).Returns(false);

        return new IncomingMessage(message.AsSpan(), mockChannel.Object);
    }

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
        public TestPdMessageSecureChannel(IOsdpConnection connection, byte[] securityKey, byte[] clientUID)
            : base(connection, securityKey, clientUID)
        {
        }

        public PayloadData TestHandleSessionChallenge(IncomingMessage command)
        {
            return HandleSessionChallenge(command);
        }
    }
}
