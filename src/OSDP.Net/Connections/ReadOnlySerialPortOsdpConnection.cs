using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace OSDP.Net.Connections
{
    /// <summary>
    /// Read-only serial port connection for passive monitoring of OSDP traffic.
    /// This connection only supports reading data and will throw NotSupportedException for write operations.
    /// </summary>
    public class ReadOnlySerialPortOsdpConnection : OsdpConnection
    {
        private readonly string _portName;
        private SerialPort _serialPort;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlySerialPortOsdpConnection"/> class.
        /// </summary>
        /// <param name="portName">Name of the serial port (e.g., "COM3").</param>
        /// <param name="baudRate">The baud rate for the serial connection.</param>
        /// <exception cref="ArgumentNullException">Thrown when portName is null.</exception>
        public ReadOnlySerialPortOsdpConnection(string portName, int baudRate) : base(baudRate)
        {
            _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        }

        /// <inheritdoc />
        public override Task Open()
        {
            if (_serialPort == null)
            {
                _serialPort = new SerialPort(_portName, BaudRate)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 1000,
                    Handshake = Handshake.None
                };

                _serialPort.Open();
                IsOpen = true;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task Close()
        {
            if (_serialPort != null)
            {
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }

            IsOpen = false;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">
        /// This connection is read-only and does not support write operations.
        /// </exception>
        public override Task WriteAsync(byte[] buffer)
        {
            throw new NotSupportedException(
                "This is a read-only connection for passive monitoring. Write operations are not supported.");
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, CancellationToken token)
        {
            if (_serialPort == null || !IsOpen)
            {
                throw new InvalidOperationException("Connection is not open.");
            }

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
            return $"{_portName} (Read-Only)";
        }
    }
}
