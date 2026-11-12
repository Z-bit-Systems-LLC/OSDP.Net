using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OSDP.Net.Connections;

namespace OSDP.Net.Tests.Utilities;

/// <summary>
/// A pretend RS-485 cable between an ACU end and a PD end. Bytes take as long to cross as they would
/// at the line rate, and the receiving end is looked up when they arrive, not when they are sent, so
/// a reply still on the wire when the ACU closes and reopens its port lands in the new session, as it
/// does on a real line. The PD end is always open, like a device on the bus.
/// </summary>
internal sealed class TimedWire
{
    private readonly object _lock = new();
    private WireEnd _acu;
    private Task _toPd = Task.CompletedTask;
    private Task _toAcu = Task.CompletedTask;

    public TimedWire(int baudRate = 9600)
    {
        BaudRate = baudRate;
        AcuEnd = new WireEnd(this, isPd: false, baudRate);
        PdEnd = new WireEnd(this, isPd: true, baudRate);
    }

    public int BaudRate { get; }

    public WireEnd AcuEnd { get; }

    public WireEnd PdEnd { get; }

    /// <summary>
    /// Raised with each chunk as it reaches the PD end.
    /// </summary>
    public event Action<byte[]> PdReceived;

    private TimeSpan WireTime(int byteCount) => TimeSpan.FromSeconds(byteCount * 10.0 / BaudRate);

    private Task Send(WireEnd from, byte[] data)
    {
        var copy = (byte[])data.Clone();
        lock (_lock)
        {
            if (from.IsPd)
            {
                return _toAcu = Deliver(_toAcu, () => _acu, copy);
            }

            return _toPd = Deliver(_toPd, () => PdEnd, copy);
        }
    }

    private async Task Deliver(Task previous, Func<WireEnd> target, byte[] data)
    {
        await previous.ConfigureAwait(false);
        await Task.Delay(WireTime(data.Length)).ConfigureAwait(false);
        WireEnd end;
        lock (_lock) end = target();
        end?.Receive(data);
        if (end == PdEnd) PdReceived?.Invoke(data);
    }

    private void Attach(WireEnd acu)
    {
        lock (_lock) _acu = acu;
    }

    private void Detach(WireEnd acu)
    {
        lock (_lock)
        {
            if (_acu == acu) _acu = null;
        }
    }

    internal sealed class WireEnd : OsdpConnection
    {
        private readonly TimedWire _wire;
        private Channel<byte[]> _incoming = Channel.CreateUnbounded<byte[]>();
        private byte[] _pending = [];
        private int _pendingOffset;

        public WireEnd(TimedWire wire, bool isPd, int baudRate) : base(baudRate)
        {
            _wire = wire;
            IsPd = isPd;
            if (isPd) IsOpen = true;
        }

        public bool IsPd { get; }

        public override Task Open()
        {
            if (IsPd || IsOpen) return Task.CompletedTask;
            _incoming = Channel.CreateUnbounded<byte[]>();
            _pending = [];
            _pendingOffset = 0;
            _wire.Attach(this);
            IsOpen = true;
            return Task.CompletedTask;
        }

        public override Task Close()
        {
            if (IsPd || !IsOpen) return Task.CompletedTask;
            IsOpen = false;
            _wire.Detach(this);
            _incoming.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer)
        {
            if (!IsOpen) return Task.CompletedTask;
            _ = _wire.Send(this, buffer);
            return Task.CompletedTask;
        }

        public override async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
        {
            if (!IsOpen) return 0;

            try
            {
                if (_pendingOffset >= _pending.Length)
                {
                    _pending = await _incoming.Reader.ReadAsync(token).ConfigureAwait(false);
                    _pendingOffset = 0;
                }
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (ChannelClosedException)
            {
                return 0;
            }

            var count = Math.Min(buffer.Length, _pending.Length - _pendingOffset);
            Array.Copy(_pending, _pendingOffset, buffer, 0, count);
            _pendingOffset += count;
            return count;
        }

        internal void Receive(byte[] data) => _incoming.Writer.TryWrite(data);

        public override string ToString() => IsPd ? "PD end" : "ACU end";
    }
}
