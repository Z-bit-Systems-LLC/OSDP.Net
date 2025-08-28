using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OSDP.Net.Connections;

/// <summary>
/// Represents a TCP-based OSDP connection that wraps an already-established TCP client connection.
/// </summary>
/// <remarks>
/// This class is designed to work with connection listeners that accept TCP connections and then
/// create instances of this class to handle the OSDP communication over the established TCP connection.
/// It does not handle the listening aspect - that responsibility belongs to connection listeners
/// like TcpConnectionListener.
/// </remarks>
internal sealed class TcpOsdpConnection : OsdpConnection
{
    private readonly ILogger _logger;
    private TcpClient _tcpClient;
    private NetworkStream _stream;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpOsdpConnection"/> class.
    /// </summary>
    /// <param name="tcpClient">An already-connected TCP client.</param>
    /// <param name="baudRate">The simulated baud rate for OSDP communication timing.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostic logging.</param>
    public TcpOsdpConnection(
        TcpClient tcpClient, int baudRate, ILoggerFactory loggerFactory) : base(baudRate)
    {
        IsOpen = true;
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
        _logger = loggerFactory?.CreateLogger<TcpOsdpConnection>();
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
    {
        try
        {
            var bytes = await _stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            if (bytes == 0) { IsOpen = false; }
            return bytes;
        }
        catch (Exception exception) 
        {
            if (exception is not OperationCanceledException && IsOpen)
            {
                if (exception is IOException && exception.InnerException is SocketException)
                {
                    _logger?.LogInformation("Error reading TCP stream: {ExceptionMessage}", exception.Message);
                } 
                else
                {
                    _logger?.LogWarning(exception, "Error reading TCP stream");
                }
                    
                IsOpen = false;
            }
            return 0;
        }
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer)
    {
        try
        {
            await _stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (IsOpen)
            {
                _logger?.LogWarning(ex, "Error writing TCP stream");
                IsOpen = false;
            }
        }
    }
        
    /// <inheritdoc />
    /// <remarks>
    /// This method is not supported because the connection is already established when this class is instantiated.
    /// </remarks>
    public override Task Open() => throw new NotSupportedException("Connection is already established");

    /// <inheritdoc />
    public override Task Close()
    {
        IsOpen = false;
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _stream = null;
        _tcpClient = null;
        return Task.CompletedTask;
    }
}