using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.Tracing;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tests.Tracing;

[TestFixture]
[Category("Unit")]
public class MessageSpyTest
{
    [TestFixture]
    public class ConstructorTest
    {
        [Test]
        public void Constructor_WithoutSecurityKey_ShouldCreateInstance()
        {
            // Act
            var spy = new MessageSpy();

            // Assert
            Assert.That(spy, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithSecurityKey_ShouldCreateInstance()
        {
            // Arrange
            var securityKey = new byte[16];

            // Act
            var spy = new MessageSpy(securityKey);

            // Assert
            Assert.That(spy, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullSecurityKey_ShouldCreateInstance()
        {
            // Act
            var spy = new MessageSpy(null);

            // Assert
            Assert.That(spy, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithEmptySecurityKey_ShouldCreateInstance()
        {
            // Arrange
            var securityKey = Array.Empty<byte>();

            // Act
            var spy = new MessageSpy(securityKey);

            // Assert
            Assert.That(spy, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithValidSecurityKey_ShouldCreateInstance()
        {
            // Arrange
            var securityKey = new byte[16];
            Array.Fill(securityKey, (byte)0x30);

            // Act
            var spy = new MessageSpy(securityKey);

            // Assert
            Assert.That(spy, Is.Not.Null);
        }
    }

    [TestFixture]
    public class PeekAddressByteTest
    {
        [Test]
        public void PeekAddressByte_ValidCommandData_ShouldReturnCorrectAddress()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-00-0D-00-06-6A").ToArray();

            // Act
            var address = spy.PeekAddressByte(data);

            // Assert
            Assert.That(address, Is.EqualTo(0x00));
        }

        [Test]
        public void PeekAddressByte_ReplyData_ShouldReturnCorrectAddress()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-80-14-00-06-45").ToArray();

            // Act
            var address = spy.PeekAddressByte(data);

            // Assert
            Assert.That(address, Is.EqualTo(0x80));
        }

        [TestCase("53-00-0D-00-06-6A", (byte)0x00)]
        [TestCase("53-01-0D-00-06-6A", (byte)0x01)]
        [TestCase("53-05-0D-00-06-6A", (byte)0x05)]
        [TestCase("53-7E-0D-00-06-6A", (byte)0x7E)]
        [TestCase("53-7F-0D-00-06-6A", (byte)0x7F)]
        [TestCase("53-80-14-00-06-45", (byte)0x80)]
        [TestCase("53-FF-14-00-06-45", (byte)0xFF)]
        public void PeekAddressByte_VariousAddresses_ShouldReturnCorrectAddress(string hexData, byte expectedAddress)
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes(hexData).ToArray();

            // Act
            var address = spy.PeekAddressByte(data);

            // Assert
            Assert.That(address, Is.EqualTo(expectedAddress));
        }

        [Test]
        public void PeekAddressByte_MinimumValidData_ShouldReturnAddress()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = new byte[] { 0x53, 0x42 }; // Minimum data with SOM and address

            // Act
            var address = spy.PeekAddressByte(data);

            // Assert
            Assert.That(address, Is.EqualTo(0x42));
        }
    }

    [TestFixture]
    public class ParseCommandTest
    {
        [Test]
        public void ParseCommand_Poll_ShouldParseCorrectly()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-00-08-00-04-60-00-C0-B9").ToArray();

            // Act
            var message = spy.ParseCommand(data);

            // Assert
            Assert.That(message.Address, Is.EqualTo(0));
            Assert.That((CommandType)message.Type, Is.EqualTo(CommandType.Poll));
        }

        [Test]
        public void ParseCommand_BuzzerControl_ShouldParseCorrectly()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();

            // Act
            var message = spy.ParseCommand(data);

            // Assert
            Assert.That(message.Address, Is.EqualTo(0));
            Assert.That((CommandType)message.Type, Is.EqualTo(CommandType.BuzzerControl));
            Assert.That(message.Payload.Length, Is.GreaterThan(0));
        }

        [Test]
        public void ParseCommand_IdReport_ShouldParseCorrectly()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-00-09-00-05-61-00-72-C0").ToArray();

            // Act
            var message = spy.ParseCommand(data);

            // Assert
            Assert.That(message.Address, Is.EqualTo(0));
            Assert.That((CommandType)message.Type, Is.EqualTo(CommandType.IdReport));
        }

        [Test]
        public void ParseCommand_MultipleCalls_ShouldExtractSequenceFromData()
        {
            // Arrange
            var spy = new MessageSpy();
            var data1 = BinaryUtils.HexToBytes("53-00-08-00-04-60-00-C0-B9").ToArray();
            var data2 = BinaryUtils.HexToBytes("53-00-08-01-04-60-01-32-98").ToArray();
            var data3 = BinaryUtils.HexToBytes("53-00-08-02-04-60-02-A4-EB").ToArray();

            // Act
            var message1 = spy.ParseCommand(data1);
            var message2 = spy.ParseCommand(data2);
            var message3 = spy.ParseCommand(data3);

            // Assert - The sequence is read from the packet itself, not tracked by the spy
            Assert.That(message1.Sequence, Is.EqualTo(0));
            Assert.That(message2.Sequence, Is.EqualTo(0));  // Sequence comes from packet byte 3
            Assert.That(message3.Sequence, Is.EqualTo(0));  // Each has sequence 0 in the control byte
        }

        [Test]
        public void ParseCommand_DifferentAddresses_ShouldParseCorrectly()
        {
            // Arrange
            var spy = new MessageSpy();
            var addresses = new byte[] { 0x00, 0x01, 0x05, 0x7E };

            foreach (var addr in addresses)
            {
                var data = new byte[] { 0x53, addr, 0x08, 0x00, 0x04, 0x60, 0x00, 0x00, 0x00 };

                // Act
                var message = spy.ParseCommand(data);

                // Assert
                Assert.That(message.Address, Is.EqualTo(addr));
            }
        }

        [Test]
        public void ParseCommand_WithCrc_ShouldIndicateCrcUsage()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();

            // Act
            var message = spy.ParseCommand(data);

            // Assert
            Assert.That(message.IsUsingCrc, Is.True);
        }

        [Test]
        public void ParseCommand_SessionChallenge_ShouldReturnCorrectType()
        {
            // Arrange
            var securityKey = new byte[16];
            Array.Fill(securityKey, (byte)0x30);
            var spy = new MessageSpy(securityKey);
            var data = BinaryUtils.HexToBytes("53-00-10-00-04-76-11-22-33-44-55-66-77-88-EF-4E").ToArray();

            // Act
            var message = spy.ParseCommand(data);

            // Assert
            Assert.That((CommandType)message.Type, Is.EqualTo(CommandType.SessionChallenge));
            Assert.That(message.Payload.Length, Is.EqualTo(8));
        }

        [Test]
        public void ParseCommand_ServerCryptogram_AfterSessionChallenge_ShouldReturnCorrectType()
        {
            // Arrange
            var securityKey = new byte[16];
            Array.Fill(securityKey, (byte)0x30);
            var spy = new MessageSpy(securityKey);

            var challengeData = BinaryUtils.HexToBytes("53-00-10-00-04-76-11-22-33-44-55-66-77-88-EF-4E").ToArray();
            spy.ParseCommand(challengeData);

            var scryptData = BinaryUtils.HexToBytes("53-00-18-00-04-77-11-22-33-44-55-66-77-88-11-22-33-44-55-66-77-88-12-34").ToArray();

            // Act
            var message = spy.ParseCommand(scryptData);

            // Assert
            Assert.That((CommandType)message.Type, Is.EqualTo(CommandType.ServerCryptogram));
        }

        [Test]
        public void ParseCommand_WithoutSecurityKey_SessionChallenge_ShouldStillParse()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-00-10-00-04-76-11-22-33-44-55-66-77-88-EF-4E").ToArray();

            // Act
            var message = spy.ParseCommand(data);

            // Assert
            Assert.That((CommandType)message.Type, Is.EqualTo(CommandType.SessionChallenge));
            Assert.That(message, Is.Not.Null);
        }
    }

    [TestFixture]
    public class ParseReplyTest
    {
        [Test]
        public void ParseReply_Ack_ShouldParseCorrectly()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-80-09-00-05-40-00-4D-B2").ToArray();

            // Act
            var message = spy.ParseReply(data);

            // Assert
            Assert.That(message.Address, Is.EqualTo(0));
            Assert.That((ReplyType)message.Type, Is.EqualTo(ReplyType.Ack));
        }

        [Test]
        public void ParseReply_PdIdReport_ShouldParseCorrectly()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-80-14-00-06-45-00-0E-E3-10-10-00-00-74-97-23-06-06-1B-88").ToArray();

            // Act
            var message = spy.ParseReply(data);

            // Assert
            Assert.That(message.Address, Is.EqualTo(0));
            Assert.That((ReplyType)message.Type, Is.EqualTo(ReplyType.PdIdReport));
            Assert.That(message.Payload.Length, Is.GreaterThan(0));
        }

        [Test]
        public void ParseReply_Nak_ShouldParseCorrectly()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-80-0A-00-06-41-01-90-45").ToArray();

            // Act
            var message = spy.ParseReply(data);

            // Assert
            Assert.That(message.Address, Is.EqualTo(0));
            Assert.That((ReplyType)message.Type, Is.EqualTo(ReplyType.Nak));
        }

        [Test]
        public void ParseReply_MultipleCalls_ShouldMaintainSequence()
        {
            // Arrange
            var spy = new MessageSpy();
            var data1 = BinaryUtils.HexToBytes("53-80-09-00-04-40-00-4C-B3").ToArray();
            var data2 = BinaryUtils.HexToBytes("53-80-09-00-05-40-00-4D-B2").ToArray();
            var data3 = BinaryUtils.HexToBytes("53-80-09-00-06-40-00-4E-B1").ToArray();

            // Act
            var message1 = spy.ParseReply(data1);
            var message2 = spy.ParseReply(data2);
            var message3 = spy.ParseReply(data3);

            // Assert
            Assert.That(message1.Sequence, Is.EqualTo(0));
            Assert.That(message2.Sequence, Is.EqualTo(1));
            Assert.That(message3.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void ParseReply_DifferentAddresses_ShouldStripReplyBit()
        {
            // Arrange
            var spy = new MessageSpy();
            var addresses = new byte[] { 0x80, 0x81, 0x85, 0xFE };

            foreach (var addr in addresses)
            {
                var data = new byte[] { 0x53, addr, 0x09, 0x00, 0x05, 0x40, 0x00, 0x00, 0x00 };

                // Act
                var message = spy.ParseReply(data);

                // Assert
                Assert.That(message.Address, Is.EqualTo(addr & 0x7F));
            }
        }

        [Test]
        public void ParseReply_WithCrc_ShouldIndicateCrcUsage()
        {
            // Arrange
            var spy = new MessageSpy();
            var data = BinaryUtils.HexToBytes("53-80-09-00-05-40-00-4D-B2").ToArray();

            // Act
            var message = spy.ParseReply(data);

            // Assert
            Assert.That(message.IsUsingCrc, Is.True);
        }

        [Test]
        public void ParseReply_InitialRMac_AfterSecureChannelSetup_ShouldReturnCorrectType()
        {
            // Arrange
            var securityKey = new byte[16];
            Array.Fill(securityKey, (byte)0x30);
            var spy = new MessageSpy(securityKey);

            var challengeData = BinaryUtils.HexToBytes("53-00-10-00-04-76-11-22-33-44-55-66-77-88-EF-4E").ToArray();
            spy.ParseCommand(challengeData);

            var scryptData = BinaryUtils.HexToBytes("53-00-18-00-04-77-11-22-33-44-55-66-77-88-11-22-33-44-55-66-77-88-12-34").ToArray();
            spy.ParseCommand(scryptData);

            var rmacData = BinaryUtils.HexToBytes("53-80-15-00-04-78-11-22-33-44-55-66-77-88-11-22-33-44-55-66-77-88-12-34").ToArray();

            // Act
            var message = spy.ParseReply(rmacData);

            // Assert
            Assert.That((ReplyType)message.Type, Is.EqualTo(ReplyType.InitialRMac));
        }

        [Test]
        public void ParseReply_WithoutSecurityKey_InitialRMac_ShouldStillParse()
        {
            // Arrange
            var spy = new MessageSpy();
            var rmacData = BinaryUtils.HexToBytes("53-80-15-00-04-78-11-22-33-44-55-66-77-88-11-22-33-44-55-66-77-88-12-34").ToArray();

            // Act
            var message = spy.ParseReply(rmacData);

            // Assert
            Assert.That((ReplyType)message.Type, Is.EqualTo(ReplyType.InitialRMac));
            Assert.That(message, Is.Not.Null);
        }
    }

    [TestFixture]
    public class SecureChannelTest
    {
        [Test]
        public void SecureChannel_CompleteHandshake_ShouldProcessAllSteps()
        {
            // Arrange
            var securityKey = new byte[16];
            Array.Fill(securityKey, (byte)0x30);
            var spy = new MessageSpy(securityKey);

            var challengeData = BinaryUtils.HexToBytes("53-00-10-00-04-76-11-22-33-44-55-66-77-88-EF-4E").ToArray();
            var scryptData = BinaryUtils.HexToBytes("53-00-18-00-04-77-11-22-33-44-55-66-77-88-11-22-33-44-55-66-77-88-12-34").ToArray();
            var rmacData = BinaryUtils.HexToBytes("53-80-15-00-04-78-11-22-33-44-55-66-77-88-11-22-33-44-55-66-77-88-12-34").ToArray();

            // Act
            var challengeMessage = spy.ParseCommand(challengeData);
            var scryptMessage = spy.ParseCommand(scryptData);
            var rmacMessage = spy.ParseReply(rmacData);

            // Assert
            Assert.That((CommandType)challengeMessage.Type, Is.EqualTo(CommandType.SessionChallenge));
            Assert.That((CommandType)scryptMessage.Type, Is.EqualTo(CommandType.ServerCryptogram));
            Assert.That((ReplyType)rmacMessage.Type, Is.EqualTo(ReplyType.InitialRMac));
        }

        [Test]
        public void SecureChannel_MultipleSessionChallenges_ShouldHandleSequentially()
        {
            // Arrange
            var securityKey = new byte[16];
            Array.Fill(securityKey, (byte)0x30);
            var spy = new MessageSpy(securityKey);

            var challengeData1 = BinaryUtils.HexToBytes("53-00-10-00-04-76-11-22-33-44-55-66-77-88-EF-4E").ToArray();
            var challengeData2 = BinaryUtils.HexToBytes("53-00-10-00-05-76-AA-BB-CC-DD-EE-FF-00-11-EF-4E").ToArray();

            // Act
            var message1 = spy.ParseCommand(challengeData1);
            var message2 = spy.ParseCommand(challengeData2);

            // Assert
            Assert.That((CommandType)message1.Type, Is.EqualTo(CommandType.SessionChallenge));
            Assert.That((CommandType)message2.Type, Is.EqualTo(CommandType.SessionChallenge));
            Assert.That(message1.Sequence, Is.Not.EqualTo(message2.Sequence));
        }
    }
}
