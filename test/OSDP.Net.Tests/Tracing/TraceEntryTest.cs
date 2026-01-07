using System;
using NUnit.Framework;
using OSDP.Net.Tracing;

namespace OSDP.Net.Tests.Tracing;

[TestFixture]
[Category("Unit")]
public class TraceEntryTest
{
    [TestFixture]
    public class ConstructorTest
    {
        [Test]
        public void Constructor_WithInputDirection_ShouldCreateEntry()
        {
            // Arrange
            var direction = TraceDirection.Input;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53, 0x00, 0x0D };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(entry.Data, Is.EqualTo(data));
        }

        [Test]
        public void Constructor_WithOutputDirection_ShouldCreateEntry()
        {
            // Arrange
            var direction = TraceDirection.Output;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53, 0x80, 0x14 };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(entry.Data, Is.EqualTo(data));
        }

        [Test]
        public void Constructor_WithTraceDirection_ShouldCreateEntry()
        {
            // Arrange
            var direction = TraceDirection.Trace;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53, 0x00, 0x0D };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(entry.Data, Is.EqualTo(data));
        }

        [Test]
        public void Constructor_WithEmptyGuid_ShouldCreateEntry()
        {
            // Arrange
            var direction = TraceDirection.Input;
            var connectionId = Guid.Empty;
            var data = new byte[] { 0x53 };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.ConnectionId, Is.EqualTo(Guid.Empty));
            Assert.That(entry.Data, Is.EqualTo(data));
        }

        [Test]
        public void Constructor_WithEmptyByteArray_ShouldCreateEntry()
        {
            // Arrange
            var direction = TraceDirection.Input;
            var connectionId = Guid.NewGuid();
            var data = Array.Empty<byte>();

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(entry.Data, Is.Empty);
        }

        [Test]
        public void Constructor_WithSingleByte_ShouldCreateEntry()
        {
            // Arrange
            var direction = TraceDirection.Output;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53 };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(entry.Data, Has.Length.EqualTo(1));
            Assert.That(entry.Data[0], Is.EqualTo(0x53));
        }

        [Test]
        public void Constructor_WithLargeByteArray_ShouldCreateEntry()
        {
            // Arrange
            var direction = TraceDirection.Output;
            var connectionId = Guid.NewGuid();
            var data = new byte[1024];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(entry.Data, Is.EqualTo(data));
            Assert.That(entry.Data, Has.Length.EqualTo(1024));
        }

        [Test]
        public void Constructor_WithNullData_ShouldCreateEntry()
        {
            // Arrange
            var direction = TraceDirection.Input;
            var connectionId = Guid.NewGuid();

            // Act
            var entry = new TraceEntry(direction, connectionId, null);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(entry.Data, Is.Null);
        }

        [Test]
        public void Constructor_WithSpecificConnectionId_ShouldStoreCorrectly()
        {
            // Arrange
            var direction = TraceDirection.Trace;
            var connectionId = new Guid("12345678-1234-1234-1234-123456789012");
            var data = new byte[] { 0x01, 0x02, 0x03 };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(entry.ConnectionId.ToString(), Is.EqualTo("12345678-1234-1234-1234-123456789012"));
        }
    }

    [TestFixture]
    public class PropertiesTest
    {
        [Test]
        public void Direction_ShouldRetainOriginalValue()
        {
            // Arrange
            var direction = TraceDirection.Input;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53 };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
        }

        [Test]
        public void ConnectionId_ShouldRetainOriginalValue()
        {
            // Arrange
            var direction = TraceDirection.Input;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53 };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
        }

        [Test]
        public void Data_ShouldRetainOriginalReference()
        {
            // Arrange
            var direction = TraceDirection.Input;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53, 0x00, 0x0D };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Data, Is.EqualTo(data));
            Assert.That(entry.Data, Is.SameAs(data));
        }

        [Test]
        public void AllProperties_ShouldBeSetCorrectly()
        {
            // Arrange
            var direction = TraceDirection.Output;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53, 0x80, 0x14, 0x00, 0x06, 0x45 };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Direction, Is.EqualTo(direction));
            Assert.That(entry.ConnectionId, Is.EqualTo(connectionId));
            Assert.That(entry.Data, Is.EqualTo(data));
        }
    }

    [TestFixture]
    public class EdgeCaseTest
    {
        [Test]
        public void Data_ModifyingOriginalArray_ShouldAffectEntry()
        {
            // Arrange
            var direction = TraceDirection.Input;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53, 0x00, 0x0D };
            var entry = new TraceEntry(direction, connectionId, data);

            // Act
            data[0] = 0xFF;

            // Assert - TraceEntry holds a reference, not a copy
            Assert.That(entry.Data[0], Is.EqualTo(0xFF));
        }

        [Test]
        public void MultipleEntries_WithSameConnectionId_ShouldBeIndependent()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var data1 = new byte[] { 0x53, 0x00, 0x0D };
            var data2 = new byte[] { 0x53, 0x80, 0x14 };

            // Act
            var entry1 = new TraceEntry(TraceDirection.Output, connectionId, data1);
            var entry2 = new TraceEntry(TraceDirection.Input, connectionId, data2);

            // Assert
            Assert.That(entry1.ConnectionId, Is.EqualTo(entry2.ConnectionId));
            Assert.That(entry1.Direction, Is.Not.EqualTo(entry2.Direction));
            Assert.That(entry1.Data, Is.Not.EqualTo(entry2.Data));
        }

        [Test]
        public void MultipleEntries_WithDifferentDirections_ShouldStoreCorrectly()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53, 0x00 };

            // Act
            var inputEntry = new TraceEntry(TraceDirection.Input, connectionId, data);
            var outputEntry = new TraceEntry(TraceDirection.Output, connectionId, data);
            var traceEntry = new TraceEntry(TraceDirection.Trace, connectionId, data);

            // Assert
            Assert.That(inputEntry.Direction, Is.EqualTo(TraceDirection.Input));
            Assert.That(outputEntry.Direction, Is.EqualTo(TraceDirection.Output));
            Assert.That(traceEntry.Direction, Is.EqualTo(TraceDirection.Trace));
        }

        [Test]
        public void Data_WithMaxByteValues_ShouldStoreCorrectly()
        {
            // Arrange
            var direction = TraceDirection.Input;
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x00, 0xFF, 0x7F, 0x80, 0x01 };

            // Act
            var entry = new TraceEntry(direction, connectionId, data);

            // Assert
            Assert.That(entry.Data[0], Is.EqualTo(0x00));
            Assert.That(entry.Data[1], Is.EqualTo(0xFF));
            Assert.That(entry.Data[2], Is.EqualTo(0x7F));
            Assert.That(entry.Data[3], Is.EqualTo(0x80));
            Assert.That(entry.Data[4], Is.EqualTo(0x01));
        }
    }

    [TestFixture]
    public class AddressPropertyTest
    {
        [Test]
        public void Address_WithCommandPacket_ShouldExtractCorrectAddress()
        {
            // Arrange - Command packet for address 0
            var data = new byte[] { 0x00, 0x0D, 0x00, 0x06, 0x60 }; // Address byte is 0x00

            // Act
            var entry = new TraceEntry(TraceDirection.Output, Guid.NewGuid(), data);

            // Assert
            Assert.That(entry.Address, Is.EqualTo(0));
        }

        [Test]
        public void Address_WithReplyPacket_ShouldMaskReplyBit()
        {
            // Arrange - Reply packet for address 0 (0x80 has reply bit set)
            var data = new byte[] { 0x80, 0x14, 0x00, 0x06, 0x45 }; // Address byte is 0x80 (address 0 with reply bit)

            // Act
            var entry = new TraceEntry(TraceDirection.Input, Guid.NewGuid(), data);

            // Assert - Reply bit (0x80) should be masked off, resulting in address 0
            Assert.That(entry.Address, Is.EqualTo(0));
        }

        [Test]
        public void Address_WithNonZeroAddress_ShouldExtractCorrectly()
        {
            // Arrange - Command packet for address 5
            var data = new byte[] { 0x05, 0x0D, 0x00, 0x06, 0x60 };

            // Act
            var entry = new TraceEntry(TraceDirection.Output, Guid.NewGuid(), data);

            // Assert
            Assert.That(entry.Address, Is.EqualTo(5));
        }

        [Test]
        public void Address_WithNonZeroAddressReply_ShouldMaskReplyBit()
        {
            // Arrange - Reply packet for address 5 (0x85 = 0x80 | 0x05)
            var data = new byte[] { 0x85, 0x14, 0x00, 0x06, 0x45 };

            // Act
            var entry = new TraceEntry(TraceDirection.Input, Guid.NewGuid(), data);

            // Assert - Reply bit (0x80) should be masked off, resulting in address 5
            Assert.That(entry.Address, Is.EqualTo(5));
        }

        [Test]
        public void Address_WithMaxAddress_ShouldExtractCorrectly()
        {
            // Arrange - Command packet for max address (127 = 0x7F)
            var data = new byte[] { 0x7F, 0x0D, 0x00, 0x06, 0x60 };

            // Act
            var entry = new TraceEntry(TraceDirection.Output, Guid.NewGuid(), data);

            // Assert
            Assert.That(entry.Address, Is.EqualTo(127));
        }

        [Test]
        public void Address_WithMaxAddressReply_ShouldMaskReplyBit()
        {
            // Arrange - Reply packet for max address (0xFF = 0x80 | 0x7F)
            var data = new byte[] { 0xFF, 0x14, 0x00, 0x06, 0x45 };

            // Act
            var entry = new TraceEntry(TraceDirection.Input, Guid.NewGuid(), data);

            // Assert - Reply bit (0x80) should be masked off, resulting in address 127
            Assert.That(entry.Address, Is.EqualTo(127));
        }

        [Test]
        public void Address_WithEmptyData_ShouldReturnNull()
        {
            // Arrange
            var data = Array.Empty<byte>();

            // Act
            var entry = new TraceEntry(TraceDirection.Output, Guid.NewGuid(), data);

            // Assert
            Assert.That(entry.Address, Is.Null);
        }

        [Test]
        public void Address_WithNullData_ShouldReturnNull()
        {
            // Act
            var entry = new TraceEntry(TraceDirection.Output, Guid.NewGuid(), null);

            // Assert
            Assert.That(entry.Address, Is.Null);
        }

        [Test]
        public void Address_WithSingleByteData_ShouldExtractAddress()
        {
            // Arrange
            var data = new byte[] { 0x03 };

            // Act
            var entry = new TraceEntry(TraceDirection.Output, Guid.NewGuid(), data);

            // Assert
            Assert.That(entry.Address, Is.EqualTo(3));
        }

        [Test]
        public void Address_WithTypicalOsdpPacket_ShouldExtractCorrectly()
        {
            // Arrange - Typical OSDP poll command for address 0
            // Data starts after SOM byte (0x53 is skipped in Bus.cs)
            var data = new byte[] { 0x00, 0x08, 0x00, 0x04, 0x60, 0x3D, 0x57 };

            // Act
            var entry = new TraceEntry(TraceDirection.Output, Guid.NewGuid(), data);

            // Assert
            Assert.That(entry.Address, Is.EqualTo(0));
        }

        [Test]
        public void Address_WithTypicalOsdpReply_ShouldMaskReplyBit()
        {
            // Arrange - Typical OSDP ACK reply for address 0
            // Data starts after SOM byte (0x53 is skipped in Bus.cs)
            var data = new byte[] { 0x80, 0x07, 0x00, 0x05, 0x40, 0xFE, 0x9C };

            // Act
            var entry = new TraceEntry(TraceDirection.Input, Guid.NewGuid(), data);

            // Assert
            Assert.That(entry.Address, Is.EqualTo(0));
        }
    }
}
