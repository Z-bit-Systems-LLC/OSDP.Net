using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OSDP.Net.Connections;
using OSDP.Net.Tracing;

namespace PDConsole.Tracing
{
    /// <summary>
    /// Decorates an <see cref="IOsdpConnection"/> to capture OSDP traffic for tracing.
    /// Outgoing replies are written as complete packets, while the incoming byte stream is
    /// reassembled into complete OSDP frames before being reported to the tracer.
    /// </summary>
    internal sealed class TracingOsdpConnection(IOsdpConnection inner, Action<TraceEntry> tracer) : IOsdpConnection
    {
        private const byte StartOfMessage = 0x53;

        // OSDP packet length bounds used to validate the framing length field while resyncing.
        private const int MinPacketLength = 5;
        private const int MaxPacketLength = 1440;

        private readonly List<byte> _readBuffer = new();

        public Guid ConnectionId { get; } = Guid.NewGuid();

        public int BaudRate => inner.BaudRate;

        public bool IsOpen => inner.IsOpen;

        public TimeSpan ReplyTimeout
        {
            get => inner.ReplyTimeout;
            set => inner.ReplyTimeout = value;
        }

        public Task Open() => inner.Open();

        public Task Close() => inner.Close();

        public async Task WriteAsync(byte[] buffer)
        {
            // A reply is written as a single complete packet, so it can be traced directly.
            if (buffer is { Length: > 0 })
            {
                tracer(new TraceEntry(TraceDirection.Output, ConnectionId, buffer));
            }

            await inner.WriteAsync(buffer);
        }

        public async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
        {
            int read = await inner.ReadAsync(buffer, token);

            if (read > 0)
            {
                foreach (var frame in ExtractFrames(buffer, read))
                {
                    tracer(new TraceEntry(TraceDirection.Input, ConnectionId, frame));
                }
            }

            return read;
        }

        public void Dispose() => inner.Dispose();

        /// <summary>
        /// Appends newly read bytes to the rolling buffer and yields any complete OSDP frames,
        /// resynchronizing to the start-of-message marker when framing is lost.
        /// </summary>
        private IEnumerable<byte[]> ExtractFrames(byte[] data, int count)
        {
            for (int i = 0; i < count; i++)
            {
                _readBuffer.Add(data[i]);
            }

            var frames = new List<byte[]>();

            while (true)
            {
                int somIndex = _readBuffer.IndexOf(StartOfMessage);
                if (somIndex < 0)
                {
                    _readBuffer.Clear();
                    break;
                }

                if (somIndex > 0)
                {
                    _readBuffer.RemoveRange(0, somIndex);
                }

                if (_readBuffer.Count < 4)
                {
                    break; // Need the 4-byte header (SOM, address, length LSB/MSB).
                }

                int length = _readBuffer[2] | (_readBuffer[3] << 8);
                if (length is < MinPacketLength or > MaxPacketLength)
                {
                    _readBuffer.RemoveAt(0); // Implausible length; drop SOM and resync.
                    continue;
                }

                if (_readBuffer.Count < length)
                {
                    break; // Wait for the rest of the packet.
                }

                frames.Add(_readBuffer.GetRange(0, length).ToArray());
                _readBuffer.RemoveRange(0, length);
            }

            return frames;
        }
    }
}
