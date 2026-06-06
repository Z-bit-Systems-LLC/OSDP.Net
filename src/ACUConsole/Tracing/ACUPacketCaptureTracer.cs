using System;
using System.Collections.Concurrent;
using System.IO;
using OSDP.Net.Tracing;

namespace ACUConsole.Tracing
{
    /// <summary>
    /// Manages packet capture for ACUConsole, writing both raw .osdpcap files and parsed .txt files.
    /// </summary>
    internal sealed class ACUPacketCaptureTracer : IDisposable
    {
        private const string CaptureDirectory = "captures";
        private const string FilePrefix = "acu-capture";

        private readonly ConcurrentDictionary<Guid, CaptureWriters> _writers = new();
        private readonly string _captureDirectoryPath;
        private bool _disposed;

        public ACUPacketCaptureTracer()
        {
            _captureDirectoryPath = CaptureDirectory;

            if (!Directory.Exists(_captureDirectoryPath))
            {
                Directory.CreateDirectory(_captureDirectoryPath);
            }
        }

        public void Trace(TraceEntry trace)
        {
            if (_disposed) return;

            try
            {
                var writers = _writers.GetOrAdd(trace.ConnectionId, CreateWriters);
                var timestamp = DateTime.Now;

                writers.CaptureWriter.WriteTrace(trace);
                writers.WritePacket(trace.Data, timestamp);
            }
            catch (Exception)
            {
                // Silently ignore errors to prevent disrupting the connection
            }
        }

        private CaptureWriters CreateWriters(Guid connectionId)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var basePath = Path.Combine(_captureDirectoryPath, $"{FilePrefix}-{timestamp}");
            var osdpcapPath = $"{basePath}.osdpcap";
            var txtPath = $"{basePath}.txt";

            return new CaptureWriters(osdpcapPath, txtPath);
        }

        public void CloseWriter(Guid connectionId)
        {
            if (_writers.TryRemove(connectionId, out var writers))
            {
                writers.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var writers in _writers.Values)
            {
                writers.Dispose();
            }
            _writers.Clear();
        }

        private sealed class CaptureWriters : IDisposable
        {
            public OSDPCaptureFileWriter CaptureWriter { get; }
            private readonly StreamWriter _textWriter;
            private readonly MessageSpy _messageSpy;
            private readonly IPacketTextFormatter _formatter;
            private DateTime _lastPacketTime = DateTime.MinValue;

            public CaptureWriters(string osdpcapPath, string txtPath)
            {
                CaptureWriter = new OSDPCaptureFileWriter(osdpcapPath, "ACUConsole", append: true);
                _textWriter = new StreamWriter(txtPath, append: true);
                _messageSpy = new MessageSpy();
                _formatter = new OSDPPacketTextFormatter();
            }

            public void WritePacket(byte[] packetData, DateTime timestamp)
            {
                TimeSpan? delta = _lastPacketTime > DateTime.MinValue
                    ? timestamp - _lastPacketTime
                    : null;
                _lastPacketTime = timestamp;

                try
                {
                    var packet = _messageSpy.ParsePacket(packetData);
                    _textWriter.Write(_formatter.FormatPacket(packet, timestamp, delta));
                    _textWriter.Flush();
                }
                catch (Exception ex)
                {
                    _textWriter.Write(_formatter.FormatError(packetData, timestamp, delta, ex.Message));
                    _textWriter.Flush();
                }
            }

            public void Dispose()
            {
                CaptureWriter.Dispose();
                _textWriter.Dispose();
            }
        }
    }
}
