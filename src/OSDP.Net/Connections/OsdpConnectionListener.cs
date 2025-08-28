using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OSDP.Net.Connections;

/// <summary>
/// Base class for OSDP connection listeners that accept incoming connections from Access Control Units (ACUs).
/// </summary>
/// <remarks>
/// This abstract class provides the foundation for transport-specific listeners (TCP, Serial) that 
/// OSDP Peripheral Devices use to accept connections. Despite the previous "Server" naming, this class
/// does not implement an OSDP protocol server. Instead, it manages transport-layer connections that
/// enable OSDP communication between ACUs (masters) and PDs (slaves).
/// </remarks>
public abstract class OsdpConnectionListener : IOsdpConnectionListener
{
    private bool _disposedValue;
    private readonly ConcurrentDictionary<Task, OsdpConnection> _connections = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OsdpConnectionListener"/> class.
    /// </summary>
    /// <param name="baudRate">The baud rate for serial connections. May not apply to TCP listeners.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostic logging.</param>
    protected OsdpConnectionListener(int baudRate, ILoggerFactory loggerFactory = null)
    {
        LoggerFactory = loggerFactory;
        Logger = loggerFactory?.CreateLogger<OsdpConnectionListener>();
        BaudRate = baudRate;
    }

    /// <inheritdoc/>
    public bool IsRunning { get; protected set; }

    /// <inheritdoc/>
    public int ConnectionCount => _connections.Count;

    /// <summary>
    /// Gets the logger factory instance if one was provided during instantiation.
    /// </summary>
    protected ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Gets the logger instance for this listener if a logger factory was provided.
    /// </summary>
    protected ILogger Logger { get; }

    /// <inheritdoc/>
    public int BaudRate { get; }

    /// <inheritdoc/>
    public abstract Task Start(Func<IOsdpConnection, Task> newConnectionHandler);

    /// <inheritdoc/>
    public virtual async Task Stop()
    {
        IsRunning = false;

        Logger?.LogDebug("Stopping OSDP connection listener...");

        while (true)
        {
            var entries = _connections.ToArray();
            if (entries.Length == 0) break;

            await Task.WhenAll(entries.Select(item => item.Value.Close()));
            await Task.WhenAll(entries.Select(x => x.Key));
        }

        Logger?.LogDebug("OSDP connection listener stopped");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Registers a new connection and its associated handling task with the listener.
    /// </summary>
    /// <param name="connection">The OSDP connection to register.</param>
    /// <param name="task">The task that handles communication for this connection.</param>
    /// <remarks>
    /// This method should be called by derived classes when they create a new connection
    /// in response to an incoming transport connection (TCP accept, serial port open, etc.).
    /// The listener tracks all active connections and ensures they are properly closed when
    /// the listener stops.
    /// </remarks>
    protected void RegisterConnection(OsdpConnection connection, Task task)
    {
        Task.Run(async () =>
        {
            _connections.TryAdd(task, connection);
            if (!IsRunning) await connection.Close();
            Logger?.LogDebug("New OSDP connection opened - total connections: {ConnectionCount}", _connections.Count);
            await task;
            _connections.TryRemove(task, out _);
            Logger?.LogDebug("OSDP connection terminated - remaining connections: {ConnectionCount}", _connections.Count);
        });
    }

    /// <summary>
    /// Releases the resources used by the <see cref="OsdpConnectionListener"/> instance.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources; false if finalizing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                var _ = Stop();
            }

            _disposedValue = true;
        }
    }
}