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
        /// <remarks>
        /// The setter is protected because most connection types fix their rate at construction.
        /// Types that can retune a live connection, such as
        /// <see cref="SerialPortOsdpConnection"/>, expose a public method to do so.
        /// </remarks>
        public int BaudRate { get; protected set; }

        /// <inheritdoc/>
        public virtual bool IsOpen { get; protected set; }

        /// <inheritdoc/>
        public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

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
