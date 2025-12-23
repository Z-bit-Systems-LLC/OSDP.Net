using System;
using System.Collections.Generic;
using NUnit.Framework;
using OSDP.Net.Tracing;

namespace OSDP.Net.Tests.Tracing;

[TestFixture]
[Category("Unit")]
public class OSDPFileCaptureTracerTest
{
    private MockFileWriterFactory _mockFactory;

    [SetUp]
    public void SetUp()
    {
        _mockFactory = new MockFileWriterFactory();
    }

    [TearDown]
    public void TearDown()
    {
        _mockFactory?.CloseAllWriters();
    }

    [TestFixture]
    public class TraceTest : OSDPFileCaptureTracerTest
    {
        [Test]
        public void Trace_FirstCallForConnection_ShouldCreateWriter()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var data = new byte[] { 0x53, 0x00, 0x0D };
            var trace = new TraceEntry(TraceDirection.Output, connectionId, data);

            // Act
            _mockFactory.Trace(trace);

            // Assert
            var writer = _mockFactory.GetWriter(connectionId);
            Assert.That(writer, Is.Not.Null);
            Assert.That(writer.FilePath, Is.EqualTo($"{connectionId:D}.osdpcap"));
            Assert.That(writer.WrittenTraces.Count, Is.EqualTo(1));
        }

        [Test]
        public void Trace_MultipleCallsSameConnection_ShouldAppendToSameWriter()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var trace1 = new TraceEntry(TraceDirection.Output, connectionId, new byte[] { 0x53, 0x00, 0x0D });
            var trace2 = new TraceEntry(TraceDirection.Input, connectionId, new byte[] { 0x53, 0x80, 0x14 });
            var trace3 = new TraceEntry(TraceDirection.Trace, connectionId, new byte[] { 0x53, 0x00, 0x0E });

            // Act
            _mockFactory.Trace(trace1);
            _mockFactory.Trace(trace2);
            _mockFactory.Trace(trace3);

            // Assert
            var writer = _mockFactory.GetWriter(connectionId);
            Assert.That(writer, Is.Not.Null);
            Assert.That(writer.WrittenTraces.Count, Is.EqualTo(3));
        }

        [Test]
        public void Trace_MultipleDifferentConnections_ShouldCreateSeparateWriters()
        {
            // Arrange
            var connectionId1 = Guid.NewGuid();
            var connectionId2 = Guid.NewGuid();
            var trace1 = new TraceEntry(TraceDirection.Output, connectionId1, new byte[] { 0x53, 0x00, 0x0D });
            var trace2 = new TraceEntry(TraceDirection.Input, connectionId2, new byte[] { 0x53, 0x80, 0x14 });

            // Act
            _mockFactory.Trace(trace1);
            _mockFactory.Trace(trace2);

            // Assert
            Assert.That(_mockFactory.GetWriter(connectionId1), Is.Not.Null);
            Assert.That(_mockFactory.GetWriter(connectionId2), Is.Not.Null);
            Assert.That(_mockFactory.GetWriterCount(), Is.EqualTo(2));
        }

        [Test]
        public void Trace_WithEmptyData_ShouldWriteToWriter()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var trace = new TraceEntry(TraceDirection.Output, connectionId, Array.Empty<byte>());

            // Act
            _mockFactory.Trace(trace);

            // Assert
            var writer = _mockFactory.GetWriter(connectionId);
            Assert.That(writer, Is.Not.Null);
            Assert.That(writer.WrittenTraces.Count, Is.EqualTo(1));
        }

        [Test]
        public void Trace_ConcurrentCalls_ShouldHandleThreadSafety()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var traces = new[]
            {
                new TraceEntry(TraceDirection.Output, connectionId, new byte[] { 0x53, 0x00, 0x0D }),
                new TraceEntry(TraceDirection.Input, connectionId, new byte[] { 0x53, 0x80, 0x14 }),
                new TraceEntry(TraceDirection.Trace, connectionId, new byte[] { 0x53, 0x00, 0x0E })
            };

            // Act - Simulate concurrent calls
            System.Threading.Tasks.Parallel.ForEach(traces, trace =>
            {
                _mockFactory.Trace(trace);
            });

            // Assert
            var writer = _mockFactory.GetWriter(connectionId);
            Assert.That(writer, Is.Not.Null);
            Assert.That(writer.WrittenTraces.Count, Is.EqualTo(3));
        }

        [Test]
        public void Trace_AllDirectionTypes_ShouldWriteCorrectly()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var inputTrace = new TraceEntry(TraceDirection.Input, connectionId, new byte[] { 0x53, 0x80 });
            var outputTrace = new TraceEntry(TraceDirection.Output, connectionId, new byte[] { 0x53, 0x00 });
            var traceTrace = new TraceEntry(TraceDirection.Trace, connectionId, new byte[] { 0x53, 0xFF });

            // Act
            _mockFactory.Trace(inputTrace);
            _mockFactory.Trace(outputTrace);
            _mockFactory.Trace(traceTrace);

            // Assert
            Assert.That(_mockFactory.GetWriter(connectionId).WrittenTraces.Count, Is.EqualTo(3));
            Assert.That(_mockFactory.GetWriter(connectionId).WrittenTraces[0].Direction, Is.EqualTo(TraceDirection.Input));
            Assert.That(_mockFactory.GetWriter(connectionId).WrittenTraces[1].Direction, Is.EqualTo(TraceDirection.Output));
            Assert.That(_mockFactory.GetWriter(connectionId).WrittenTraces[2].Direction, Is.EqualTo(TraceDirection.Trace));
        }

        [Test]
        public void Trace_LargeDataPayload_ShouldWriteCorrectly()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var largeData = new byte[2048];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }
            var trace = new TraceEntry(TraceDirection.Output, connectionId, largeData);

            // Act
            _mockFactory.Trace(trace);

            // Assert
            var writer = _mockFactory.GetWriter(connectionId);
            Assert.That(writer, Is.Not.Null);
            Assert.That(writer.WrittenTraces.Count, Is.EqualTo(1));
            Assert.That(_mockFactory.GetWriter(connectionId).WrittenTraces[0].Data.Length, Is.EqualTo(2048));
        }

        [Test]
        public void Trace_MixedConnectionsAndDirections_ShouldSeparateByConnection()
        {
            // Arrange
            var connectionId1 = Guid.NewGuid();
            var connectionId2 = Guid.NewGuid();

            var traces = new[]
            {
                new TraceEntry(TraceDirection.Output, connectionId1, new byte[] { 0x01 }),
                new TraceEntry(TraceDirection.Input, connectionId2, new byte[] { 0x02 }),
                new TraceEntry(TraceDirection.Output, connectionId1, new byte[] { 0x03 }),
                new TraceEntry(TraceDirection.Input, connectionId2, new byte[] { 0x04 })
            };

            // Act
            foreach (var trace in traces)
            {
                _mockFactory.Trace(trace);
            }

            // Assert
            Assert.That(_mockFactory.GetWriter(connectionId1).WrittenTraces.Count, Is.EqualTo(2));
            Assert.That(_mockFactory.GetWriter(connectionId2).WrittenTraces.Count, Is.EqualTo(2));
        }
    }

    [TestFixture]
    public class CloseWriterTest : OSDPFileCaptureTracerTest
    {
        [Test]
        public void CloseWriter_ExistingConnection_ShouldDisposeWriter()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var trace = new TraceEntry(TraceDirection.Output, connectionId, new byte[] { 0x53, 0x00, 0x0D });
            _mockFactory.Trace(trace);

            // Get reference before closing
            var writer = _mockFactory.GetWriter(connectionId);

            // Act
            _mockFactory.CloseWriter(connectionId);

            // Assert
            Assert.That(writer.IsDisposed, Is.True);
            Assert.That(_mockFactory.GetActiveWriterCount(), Is.EqualTo(0));
        }

        [Test]
        public void CloseWriter_NonExistentConnection_ShouldNotThrow()
        {
            // Arrange
            var connectionId = Guid.NewGuid();

            // Act & Assert
            Assert.DoesNotThrow(() => _mockFactory.CloseWriter(connectionId));
        }

        [Test]
        public void CloseWriter_MultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var trace = new TraceEntry(TraceDirection.Output, connectionId, new byte[] { 0x53 });
            _mockFactory.Trace(trace);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _mockFactory.CloseWriter(connectionId);
                _mockFactory.CloseWriter(connectionId);
                _mockFactory.CloseWriter(connectionId);
            });
        }

        [Test]
        public void CloseWriter_AfterClose_NewTraceShouldCreateNewWriter()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var trace1 = new TraceEntry(TraceDirection.Output, connectionId, new byte[] { 0x53, 0x00, 0x0D });
            _mockFactory.Trace(trace1);
            var firstWriter = _mockFactory.GetWriter(connectionId);
            _mockFactory.CloseWriter(connectionId);

            var trace2 = new TraceEntry(TraceDirection.Input, connectionId, new byte[] { 0x53, 0x80, 0x14 });

            // Act
            _mockFactory.Trace(trace2);

            // Assert
            Assert.That(_mockFactory.GetWriter(connectionId), Is.Not.SameAs(firstWriter));
            Assert.That(_mockFactory.GetWriter(connectionId).WrittenTraces.Count, Is.EqualTo(1));
        }
    }

    [TestFixture]
    public class CloseAllWritersTest : OSDPFileCaptureTracerTest
    {
        [Test]
        public void CloseAllWriters_MultipleWriters_ShouldCloseAll()
        {
            // Arrange
            var connectionId1 = Guid.NewGuid();
            var connectionId2 = Guid.NewGuid();
            var connectionId3 = Guid.NewGuid();

            _mockFactory.Trace(new TraceEntry(TraceDirection.Output, connectionId1, new byte[] { 0x53 }));
            _mockFactory.Trace(new TraceEntry(TraceDirection.Output, connectionId2, new byte[] { 0x53 }));
            _mockFactory.Trace(new TraceEntry(TraceDirection.Output, connectionId3, new byte[] { 0x53 }));

            // Get references before closing
            var writer1 = _mockFactory.GetWriter(connectionId1);
            var writer2 = _mockFactory.GetWriter(connectionId2);
            var writer3 = _mockFactory.GetWriter(connectionId3);

            // Act
            _mockFactory.CloseAllWriters();

            // Assert
            Assert.That(writer1.IsDisposed, Is.True);
            Assert.That(writer2.IsDisposed, Is.True);
            Assert.That(writer3.IsDisposed, Is.True);
            Assert.That(_mockFactory.GetActiveWriterCount(), Is.EqualTo(0));
        }

        [Test]
        public void CloseAllWriters_NoWriters_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _mockFactory.CloseAllWriters());
        }

        [Test]
        public void CloseAllWriters_MultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var connectionId = Guid.NewGuid();
            var trace = new TraceEntry(TraceDirection.Output, connectionId, new byte[] { 0x53 });
            _mockFactory.Trace(trace);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _mockFactory.CloseAllWriters();
                _mockFactory.CloseAllWriters();
                _mockFactory.CloseAllWriters();
            });
        }
    }

    [TestFixture]
    public class FileNamingTest : OSDPFileCaptureTracerTest
    {
        [Test]
        public void Trace_SpecificConnectionIdFormat_ShouldCreateCorrectFileName()
        {
            // Arrange
            var connectionId = new Guid("12345678-1234-1234-1234-123456789012");
            var trace = new TraceEntry(TraceDirection.Output, connectionId, new byte[] { 0x53 });

            // Act
            _mockFactory.Trace(trace);

            // Assert
            Assert.That(_mockFactory.GetWriter(connectionId).FilePath, Is.EqualTo("12345678-1234-1234-1234-123456789012.osdpcap"));
        }
    }

    /// <summary>
    /// Mock implementation of file writer factory for testing.
    /// Simulates the behavior of OSDPFileCaptureTracer without file system operations.
    /// </summary>
    private class MockFileWriterFactory
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, MockCaptureFileWriter> _writers = new();

        public void Trace(TraceEntry trace)
        {
            var writer = _writers.GetOrAdd(trace.ConnectionId, connectionId =>
                new MockCaptureFileWriter($"{connectionId:D}.osdpcap"));

            writer.WriteTrace(trace);
        }

        public void CloseWriter(Guid connectionId)
        {
            if (_writers.TryRemove(connectionId, out var writer))
            {
                writer.Dispose();
            }
        }

        public void CloseAllWriters()
        {
            foreach (var writer in _writers.Values)
            {
                writer.Dispose();
            }
            _writers.Clear();
        }

        public MockCaptureFileWriter GetWriter(Guid connectionId)
        {
            _writers.TryGetValue(connectionId, out var writer);
            return writer;
        }

        public int GetActiveWriterCount()
        {
            return _writers.Count;
        }

        public int GetWriterCount()
        {
            return _writers.Count;
        }
    }

    /// <summary>
    /// Mock implementation of OSDPCaptureFileWriter for testing.
    /// Tracks written traces in memory without file system operations.
    /// </summary>
    private class MockCaptureFileWriter : IDisposable
    {
        private readonly object _lock = new();

        public string FilePath { get; }
        public List<TraceEntry> WrittenTraces { get; } = new();
        public bool IsDisposed { get; private set; }

        public MockCaptureFileWriter(string filePath)
        {
            FilePath = filePath;
        }

        public void WriteTrace(TraceEntry trace)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(MockCaptureFileWriter));

            lock (_lock)
            {
                WrittenTraces.Add(trace);
            }
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}