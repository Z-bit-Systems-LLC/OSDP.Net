using System;
using System.Threading;
using System.Threading.Tasks;

namespace OSDP.Net.Connections
{
    /// <summary>
    /// Base OSDP connection class
    /// </summary>
    public abstract class OsdpConnection : IOsdpConnection
    {
        private bool _disposedValue;

        /// <summary>
        /// Creates an instance of OsdpConnection
        /// </summary>
        /// <param name="baudRate">Baud rate for OSDP comms</param>
        protected OsdpConnection(int baudRate)
        {
            BaudRate = baudRate;
        }   

        /// <inheritdoc/>
        public int BaudRate { get; }

        /// <inheritdoc/>
        public virtual bool IsOpen { get; protected set; }

        /// <inheritdoc/>
        public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// The time it takes to transmit the given number of bytes on this connection, used by the
        /// bus to honor the OSDP idle-line requirement. The default models a serial line at
        /// <see cref="BaudRate"/> (10 bit-times per byte). In-memory or otherwise instantaneous
        /// connections may override this to return <see cref="TimeSpan.Zero"/>.
        /// </summary>
        /// <param name="numberOfBytes">The number of bytes being transmitted.</param>
        public virtual TimeSpan IdleLineDelay(int numberOfBytes) =>
            TimeSpan.FromSeconds((1.0 / BaudRate) * (10.0 * numberOfBytes));

        /// <inheritdoc/>
        public abstract Task Close();

        /// <inheritdoc/>
        public abstract Task Open();

        /// <inheritdoc/>
        public abstract Task<int> ReadAsync(byte[] buffer, CancellationToken token);

        /// <inheritdoc/>
        public abstract Task WriteAsync(byte[] buffer);

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the OsdpConnection instance.
        /// </summary>
        /// <remarks>
        /// This method is responsible for releasing any resources used by the OsdpConnection instance. It calls the <see cref="Close"/> method to close the connection if it is open and sets
        /// the <see cref="_disposedValue"/> field to true to indicate that the instance has been disposed.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Close();
                }

                _disposedValue = true;
            }
        }
    }
}
