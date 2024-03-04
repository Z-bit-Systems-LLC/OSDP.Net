using System;
using System.Threading;
using System.Threading.Tasks;

namespace OSDP.Net.Connections
{
    /// <summary>
    /// Defines a connection for communicating via OSDP protocol
    /// </summary>
    public interface IOsdpConnection : IDisposable
    {
        /// <summary>Speed of the connection</summary>
        int BaudRate { get; }

        /// <summary>
        /// Is the connection open and ready to communicate
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Timeout value waiting for a reply
        /// </summary>
        TimeSpan ReplyTimeout { get; set; }

        /// <summary>
        /// Open the connection for communications
        /// </summary>
        Task Open();

        /// <summary>
        /// Close the connection for communications
        /// </summary>
        Task Close();

        /// <summary>
        /// Write to connection
        /// </summary>
        /// <param name="buffer">Array of bytes to write</param>
        Task WriteAsync(byte[] buffer);

        /// <summary>
        /// Read from connection
        /// </summary>
        /// <param name="buffer">Array of bytes to read</param>
        /// <param name="token">Cancellation token to end reading of bytes</param>
        /// <returns>Number of actual bytes read</returns>
        Task<int> ReadAsync(byte[] buffer, CancellationToken token);
    }
}
