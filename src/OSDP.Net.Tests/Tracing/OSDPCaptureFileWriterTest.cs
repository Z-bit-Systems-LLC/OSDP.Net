using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Moq;
using NUnit.Framework;
using OSDP.Net.Tracing;

namespace OSDP.Net.Tests.Tracing;

[TestFixture]
[Category("Unit")]
public class OSDPCaptureFileWriterTest
{
    private Mock<IFileSystem> _mockFileSystem;
    private MemoryStream _memoryStream;
    private StreamWriter _streamWriter;
    private const string TestFilePath = "test.osdpcap";
    private const string TestSource = "OSDP.Net";

    [SetUp]
    public void SetUp()
    {
        _mockFileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        _memoryStream = new MemoryStream();
        _streamWriter = new StreamWriter(_memoryStream, Encoding.UTF8, leaveOpen: true);
    }

    [TearDown]
    public void TearDown()
    {
        _streamWriter?.Dispose();
        _memoryStream?.Dispose();
    }

    [TestFixture]
    public class ConstructorTest : OSDPCaptureFileWriterTest
    {
        [Test]
        public void Constructor_ValidParameters_ShouldCreateWriter()
        {
            // Arrange
            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, true)).Returns(_streamWriter);

            // Act
            using var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object);

            // Assert
            Assert.That(writer, Is.Not.Null);
            _mockFileSystem.Verify(x => x.CreateStreamWriter(TestFilePath, true), Times.Once);
        }

        [Test]
        public void Constructor_NullFilePath_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new OSDPCaptureFileWriter(null, TestSource, _mockFileSystem.Object));
        }

        [Test]
        public void Constructor_EmptyFilePath_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new OSDPCaptureFileWriter(string.Empty, TestSource, _mockFileSystem.Object));
        }

        [Test]
        public void Constructor_NullSource_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new OSDPCaptureFileWriter(TestFilePath, null, _mockFileSystem.Object));
        }

        [Test]
        public void Constructor_EmptySource_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new OSDPCaptureFileWriter(TestFilePath, string.Empty, _mockFileSystem.Object));
        }

        [Test]
        public void Constructor_NonExistentDirectory_ShouldCreateDirectory()
        {
            // Arrange
            var filePath = Path.Combine("subdir", "subsubdir", "test.osdpcap");
            var directoryPath = Path.Combine("subdir", "subsubdir");

            _mockFileSystem.Setup(x => x.GetDirectoryName(filePath)).Returns(directoryPath);
            _mockFileSystem.Setup(x => x.CreateDirectory(directoryPath));
            _mockFileSystem.Setup(x => x.CreateStreamWriter(filePath, true)).Returns(_streamWriter);

            // Act
            using var writer = new OSDPCaptureFileWriter(filePath, TestSource, _mockFileSystem.Object);

            // Assert
            _mockFileSystem.Verify(x => x.CreateDirectory(directoryPath), Times.Once);
            _mockFileSystem.Verify(x => x.CreateStreamWriter(filePath, true), Times.Once);
        }
    }

    [TestFixture]
    public class WritePacketTest : OSDPCaptureFileWriterTest
    {
        [Test]
        public void WritePacket_ValidData_ShouldWriteToFile()
        {
            // Arrange
            var data = new byte[] { 0x53, 0x00, 0x0D, 0x00, 0x06, 0x6A };
            var direction = TraceDirection.Output;

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data, direction);
            }

            // Assert
            var fileContent = GetStreamContent();
            Assert.That(fileContent, Is.Not.Empty);
            Assert.That(fileContent, Does.Contain("\"io\":\"output\""));
            Assert.That(fileContent, Does.Contain("\"osdpSource\":\"OSDP.Net\""));
            Assert.That(fileContent, Does.Contain("\"osdpTraceVersion\":\"1\""));
            Assert.That(fileContent, Does.Contain("53-00-0D-00-06-6A"));
        }

        [Test]
        public void WritePacket_InputDirection_ShouldWriteCorrectDirection()
        {
            // Arrange
            var data = new byte[] { 0x53, 0x80, 0x14 };
            var direction = TraceDirection.Input;

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data, direction);
            }

            // Assert
            var fileContent = GetStreamContent();
            Assert.That(fileContent, Does.Contain("\"io\":\"input\""));
        }

        [Test]
        public void WritePacket_TraceDirection_ShouldWriteCorrectDirection()
        {
            // Arrange
            var data = new byte[] { 0x53, 0x00, 0x0D };
            var direction = TraceDirection.Trace;

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data, direction);
            }

            // Assert
            var fileContent = GetStreamContent();
            Assert.That(fileContent, Does.Contain("\"io\":\"trace\""));
        }

        [Test]
        public void WritePacket_WithTimestamp_ShouldWriteCorrectTimestamp()
        {
            // Arrange
            var data = new byte[] { 0x53, 0x00, 0x0D };
            var direction = TraceDirection.Output;
            // 2023-07-17 13:06:53.141 UTC + 0.793 seconds (7930000 ticks)
            var timestamp = new DateTime(2023, 7, 17, 13, 6, 53, 141, DateTimeKind.Utc).AddTicks(7930000);

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data, direction, timestamp);
            }

            // Assert
            var fileContent = GetStreamContent();
            var entry = JsonSerializer.Deserialize<JsonElement>(fileContent.Trim());

            Assert.That(entry.GetProperty("timeSec").GetString(), Is.EqualTo("1689599213"));
            // 141ms + 793ms = 934ms = 934000000 nanoseconds
            Assert.That(entry.GetProperty("timeNano").GetString(), Is.EqualTo("934000000"));
        }

        [Test]
        public void WritePacket_NullData_ShouldThrowArgumentNullException()
        {
            // Arrange
            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, true)).Returns(_streamWriter);

            using var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => writer.WritePacket(null, TraceDirection.Output));
        }

        [Test]
        public void WritePacket_AfterDispose_ShouldThrowObjectDisposedException()
        {
            // Arrange
            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, true)).Returns(_streamWriter);

            var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object);
            writer.Dispose();
            var data = new byte[] { 0x53 };

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => writer.WritePacket(data, TraceDirection.Output));
        }

        [Test]
        public void WritePacket_EmptyByteArray_ShouldWriteToFile()
        {
            // Arrange
            var data = Array.Empty<byte>();
            var direction = TraceDirection.Output;

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data, direction);
            }

            // Assert
            var fileContent = GetStreamContent();
            Assert.That(fileContent, Is.Not.Empty);
            var lines = fileContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.EqualTo(1));
        }

        [Test]
        public void WritePacket_CustomSource_ShouldWriteCorrectSource()
        {
            // Arrange
            var data = new byte[] { 0x53 };
            var customSource = "CustomMonitor";

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, customSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data, TraceDirection.Output);
            }

            // Assert
            var fileContent = GetStreamContent();
            Assert.That(fileContent, Does.Contain($"\"osdpSource\":\"{customSource}\""));
        }

        [Test]
        public void WritePacket_LargeDataArray_ShouldWriteCorrectly()
        {
            // Arrange
            var data = new byte[1024];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data, TraceDirection.Output);
            }

            // Assert
            var fileContent = GetStreamContent();
            Assert.That(fileContent, Is.Not.Empty);
            Assert.That(fileContent, Does.Contain("\"io\":\"output\""));
        }

        [Test]
        public void WritePacket_AllDirections_ShouldWriteCorrectDirectionStrings()
        {
            // Arrange
            var data = new byte[] { 0x53 };
            var expectedDirections = new Dictionary<TraceDirection, string>
            {
                { TraceDirection.Input, "input" },
                { TraceDirection.Output, "output" },
                { TraceDirection.Trace, "trace" }
            };

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                foreach (var direction in expectedDirections.Keys)
                {
                    writer.WritePacket(data, direction);
                }
            }

            // Assert
            var fileContent = GetStreamContent();
            foreach (var expectedDirection in expectedDirections.Values)
            {
                Assert.That(fileContent, Does.Contain($"\"io\":\"{expectedDirection}\""));
            }
        }

        [Test]
        public void WritePacket_WithSpecialCharactersInData_ShouldEncodeCorrectly()
        {
            // Arrange
            var data = new byte[] { 0x00, 0xFF, 0x7F, 0x80, 0x01 };

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data, TraceDirection.Output);
            }

            // Assert
            var fileContent = GetStreamContent();
            Assert.That(fileContent, Does.Contain("00-FF-7F-80-01"));
        }

        [Test]
        public void WritePacket_WithTimestampPrecision_ShouldPreserveNanoseconds()
        {
            // Arrange
            var data = new byte[] { 0x53 };
            var timestamp = new DateTime(2023, 7, 17, 13, 6, 53, 141, DateTimeKind.Utc).AddTicks(7933);

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data, TraceDirection.Output, timestamp);
            }

            // Assert
            var fileContent = GetStreamContent();
            Assert.That(fileContent, Does.Contain("\"timeSec\""));
            Assert.That(fileContent, Does.Contain("\"timeNano\""));
        }

        [Test]
        public void WritePacket_MultiplePackets_ShouldWriteMultipleLines()
        {
            // Arrange
            var data1 = new byte[] { 0x53, 0x00, 0x0D };
            var data2 = new byte[] { 0x53, 0x80, 0x14 };
            var data3 = new byte[] { 0x53, 0x00, 0x0E };

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WritePacket(data1, TraceDirection.Output);
                writer.WritePacket(data2, TraceDirection.Input);
                writer.WritePacket(data3, TraceDirection.Trace);
            }

            // Assert
            var lines = GetStreamContent().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.EqualTo(3));
        }
    }

    [TestFixture]
    public class WriteTraceTest : OSDPCaptureFileWriterTest
    {
        [Test]
        public void WriteTrace_ValidTraceEntry_ShouldWriteToFile()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53, 0x00, 0x0D };
            var trace = new TraceEntry(TraceDirection.Output, connectionId, data);

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                writer.WriteTrace(trace);
            }

            // Assert
            var fileContent = GetStreamContent();
            Assert.That(fileContent, Is.Not.Empty);
            Assert.That(fileContent, Does.Contain("\"io\":\"output\""));
            Assert.That(fileContent, Does.Contain("53-00-0D"));
        }

        [Test]
        public void WriteTrace_AfterDispose_ShouldThrowObjectDisposedException()
        {
            // Arrange
            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, true)).Returns(_streamWriter);

            var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object);
            writer.Dispose();
            var trace = new TraceEntry(TraceDirection.Output, Guid.NewGuid(), new byte[] { 0x53 });

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => writer.WriteTrace(trace));
        }

        [Test]
        public void WriteTrace_WithNullData_ShouldThrowArgumentNullException()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var trace = new TraceEntry(TraceDirection.Output, connectionId, null);

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, true)).Returns(_streamWriter);

            using var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => writer.WriteTrace(trace));
        }

        [Test]
        public void WriteTrace_MultipleEntries_ShouldWriteAll()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var traces = new[]
            {
                new TraceEntry(TraceDirection.Output, connectionId, new byte[] { 0x53, 0x00 }),
                new TraceEntry(TraceDirection.Input, connectionId, new byte[] { 0x53, 0x80 }),
                new TraceEntry(TraceDirection.Trace, connectionId, new byte[] { 0x53, 0xFF })
            };

            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, false)).Returns(_streamWriter);

            using (var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object, append: false))
            {
                // Act
                foreach (var trace in traces)
                {
                    writer.WriteTrace(trace);
                }
            }

            // Assert
            var lines = GetStreamContent().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.EqualTo(3));
        }
    }

    [TestFixture]
    public class DisposeTest : OSDPCaptureFileWriterTest
    {
        [Test]
        public void Dispose_MultipleCalls_ShouldNotThrow()
        {
            // Arrange
            _mockFileSystem.Setup(x => x.GetDirectoryName(TestFilePath)).Returns("");
            _mockFileSystem.Setup(x => x.CreateStreamWriter(TestFilePath, true)).Returns(_streamWriter);

            var writer = new OSDPCaptureFileWriter(TestFilePath, TestSource, _mockFileSystem.Object);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                writer.Dispose();
                writer.Dispose();
                writer.Dispose();
            });
        }
    }

    private string GetStreamContent()
    {
        _streamWriter.Flush();
        _memoryStream.Position = 0;
        using var reader = new StreamReader(_memoryStream, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }
}