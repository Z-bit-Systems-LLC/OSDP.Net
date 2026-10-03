using System.Threading;
using System.Threading.Tasks;

namespace OSDP.Net.Connections
{
    /// <summary>
    /// A connection whose line rate can be changed while it stays open.
    /// </summary>
    /// <remarks>
    /// Most OSDP work fixes the baud rate for the life of a connection. The line quality test does
    /// not: it sweeps rates, and closing and reopening a serial port between each one is slow, can
    /// leave the handle briefly unavailable on Windows, and toggles the control lines in a way
    /// that disturbs the bus. This interface exists so that code which retunes a live connection
    /// can be written against something other than a physical port.
    /// </remarks>
    public interface IRetunableOsdpConnection : IOsdpConnection
    {
        /// <summary>
        /// Gets or sets a value indicating whether buffers are discarded before every write.
        /// </summary>
        /// <remarks>
        /// An ACU wants this on: it drives a strict command/reply cycle and benefits from a clean
        /// slate before each command. A device that replies to an incoming stream wants it off,
        /// because the next command can already be arriving while the previous reply is written.
        /// </remarks>
        bool DiscardBuffersBeforeWrite { get; set; }

        /// <summary>
        /// Changes the baud rate, retuning the connection in place if it is open.
        /// </summary>
        /// <param name="baudRate">The new baud rate.</param>
        void SetBaudRate(int baudRate);

        /// <summary>
        /// Waits until data already written has left the connection.
        /// </summary>
        /// <param name="frameByteCount">Number of bytes most recently written.</param>
        /// <param name="token">Token to observe while waiting.</param>
        /// <remarks>
        /// Required before <see cref="SetBaudRate"/> mid-conversation: a write returns once the
        /// bytes are accepted for transmission, not once they have left the wire, and changing the
        /// divisor early corrupts the tail of the message.
        /// </remarks>
        Task WaitForTransmitCompleteAsync(int frameByteCount, CancellationToken token = default);
    }
}
