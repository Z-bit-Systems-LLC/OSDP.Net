using System;
using System.IO;
using System.Text.Json;

namespace OSDP.Net.Tracing;

/// <summary>
/// Writes OSDP packet captures to .osdpcap file format.
/// This format is compatible with OSDP trace analyzers and can be used for debugging and analysis.
/// </summary>
public class OSDPCaptureFileWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly string _source;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OSDPCaptureFileWriter"/> class.
    /// </summary>
    /// <param name="filePath">The path to the output .osdpcap file.</param>
    /// <param name="source">The source identifier for the capture (e.g., "OSDP.Net", "PassiveMonitor").</param>
    /// <param name="append">If true, appends to the existing file; otherwise creates a new file.</param>
    /// <exception cref="ArgumentNullException">Thrown when filePath or source is null.</exception>
    public OSDPCaptureFileWriter(string filePath, string source, bool append = true)
        : this(filePath, source, new FileSystemAdapter(), append)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OSDPCaptureFileWriter"/> class with a custom file system.
    /// </summary>
    /// <param name="filePath">The path to the output .osdpcap file.</param>
    /// <param name="source">The source identifier for the capture (e.g., "OSDP.Net", "PassiveMonitor").</param>
    /// <param name="fileSystem">The file system abstraction to use.</param>
    /// <param name="append">If true, appends to the existing file; otherwise creates a new file.</param>
    /// <exception cref="ArgumentNullException">Thrown when filePath or source is null.</exception>
    internal OSDPCaptureFileWriter(string filePath, string source, IFileSystem fileSystem, bool append = true)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));
        if (string.IsNullOrEmpty(source))
            throw new ArgumentNullException(nameof(source));

        var directory = fileSystem.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            fileSystem.CreateDirectory(directory);

        _source = source;
        _writer = fileSystem.CreateStreamWriter(filePath, append);
    }

    /// <summary>
    /// Writes a packet to the capture file.
    /// </summary>
    /// <param name="data">The raw packet data.</param>
    /// <param name="direction">The direction of the packet (Input, Output, or Trace).</param>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the writer has been disposed.</exception>
    public void WritePacket(byte[] data, TraceDirection direction)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OSDPCaptureFileWriter));
        if (data == null) throw new ArgumentNullException(nameof(data));

        WritePacketInternal(data, direction, DateTime.UtcNow);
    }

    /// <summary>
    /// Writes a packet to the capture file with a specific timestamp.
    /// </summary>
    /// <param name="data">The raw packet data.</param>
    /// <param name="direction">The direction of the packet (Input, Output, or Trace).</param>
    /// <param name="timestamp">The timestamp for the packet (in UTC).</param>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the writer has been disposed.</exception>
    public void WritePacket(byte[] data, TraceDirection direction, DateTime timestamp)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OSDPCaptureFileWriter));
        if (data == null) throw new ArgumentNullException(nameof(data));

        WritePacketInternal(data, direction, timestamp);
    }

    /// <summary>
    /// Writes a trace entry to the capture file.
    /// This method is compatible with Action&lt;TraceEntry&gt; callbacks used in the library.
    /// </summary>
    /// <param name="trace">The trace entry to write.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the writer has been disposed.</exception>
    public void WriteTrace(TraceEntry trace)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OSDPCaptureFileWriter));

        WritePacketInternal(trace.Data, trace.Direction, DateTime.UtcNow);
    }

    private void WritePacketInternal(byte[] data, TraceDirection direction, DateTime timestamp)
    {
        var unixTime = timestamp.Subtract(new DateTime(1970, 1, 1));
        long timeSec = (long)Math.Floor(unixTime.TotalSeconds);
        long timeNano = (unixTime.Ticks - timeSec * TimeSpan.TicksPerSecond) * 100L;

        string ioValue = direction switch
        {
            TraceDirection.Input => "input",
            TraceDirection.Output => "output",
            TraceDirection.Trace => "trace",
            _ => "unknown"
        };

        var entry = new
        {
            timeSec = timeSec.ToString("F0"),
            timeNano = timeNano.ToString("000000000"),
            io = ioValue,
            data = BitConverter.ToString(data),
            osdpTraceVersion = "1",
            osdpSource = _source
        };

        string json = JsonSerializer.Serialize(entry);
        _writer.WriteLine(json);
        _writer.Flush(); // Ensure data is written immediately for real-time monitoring
    }

    /// <summary>
    /// Releases all resources used by the <see cref="OSDPCaptureFileWriter"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by the <see cref="OSDPCaptureFileWriter"/>.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _writer?.Dispose();
            }
            _disposed = true;
        }
    }
}
