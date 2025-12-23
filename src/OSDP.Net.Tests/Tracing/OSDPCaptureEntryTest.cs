using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Tracing;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tests.Tracing;

[TestFixture]
[Category("Unit")]
public class OSDPCaptureEntryTest
{
    [TestFixture]
    public class ConstructorTest
    {
        [Test]
        public void Constructor_WithCommandPacket_ShouldCreateEntry()
        {
            // Arrange
            var timeStamp = DateTime.Parse("2023-07-17 13:06:53.1417933");
            var direction = TraceDirection.Output;
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
            var traceVersion = "1";
            var source = "OSDP.Net";

            // Act
            var entry = new OSDPCaptureEntry(timeStamp, direction, packet, traceVersion, source);

            // Assert
            Assert.That(entry.TimeStamp, Is.EqualTo(timeStamp));
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.Packet, Is.EqualTo(packet));
            Assert.That(entry.TraceVersion, Is.EqualTo(traceVersion));
            Assert.That(entry.Source, Is.EqualTo(source));
        }

    [Test]
    public void Constructor_WithReplyPacket_ShouldCreateEntry()
    {
        // Arrange
        var timeStamp = DateTime.Parse("2023-07-17 13:06:46.5794404");
        var direction = TraceDirection.Input;
        var testData = BinaryUtils.HexToBytes("53-80-14-00-06-45-00-0E-E3-10-10-00-00-74-97-23-06-06-1B-88").ToArray();
        var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
        var traceVersion = "1";
        var source = "OSDP.Net";

        // Act
        var entry = new OSDPCaptureEntry(timeStamp, direction, packet, traceVersion, source);

        // Assert
        Assert.That(entry.TimeStamp, Is.EqualTo(timeStamp));
        Assert.That(entry.Direction, Is.EqualTo(direction));
        Assert.That(entry.Packet, Is.EqualTo(packet));
        Assert.That(entry.TraceVersion, Is.EqualTo(traceVersion));
        Assert.That(entry.Source, Is.EqualTo(source));
    }

    [Test]
    public void Constructor_WithTraceDirection_ShouldCreateEntry()
    {
        // Arrange
        var timeStamp = DateTime.UtcNow;
        var direction = TraceDirection.Trace;
        var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
        var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
        var traceVersion = "2";
        var source = "TestApp";

        // Act
        var entry = new OSDPCaptureEntry(timeStamp, direction, packet, traceVersion, source);

        // Assert
        Assert.That(entry.TimeStamp, Is.EqualTo(timeStamp));
        Assert.That(entry.Direction, Is.EqualTo(direction));
        Assert.That(entry.Packet, Is.EqualTo(packet));
        Assert.That(entry.TraceVersion, Is.EqualTo(traceVersion));
        Assert.That(entry.Source, Is.EqualTo(source));
    }

        [Test]
        public void Constructor_WithUtcTimestamp_ShouldCreateEntry()
        {
            // Arrange
            var timeStamp = DateTime.UtcNow;
            var direction = TraceDirection.Input;
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
            var traceVersion = "1";
            var source = "OSDP.Net";

            // Act
            var entry = new OSDPCaptureEntry(timeStamp, direction, packet, traceVersion, source);

            // Assert
            Assert.That(entry.TimeStamp, Is.EqualTo(timeStamp));
            Assert.That(entry.TimeStamp.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void Constructor_WithDifferentSources_ShouldStoreCorrectly()
        {
            // Arrange
            var timeStamp = DateTime.UtcNow;
            var direction = TraceDirection.Input;
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
            var traceVersion = "1";

            // Act & Assert
            foreach (var source in new[] { "OSDP.Net", "CustomApp", "Monitor", "TestHarness" })
            {
                var entry = new OSDPCaptureEntry(timeStamp, direction, packet, traceVersion, source);
                Assert.That(entry.Source, Is.EqualTo(source));
            }
        }

        [Test]
        public void Constructor_WithDifferentTraceVersions_ShouldStoreCorrectly()
        {
            // Arrange
            var timeStamp = DateTime.UtcNow;
            var direction = TraceDirection.Input;
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
            var source = "OSDP.Net";

            // Act & Assert
            foreach (var traceVersion in new[] { "1", "2", "1.0", "2.1.5" })
            {
                var entry = new OSDPCaptureEntry(timeStamp, direction, packet, traceVersion, source);
                Assert.That(entry.TraceVersion, Is.EqualTo(traceVersion));
            }
        }

        [Test]
        public void Constructor_WithMinDateTime_ShouldCreateEntry()
        {
            // Arrange
            var timeStamp = DateTime.MinValue;
            var direction = TraceDirection.Output;
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
            var traceVersion = "1";
            var source = "OSDP.Net";

            // Act
            var entry = new OSDPCaptureEntry(timeStamp, direction, packet, traceVersion, source);

            // Assert
            Assert.That(entry.TimeStamp, Is.EqualTo(DateTime.MinValue));
        }

        [Test]
        public void Constructor_WithMaxDateTime_ShouldCreateEntry()
        {
            // Arrange
            var timeStamp = DateTime.MaxValue;
            var direction = TraceDirection.Input;
            var testData = BinaryUtils.HexToBytes("53-80-14-00-06-45-00-0E-E3-10-10-00-00-74-97-23-06-06-1B-88").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
            var traceVersion = "1";
            var source = "OSDP.Net";

            // Act
            var entry = new OSDPCaptureEntry(timeStamp, direction, packet, traceVersion, source);

            // Assert
            Assert.That(entry.TimeStamp, Is.EqualTo(DateTime.MaxValue));
        }
    }

    [TestFixture]
    public class PropertiesTest
    {
        [Test]
        public void AllProperties_ShouldRetainOriginalValues()
        {
            // Arrange
            var timeStamp = DateTime.UtcNow;
            var direction = TraceDirection.Input;
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
            var traceVersion = "1";
            var source = "OSDP.Net";

            // Act
            var entry = new OSDPCaptureEntry(timeStamp, direction, packet, traceVersion, source);

            // Assert
            Assert.That(entry.TimeStamp, Is.EqualTo(timeStamp));
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.Packet, Is.Not.Null);
            Assert.That(entry.TraceVersion, Is.EqualTo(traceVersion));
            Assert.That(entry.Source, Is.EqualTo(source));
        }

        [Test]
        public void TimeStamp_ShouldMatchConstructorValue()
        {
            // Arrange
            var specificTime = new DateTime(2023, 7, 17, 13, 6, 53, 141, DateTimeKind.Utc);
            var direction = TraceDirection.Output;
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());

            // Act
            var entry = new OSDPCaptureEntry(specificTime, direction, packet, "1", "OSDP.Net");

            // Assert
            Assert.That(entry.TimeStamp, Is.EqualTo(specificTime));
            Assert.That(entry.TimeStamp.Year, Is.EqualTo(2023));
            Assert.That(entry.TimeStamp.Month, Is.EqualTo(7));
            Assert.That(entry.TimeStamp.Day, Is.EqualTo(17));
        }

        [Test]
        public void Direction_ShouldMatchConstructorValue()
        {
            // Arrange
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());

            // Act & Assert
            foreach (var direction in new[] { TraceDirection.Input, TraceDirection.Output, TraceDirection.Trace })
            {
                var entry = new OSDPCaptureEntry(DateTime.UtcNow, direction, packet, "1", "OSDP.Net");
                Assert.That(entry.Direction, Is.EqualTo(direction));
            }
        }

        [Test]
        public void Packet_ShouldRetainReference()
        {
            // Arrange
            var timeStamp = DateTime.UtcNow;
            var direction = TraceDirection.Output;
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());

            // Act
            var entry = new OSDPCaptureEntry(timeStamp, direction, packet, "1", "OSDP.Net");

            // Assert
            Assert.That(entry.Packet, Is.SameAs(packet));
        }

        [Test]
        public void TraceVersion_ShouldMatchConstructorValue()
        {
            // Arrange
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
            var traceVersion = "2.5.3";

            // Act
            var entry = new OSDPCaptureEntry(DateTime.UtcNow, TraceDirection.Input, packet, traceVersion, "OSDP.Net");

            // Assert
            Assert.That(entry.TraceVersion, Is.EqualTo(traceVersion));
        }

        [Test]
        public void Source_ShouldMatchConstructorValue()
        {
            // Arrange
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());
            var source = "CustomMonitor";

            // Act
            var entry = new OSDPCaptureEntry(DateTime.UtcNow, TraceDirection.Output, packet, "1", source);

            // Assert
            Assert.That(entry.Source, Is.EqualTo(source));
        }
    }

    [TestFixture]
    public class PacketDataTest
    {
        [Test]
        public void Packet_WithCommandData_ShouldContainCorrectType()
        {
            // Arrange
            var timeStamp = DateTime.UtcNow;
            var direction = TraceDirection.Output;
            var testData = BinaryUtils.HexToBytes("53-00-0D-00-06-6A-00-02-02-02-01-59-92").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());

            // Act
            var entry = new OSDPCaptureEntry(timeStamp, direction, packet, "1", "OSDP.Net");

            // Assert
            Assert.That(entry.Packet.CommandType, Is.EqualTo(CommandType.BuzzerControl));
            Assert.That(entry.Packet.Address, Is.EqualTo(0));
            Assert.That(entry.Packet.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void Packet_WithReplyData_ShouldContainCorrectType()
        {
            // Arrange
            var timeStamp = DateTime.UtcNow;
            var direction = TraceDirection.Input;
            var testData = BinaryUtils.HexToBytes("53-80-14-00-06-45-00-0E-E3-10-10-00-00-74-97-23-06-06-1B-88").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());

            // Act
            var entry = new OSDPCaptureEntry(timeStamp, direction, packet, "1", "OSDP.Net");

            // Assert
            Assert.That(entry.Packet.ReplyType, Is.EqualTo(ReplyType.PdIdReport));
            Assert.That(entry.Packet.Address, Is.EqualTo(0));
            Assert.That(entry.Packet.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void Packet_PollCommand_ShouldParseCorrectly()
        {
            // Arrange
            var testData = BinaryUtils.HexToBytes("53-00-08-00-04-60-00-C0-B9").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());

            // Act
            var entry = new OSDPCaptureEntry(DateTime.UtcNow, TraceDirection.Output, packet, "1", "OSDP.Net");

            // Assert
            Assert.That(entry.Packet.CommandType, Is.EqualTo(CommandType.Poll));
            Assert.That(entry.Packet.Address, Is.EqualTo(0));
        }

        [Test]
        public void Packet_AckReply_ShouldParseCorrectly()
        {
            // Arrange
            var testData = BinaryUtils.HexToBytes("53-80-09-00-05-40-00-4D-B2").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());

            // Act
            var entry = new OSDPCaptureEntry(DateTime.UtcNow, TraceDirection.Input, packet, "1", "OSDP.Net");

            // Assert
            Assert.That(entry.Packet.ReplyType, Is.EqualTo(ReplyType.Ack));
            Assert.That(entry.Packet.Address, Is.EqualTo(0));
        }

        [Test]
        public void Packet_WithDifferentAddress_ShouldStoreCorrectly()
        {
            // Arrange
            var testData = BinaryUtils.HexToBytes("53-05-08-00-04-60-00-C0-B9").ToArray();
            var packet = PacketDecoding.ParseMessage(testData, new ACUMessageSecureChannel());

            // Act
            var entry = new OSDPCaptureEntry(DateTime.UtcNow, TraceDirection.Output, packet, "1", "OSDP.Net");

            // Assert
            Assert.That(entry.Packet.Address, Is.EqualTo(5));
        }
    }
}
