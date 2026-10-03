using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OSDP.Net.Connections
{
    /// <summary>Connect using a serial port.</summary>
    public class SerialPortOsdpConnection : OsdpConnection, IRetunableOsdpConnection
    {
        private readonly string _portName;
        private SerialPort _serialPort;

        /// <summary>
        /// Baud rates to try when searching for a device: 9600, 19200, 38400, 57600, 115200,
        /// 230400, 460800.
        /// </summary>
        /// <remarks>
        /// OSDP section 5.2 names only the first six; 460800 is a common extension rather than a
        /// rate the specification defines, and is included here because devices in the field use it.
        /// </remarks>
        public static readonly int[] StandardBaudRates = [9600, 19200, 38400, 57600, 115200, 230400, 460800];

        /// <summary>Initializes a new instance of the <see cref="T:OSDP.Net.Connections.SerialPortOsdpConnection" /> class.</summary>
        /// <param name="portName">Name of the port.</param>
        /// <param name="baudRate">The baud rate.</param>
        /// <exception cref="T:System.ArgumentNullException">portName</exception>
        public SerialPortOsdpConnection(string portName, int baudRate) : base(baudRate)
        {
            _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        }

        /// <summary>
        /// A helper method that returns a lazily instantiated set of SerialPortOsdpConnection
        /// instances, each one configured for a different baud rate. The primary use case for this
        /// method is in conjunction with <see cref="ControlPanel.DiscoverDevice(IEnumerable{IOsdpConnection}, PanelCommands.DeviceDiscover.DiscoveryOptions)"/>
        /// which expects a set of connections to test for a device.
        /// </summary>
        /// <param name="portName">Name of the port</param>
        /// <param name="rates">
        /// Optional parameter identifying a set of baud rates to enumerate over. If not specified,
        /// the list from OSDP spec (9600, 19,200, 38,400, 57,600, 115,200, 230,400, 460,800) will be used by default
        /// </param>
        /// <returns>An enumerable that will lazily generate SerialPortOsdpConnection instances for a 
        /// given set of baud rates (see description of "rates" parameter)</returns>
        public static IEnumerable<SerialPortOsdpConnection> EnumBaudRates(string portName, int[] rates = null)
        {
            return (rates ?? StandardBaudRates).Select(rate => new SerialPortOsdpConnection(portName, rate));
        }

        /// <summary>
        /// Gets or sets a value indicating whether the receive and transmit buffers are discarded
        /// before every write. Defaults to <c>true</c>.
        /// </summary>
        /// <remarks>
        /// Discarding suits an ACU, which drives a strict command/reply cycle and wants a clean
        /// slate before each command. It is harmful for a device that replies to an incoming
        /// stream, because the next command may already be arriving while the previous reply is
        /// being written, and discarding throws it away.
        /// </remarks>
        public bool DiscardBuffersBeforeWrite { get; set; } = true;

        /// <inheritdoc />
        public override Task Open()
        {
            if (_serialPort == null)
            {
                _serialPort = new(_portName, BaudRate);
                _serialPort.Open();
                IsOpen = true;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Changes the baud rate of the connection, retuning the port in place if it is open.
        /// </summary>
        /// <param name="baudRate">The new baud rate.</param>
        /// <exception cref="ArgumentOutOfRangeException">The baud rate is not positive.</exception>
        /// <remarks>
        /// Retuning in place avoids closing and reopening the port, which is slow, can leave the
        /// handle briefly unavailable on Windows, and toggles the control lines in a way that can
        /// disturb the bus. Callers changing rate mid-conversation should first let any pending
        /// transmission drain with <see cref="WaitForTransmitCompleteAsync"/>; changing the divisor
        /// while bytes are still in the shift register corrupts the tail of the message.
        /// </remarks>
        public void SetBaudRate(int baudRate)
        {
            if (baudRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baudRate), "Baud rate must be greater than zero.");
            }

            BaudRate = baudRate;

            var serialPort = _serialPort;
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.BaudRate = baudRate;
            }
        }

        /// <summary>
        /// Waits until buffered output has been handed to the hardware and had time to clear it.
        /// </summary>
        /// <param name="frameByteCount">Number of bytes most recently written, used to work out
        /// how long they occupy the line.</param>
        /// <param name="token">Token to observe while waiting.</param>
        /// <remarks>
        /// <para>
        /// Windows exposes no equivalent of <c>tcdrain</c>, so this does the best available
        /// approximation: wait for the driver's write buffer to empty, then wait out the
        /// transmission time of the frame to cover the UART FIFO and shift register that
        /// <see cref="SerialPort.BytesToWrite"/> cannot see.
        /// </para>
        /// <para>
        /// This matters before <see cref="SetBaudRate"/>: returning from a write only means the
        /// bytes were accepted for transmission, not that they have left the wire.
        /// </para>
        /// </remarks>
        public async Task WaitForTransmitCompleteAsync(int frameByteCount, CancellationToken token = default)
        {
            var serialPort = _serialPort;
            if (serialPort == null || !serialPort.IsOpen) return;

            while (!token.IsCancellationRequested && serialPort.BytesToWrite > 0)
            {
                await Task.Delay(1, token).ConfigureAwait(false);
            }

            if (frameByteCount <= 0) return;

            var wireTime = TimeSpan.FromSeconds(frameByteCount * 10.0 / BaudRate);
            await Task.Delay(wireTime, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override Task Close()
        {
            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;
            IsOpen = false;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override async Task WriteAsync(byte[] buffer)
        {
            if (DiscardBuffersBeforeWrite)
            {
                // Found an issue where many timeouts would fill up the receive buffer.
                // When writing to the port, there should be nothing in the buffers.
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();
            }

            await _serialPort.BaseStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
        {
            var task = _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, token);

            if (await Task.WhenAny(task, Task.Delay(-1, token)) == task)
            {
                return await task.ConfigureAwait(false);
            }

            throw new TimeoutException();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return _portName;
        }
    }
}
