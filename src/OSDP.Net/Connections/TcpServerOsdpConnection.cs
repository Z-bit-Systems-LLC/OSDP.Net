using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OSDP.Net.Connections
{
    /// <summary>
    /// TCP server OSDP connection that allows a ControlPanel (ACU) to act as a TCP server,
    /// accepting connections from OSDP devices.
    /// </summary>
    /// <remarks>
    /// This class combines TCP listening and connection handling in a single IOsdpConnection implementation,
    /// making it suitable for use with ControlPanel instances that need to accept incoming device connections.
    /// For scenarios where devices (PDs) need to accept ACU connections, use TcpConnectionListener instead.
    /// </remarks>
    public class TcpServerOsdpConnection : OsdpConnection
    {
        private readonly TcpListener _listener;
        private readonly ILogger _logger;
        private TcpClient _tcpClient;
        private NetworkStream _stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpServerOsdpConnection"/> class.
        /// </summary>
        /// <param name="portNumber">The TCP port number to listen on.</param>
        /// <param name="baudRate">The simulated baud rate for OSDP communication timing.</param>
        /// <param name="loggerFactory">Optional logger factory for diagnostic logging.</param>
        public TcpServerOsdpConnection(int portNumber, int baudRate, ILoggerFactory loggerFactory = null) : base(baudRate)
        {
            _listener = TcpListener.Create(portNumber);
            _logger = loggerFactory?.CreateLogger<TcpServerOsdpConnection>();
        }

        /// <inheritdoc />
        public override bool IsOpen
        {
            get
            {
                var tcpClient = _tcpClient;
                return tcpClient is { Connected: true };
            }
        }

        /// <inheritdoc />
        public override async Task Open()
        {
            try
            {
                _listener.Start();
                _logger?.LogInformation("TCP server listening on {@Endpoint} for device connections", _listener.LocalEndpoint);
                
                var newTcpClient = await _listener.AcceptTcpClientAsync();
                _logger?.LogInformation("Accepted device connection from {@RemoteEndpoint}", newTcpClient.Client.RemoteEndPoint);

                // Close any existing connection before accepting the new one
                await Close();

                _tcpClient = newTcpClient;
                _stream = _tcpClient.GetStream();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error opening TCP server connection");
                throw;
            }
        }

        /// <inheritdoc />
        public override Task Close()
        {
            try
            {
                _stream?.Dispose();
                _tcpClient?.Dispose();
                _stream = null;
                _tcpClient = null;
                
                _logger?.LogDebug("TCP server connection closed");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error closing TCP server connection");
            }
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override async Task WriteAsync(byte[] buffer)
        {
            try
            {
                var stream = _stream;
                if (stream != null)
                {
                    await stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error writing to TCP stream");
                // Don't set IsOpen to false here as the base class will handle a connection state
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
        {
            try
            {
                var stream = _stream;
                if (stream != null)
                {
                    var bytes = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytes == 0)
                    {
                        _logger?.LogInformation("TCP stream closed by remote device");
                    }
                    return bytes;
                }
                return 0;
            }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException)
                {
                    if (exception is IOException && exception.InnerException is SocketException)
                    {
                        _logger?.LogInformation("Device disconnected: {ExceptionMessage}", exception.Message);
                    }
                    else
                    {
                        _logger?.LogWarning(exception, "Error reading from TCP stream");
                    }
                }
                return 0;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return _listener?.LocalEndpoint.ToString() ?? "TcpServerOsdpConnection";
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var _ = Close();
                _listener?.Stop();
            }
            base.Dispose(disposing);
        }
    }
}