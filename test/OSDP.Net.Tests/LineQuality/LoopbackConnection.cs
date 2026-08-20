using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OSDP.Net.Connections;

namespace OSDP.Net.Tests.LineQuality
{
    /// <summary>
    /// An in-memory stand-in for a pair of serial ports wired together, so the line quality
    /// controller and responder can be driven against each other without hardware.
    /// </summary>
    /// <remarks>
    /// Baud rate changes are recorded but have no effect on delivery, which is exactly the
    /// limitation a virtual serial port pair has. That makes this useful for protocol and state
    /// machine coverage, and useless for timing or signal integrity: those need real adapters.
    /// The write filter exists so tests can drop or corrupt traffic and exercise the error
    /// taxonomy the report depends on.
    /// </remarks>
    internal sealed class LoopbackConnection : OsdpConnection, IRetunableOsdpConnection
    {
        private readonly ConcurrentQueue<byte> _inbound;
        private readonly SemaphoreSlim _inboundSignal;
        private readonly ConcurrentQueue<byte> _outbound;
        private readonly SemaphoreSlim _outboundSignal;

        private LoopbackConnection(int baudRate,
            ConcurrentQueue<byte> inbound, SemaphoreSlim inboundSignal,
            ConcurrentQueue<byte> outbound, SemaphoreSlim outboundSignal) : base(baudRate)
        {
            _inbound = inbound;
            _inboundSignal = inboundSignal;
            _outbound = outbound;
            _outboundSignal = outboundSignal;
            IsOpen = true;
        }

        /// <summary>
        /// Creates two connections wired to each other.
        /// </summary>
        public static (LoopbackConnection Controller, LoopbackConnection Responder) CreatePair(int baudRate)
        {
            var controllerToResponder = new ConcurrentQueue<byte>();
            var controllerToResponderSignal = new SemaphoreSlim(0);
            var responderToController = new ConcurrentQueue<byte>();
            var responderToControllerSignal = new SemaphoreSlim(0);

            var controller = new LoopbackConnection(baudRate,
                responderToController, responderToControllerSignal,
                controllerToResponder, controllerToResponderSignal);

            var responder = new LoopbackConnection(baudRate,
                controllerToResponder, controllerToResponderSignal,
                responderToController, responderToControllerSignal);

            return (controller, responder);
        }

        /// <summary>
        /// Gets or sets a filter applied to everything written. Return null to drop the write
        /// entirely, or a modified array to corrupt it.
        /// </summary>
        public Func<byte[], byte[]> WriteFilter { get; set; }

        /// <summary>Gets the number of frames this connection has written.</summary>
        public int FramesWritten { get; private set; }

        /// <summary>Gets the baud rates this connection has been retuned to, in order.</summary>
        public ConcurrentQueue<int> BaudRateHistory { get; } = new ConcurrentQueue<int>();

        public bool DiscardBuffersBeforeWrite { get; set; } = true;

        public void SetBaudRate(int baudRate)
        {
            BaudRate = baudRate;
            BaudRateHistory.Enqueue(baudRate);
        }

        public Task WaitForTransmitCompleteAsync(int frameByteCount, CancellationToken token = default) =>
            Task.CompletedTask;

        public override Task Open()
        {
            IsOpen = true;
            return Task.CompletedTask;
        }

        public override Task Close()
        {
            IsOpen = false;
            return Task.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer)
        {
            if (DiscardBuffersBeforeWrite)
            {
                DrainInbound();
            }

            FramesWritten++;

            byte[] data = buffer;
            if (WriteFilter != null)
            {
                data = WriteFilter(buffer);
                if (data == null) return Task.CompletedTask;
            }

            foreach (byte value in data)
            {
                _outbound.Enqueue(value);
                _outboundSignal.Release();
            }

            return Task.CompletedTask;
        }

        public override async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
        {
            await _inboundSignal.WaitAsync(token).ConfigureAwait(false);

            int count = 0;
            if (_inbound.TryDequeue(out byte first))
            {
                buffer[count++] = first;
            }

            // Take whatever else is already queued, up to the caller's buffer size.
            while (count < buffer.Length && _inboundSignal.Wait(0))
            {
                if (_inbound.TryDequeue(out byte next))
                {
                    buffer[count++] = next;
                }
                else
                {
                    _inboundSignal.Release();
                    break;
                }
            }

            return count;
        }

        private void DrainInbound()
        {
            while (_inboundSignal.Wait(0))
            {
                _inbound.TryDequeue(out _);
            }
        }
    }
}
