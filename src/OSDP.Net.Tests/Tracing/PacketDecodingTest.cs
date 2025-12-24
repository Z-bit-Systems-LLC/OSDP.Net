using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.Tracing;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tests.Tracing;

[TestFixture]
[Category("Unit")]
public class PacketDecodingTest
{
    [Test]
    public void ParseMessage_Command()
    {
        // Arrange
        var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
        
        // Act
        var actual = PacketDecoding.ParseMessage(testData.ToArray(), new ACUMessageSecureChannel());
        
        // Assert
        Assert.That(actual.Address, Is.EqualTo(0));
        Assert.That(actual.Sequence, Is.EqualTo(2));
        Assert.That(actual.IsUsingCrc, Is.EqualTo(true));
        Assert.That(actual.CommandType, Is.EqualTo(CommandType.BuzzerControl));
        Assert.That(actual.ReplyType, Is.Null);

        var testDataObject = actual.ParsePayloadData() as ReaderBuzzerControl;
        Assert.That(testDataObject, Is.Not.Null);
        
        Assert.That(actual.RawPayloadData.ToArray(), Is.EqualTo(testData.Skip(6).Take(5)));
        Assert.That(actual.RawData.ToArray(), Is.EqualTo(testData));
    }
    
    [Test]
    public void ParseMessage_Spaces()
    {
        // Arrange
        var testData = BinaryUtils.HexToBytes("53 00 0D 00 06 6A 00 02 02 02 01 59 92").ToArray();
        
        // Act
        var actual = PacketDecoding.ParseMessage(testData.ToArray(), new ACUMessageSecureChannel());
        
        // Assert
        Assert.That(actual.Address, Is.EqualTo(0));
        Assert.That(actual.Sequence, Is.EqualTo(2));
        Assert.That(actual.IsUsingCrc, Is.EqualTo(true));
        Assert.That(actual.CommandType, Is.EqualTo(CommandType.BuzzerControl));
        Assert.That(actual.ReplyType, Is.Null);

        var testDataObject = actual.ParsePayloadData() as ReaderBuzzerControl;
        Assert.That(testDataObject, Is.Not.Null);
        
        Assert.That(actual.RawPayloadData.ToArray(), Is.EqualTo(testData.Skip(6).Take(5)));
        Assert.That(actual.RawData.ToArray(), Is.EqualTo(testData));
    }
    
    [Test]
    public void ParseMessage_Reply()
    {
        // Arrange
        var testData = BinaryUtils.HexToBytes("53-80-14-00-06-45-00-0E-E3-10-10-00-00-74-97-23-06-06-1B-88").ToArray();
        
        // Act
        var actual = PacketDecoding.ParseMessage(testData.ToArray(), new ACUMessageSecureChannel());
        
        // Assert
        Assert.That(actual.Address, Is.EqualTo(0));
        Assert.That(actual.Sequence, Is.EqualTo(2));
        Assert.That(actual.IsUsingCrc, Is.EqualTo(true));
        Assert.That(actual.CommandType, Is.Null);
        Assert.That(actual.ReplyType, Is.EqualTo(ReplyType.PdIdReport));

        var testDataObject = actual.ParsePayloadData() as DeviceIdentification;
        Assert.That(testDataObject, Is.Not.Null);
        
        Assert.That(actual.RawPayloadData.ToArray(), Is.EqualTo(testData.Skip(6).Take(12)));
        Assert.That(actual.RawData.ToArray(), Is.EqualTo(testData));
    }
    
    [Test]
    public void OSDPCapParser_Command()
    {
        var testEntry =
            "{\"timeSec\":\"1689599213\",\"timeNano\":\"141793300\",\"io\":\"output\",\"data\":\"53-00-0D-00-06-6A-00-02-02-02-01-59-92\",\"osdpTraceVersion\":\"1\",\"osdpSource\":\"OSDP.Net\"}";

        var actual = PacketDecoding.OSDPCapParser(testEntry).ToArray();

        Assert.That(actual.Count, Is.EqualTo(1));
        var actualEntry = actual[0];
        
        Assert.That(actualEntry.TimeStamp, Is.EqualTo(DateTime.Parse("2023-07-17 13:06:53.1417933")));
        Assert.That(actualEntry.Direction, Is.EqualTo(TraceDirection.Output));
        Assert.That(actualEntry.Packet.Address, Is.EqualTo(0));
        Assert.That(actualEntry.TraceVersion, Is.EqualTo("1"));
        Assert.That(actualEntry.Source, Is.EqualTo("OSDP.Net"));
    }

    [Test]
    public void OSDPCapParser_Reply()
    {
        var testEntry =
            "{\"timeSec\":\"1689599206\",\"timeNano\":\"579440400\",\"io\":\"input\",\"data\":\"53-80-14-00-06-45-00-0E-E3-10-10-00-00-74-97-23-06-06-1B-88\",\"osdpTraceVersion\":\"1\",\"osdpSource\":\"OSDP.Net\"}";

        var actual = PacketDecoding.OSDPCapParser(testEntry).ToArray();

        Assert.That(actual.Count, Is.EqualTo(1));
        var actualEntry = actual[0];

        Assert.That(actualEntry.TimeStamp, Is.EqualTo(DateTime.Parse("2023-07-17 13:06:46.5794404")));
        Assert.That(actualEntry.Direction, Is.EqualTo(TraceDirection.Input));
        Assert.That(actualEntry.Packet.Address, Is.EqualTo(0));
        Assert.That(actualEntry.TraceVersion, Is.EqualTo("1"));
        Assert.That(actualEntry.Source, Is.EqualTo("OSDP.Net"));
    }

    [Test]
    public void TryParseMessage_ValidCommand_ReturnsTrue()
    {
        // Arrange
        var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();

        // Act
        var result = PacketDecoding.TryParseMessage(testData, new ACUMessageSecureChannel(), out var packet);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(packet, Is.Not.Null);
        Assert.That(packet!.Address, Is.EqualTo(0));
        Assert.That(packet.CommandType, Is.EqualTo(CommandType.BuzzerControl));
    }

    [Test]
    public void TryParseMessage_ValidReply_ReturnsTrue()
    {
        // Arrange
        var testData = BinaryUtils.HexToBytes("53-80-14-00-06-45-00-0E-E3-10-10-00-00-74-97-23-06-06-1B-88").ToArray();

        // Act
        var result = PacketDecoding.TryParseMessage(testData, new ACUMessageSecureChannel(), out var packet);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(packet, Is.Not.Null);
        Assert.That(packet!.Address, Is.EqualTo(0));
        Assert.That(packet.ReplyType, Is.EqualTo(ReplyType.PdIdReport));
    }

    [Test]
    public void TryParseMessage_EmptyData_ReturnsFalse()
    {
        // Arrange
        var testData = Array.Empty<byte>();

        // Act
        var result = PacketDecoding.TryParseMessage(testData, new ACUMessageSecureChannel(), out var packet);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(packet, Is.Null);
    }

    [Test]
    public void TryParseMessage_TruncatedData_ReturnsFalse()
    {
        // Arrange - only first 3 bytes of a valid packet
        var testData = BinaryUtils.HexToBytes("53-00-0D").ToArray();

        // Act
        var result = PacketDecoding.TryParseMessage(testData, new ACUMessageSecureChannel(), out var packet);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(packet, Is.Null);
    }

    [Test]
    public void TryParseMessage_WithoutSecureChannel_ValidCommand_ReturnsTrue()
    {
        // Arrange
        var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();

        // Act
        var result = PacketDecoding.TryParseMessage(testData, out var packet);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(packet, Is.Not.Null);
        Assert.That(packet!.Address, Is.EqualTo(0));
        Assert.That(packet.CommandType, Is.EqualTo(CommandType.BuzzerControl));
    }

    [Test]
    public void TryParseMessage_WithoutSecureChannel_ValidReply_ReturnsTrue()
    {
        // Arrange
        var testData = BinaryUtils.HexToBytes("53-80-14-00-06-45-00-0E-E3-10-10-00-00-74-97-23-06-06-1B-88").ToArray();

        // Act
        var result = PacketDecoding.TryParseMessage(testData, out var packet);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(packet, Is.Not.Null);
        Assert.That(packet!.Address, Is.EqualTo(0));
        Assert.That(packet.ReplyType, Is.EqualTo(ReplyType.PdIdReport));
    }

    [Test]
    public void TryParseMessage_WithoutSecureChannel_InvalidData_ReturnsFalse()
    {
        // Arrange
        var testData = Array.Empty<byte>();

        // Act
        var result = PacketDecoding.TryParseMessage(testData, out var packet);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(packet, Is.Null);
    }
}