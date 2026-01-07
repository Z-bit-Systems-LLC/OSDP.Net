using System;
using System.Threading.Tasks;
using OSDP.Net.Connections;

namespace OSDP.Net.Tests.Utilities;

/// <summary>
/// A connection listener that provides a single pre-created connection.
/// Used for fast unit testing without real network listeners.
/// </summary>
internal sealed class SingleConnectionListener : IOsdpConnectionListener
{
    private readonly LoopbackOsdpConnection _connection;
    private Task _connectionTask;

    /// <summary>
    /// Creates a single connection listener with the specified connection.
    /// </summary>
    /// <param name="connection">The pre-created connection to provide when started.</param>
    public SingleConnectionListener(LoopbackOsdpConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        BaudRate = connection.BaudRate;
    }

    /// <inheritdoc />
    public int BaudRate { get; }

    /// <inheritdoc />
    public bool IsRunning { get; private set; }

    /// <inheritdoc />
    public int ConnectionCount => IsRunning && _connection.IsOpen ? 1 : 0;

    /// <inheritdoc />
    public Task Start(Func<IOsdpConnection, Task> newConnectionHandler)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        IsRunning = true;
        _connectionTask = newConnectionHandler(_connection);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task Stop()
    {
        IsRunning = false;

        await _connection.Close().ConfigureAwait(false);

        if (_connectionTask != null)
        {
            await _connectionTask.ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IsRunning = false;
        _connection.Dispose();
    }
}
