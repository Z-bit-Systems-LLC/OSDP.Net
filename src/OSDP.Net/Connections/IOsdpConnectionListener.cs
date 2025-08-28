using System;
using System.Threading.Tasks;

namespace OSDP.Net.Connections;

/// <summary>
/// Defines a connection listener for OSDP Peripheral Devices (PDs) that need to accept 
/// incoming connections from Access Control Units (ACUs).
/// </summary>
/// <remarks>
/// In the OSDP protocol, ACUs (Control Panels) are masters that initiate communication,
/// while PDs (Peripheral Devices) are slaves that respond to commands. This interface
/// represents a transport-layer listener that PDs use to accept incoming connections
/// from ACUs. It is not an "OSDP server" in the protocol sense, but rather a connection
/// factory that creates IOsdpConnection instances when transport connections are established.
/// </remarks>
public interface IOsdpConnectionListener : IDisposable
{
    /// <summary>
    /// Gets the baud rate for serial connections. For TCP connections, this value may not be applicable.
    /// </summary>
    int BaudRate { get; }

    /// <summary>
    /// Starts listening for incoming connections from ACUs.
    /// </summary>
    /// <param name="newConnectionHandler">Callback invoked when a new connection is accepted. 
    /// The callback receives the IOsdpConnection instance representing the established connection.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Start(Func<IOsdpConnection, Task> newConnectionHandler);

    /// <summary>
    /// Stops the listener and terminates any active connections.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Stop();

    /// <summary>
    /// Gets a value indicating whether the listener is currently running and accepting connections.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the number of active connections currently being managed by this listener.
    /// </summary>
    int ConnectionCount { get; }
}