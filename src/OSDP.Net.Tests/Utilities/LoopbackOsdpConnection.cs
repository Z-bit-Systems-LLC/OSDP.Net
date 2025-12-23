using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using OSDP.Net.Connections;

namespace OSDP.Net.Tests.Utilities;

/// <summary>
/// An in-memory OSDP connection that uses pipes for bidirectional communication.
/// Used for fast unit testing without real network I/O.
/// </summary>
internal sealed class LoopbackOsdpConnection : OsdpConnection
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private bool _disposed;

    /// <summary>
    /// Creates a loopback connection with the specified reader and writer pipes.
    /// </summary>
    /// <param name="reader">The pipe reader for incoming data.</param>
    /// <param name="writer">The pipe writer for outgoing data.</param>
    /// <param name="baudRate">The simulated baud rate.</param>
    /// <param name="preOpen">If true, the connection starts in the open state (for device side).</param>
    private LoopbackOsdpConnection(PipeReader reader, PipeWriter writer, int baudRate, bool preOpen = false) : base(baudRate)
    {
        _reader = reader;
        _writer = writer;
        if (preOpen)
        {
            IsOpen = true;
        }
    }

    /// <summary>
    /// Creates a pair of connected loopback connections for ACU and Device communication.
    /// </summary>
    /// <param name="baudRate">The simulated baud rate for both connections.</param>
    /// <returns>A tuple containing the ACU-side and Device-side connections.</returns>
    public static (LoopbackOsdpConnection AcuConnection, LoopbackOsdpConnection DeviceConnection) CreatePair(
        int baudRate = 9600)
    {
        var acuToDevice = new Pipe();
        var deviceToAcu = new Pipe();

        var acuConnection = new LoopbackOsdpConnection(
            deviceToAcu.Reader,
            acuToDevice.Writer,
            baudRate);

        var deviceConnection = new LoopbackOsdpConnection(
            acuToDevice.Reader,
            deviceToAcu.Writer,
            baudRate,
            preOpen: true);

        return (acuConnection, deviceConnection);
    }

    /// <inheritdoc />
    public override bool IsOpen => base.IsOpen && !_disposed;

    /// <inheritdoc />
    public override Task Open()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LoopbackOsdpConnection));
        }

        IsOpen = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task Close()
    {
        IsOpen = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer)
    {
        if (!IsOpen)
        {
            return;
        }

        try
        {
            await _writer.WriteAsync(buffer).ConfigureAwait(false);
            await _writer.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            IsOpen = false;
        }
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
    {
        if (!IsOpen)
        {
            return 0;
        }

        try
        {
            var result = await _reader.ReadAsync(token).ConfigureAwait(false);
            var readBuffer = result.Buffer;

            if (result.IsCanceled || result.IsCompleted && readBuffer.IsEmpty)
            {
                IsOpen = false;
                return 0;
            }

            var bytesToCopy = (int)Math.Min(buffer.Length, readBuffer.Length);
            readBuffer.Slice(0, bytesToCopy).CopyTo(buffer.AsSpan());
            _reader.AdvanceTo(readBuffer.GetPosition(bytesToCopy));

            return bytesToCopy;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception)
        {
            IsOpen = false;
            return 0;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _writer.Complete();
            _reader.Complete();
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}
