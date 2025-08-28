using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OSDP.Net.Connections;

/// <summary>
/// Implements a TCP/IP connection listener for OSDP Peripheral Devices to accept incoming connections from ACUs.
/// </summary>
/// <remarks>
/// This listener allows OSDP devices to accept TCP connections from Access Control Units. When an ACU
/// connects via TCP, this listener creates a new IOsdpConnection instance to handle the OSDP communication
/// over that TCP connection. This is commonly used when PDs need to be accessible over network connections
/// rather than traditional serial connections.
/// </remarks>
public class TcpConnectionListener : OsdpConnectionListener
{
    private readonly TcpListener _listener;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpConnectionListener"/> class.
    /// </summary>
    /// <param name="portNumber">The TCP port number to listen on for incoming connections.</param>
    /// <param name="baudRate">The simulated baud rate for OSDP communication timing.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostic logging.</param>
    public TcpConnectionListener(
        int portNumber, int baudRate, ILoggerFactory loggerFactory = null) : base(baudRate, loggerFactory)
    {
        _listener = TcpListener.Create(portNumber);
    }

    /// <inheritdoc/>
    public override Task Start(Func<IOsdpConnection, Task> newConnectionHandler)
    {
        if (IsRunning) return Task.CompletedTask;

        IsRunning = true;
        _listener.Start();

        Logger?.LogInformation("TCP listener started on {Endpoint} for incoming OSDP connections", _listener.LocalEndpoint.ToString());

        Task.Run(async () =>
        {
            while (IsRunning)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    Logger?.LogDebug("Accepted TCP connection from {RemoteEndpoint}", client.Client.RemoteEndPoint?.ToString());

                    var connection = new TcpOsdpConnection(client, BaudRate, LoggerFactory);
                    var task = newConnectionHandler(connection);
                    RegisterConnection(connection, task);
                }
                catch (ObjectDisposedException)
                {
                    // Expected when stopping the listener
                    break;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Error accepting TCP connection");
                }
            }
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task Stop()
    {
        IsRunning = false;
        _listener.Stop();
        Logger?.LogInformation("TCP listener stopped");
        return base.Stop();
    }
}