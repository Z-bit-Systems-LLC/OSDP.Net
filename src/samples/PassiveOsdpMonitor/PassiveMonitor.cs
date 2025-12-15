using System.IO.Ports;
using Microsoft.Extensions.Logging;
using PassiveOsdpMonitor.Configuration;
using PassiveOsdpMonitor.PacketCapture;

namespace PassiveOsdpMonitor;

public class PassiveMonitor
{
    private readonly SerialPort _serialPort;
    private readonly PacketBuffer _buffer;
    private readonly OsdpCapWriter _osdpCapWriter;
    private readonly ParsedTextWriter _parsedTextWriter;
    private readonly ILogger _logger;

    public PassiveMonitor(MonitorConfiguration config, ILogger logger)
    {
        _serialPort = new SerialPort
        {
            PortName = config.SerialPort,
            BaudRate = config.BaudRate,
            DataBits = 8,                    // Standard for OSDP
            Parity = Parity.None,            // Standard for OSDP
            StopBits = StopBits.One,         // Standard for OSDP
            ReadTimeout = 1000,
            Handshake = Handshake.None       // No flow control
        };

        _buffer = new PacketBuffer();
        _osdpCapWriter = new OsdpCapWriter(config.OsdpCapFilePath);
        _parsedTextWriter = new ParsedTextWriter(config.ParsedTextFilePath, config.SecurityKey);
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _serialPort.Open();
        _logger.LogInformation("Serial port {Port} opened at {BaudRate} baud",
            _serialPort.PortName, _serialPort.BaudRate);

        byte[] readBuffer = new byte[1024];
        int packetCount = 0;
        long totalBytes = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Read available bytes
                    int bytesRead = await _serialPort.BaseStream.ReadAsync(
                        readBuffer, 0, readBuffer.Length, cancellationToken);

                    if (bytesRead > 0)
                    {
                        totalBytes += bytesRead;
                        _buffer.Append(readBuffer, bytesRead);

                        // Extract all complete packets from buffer
                        while (_buffer.TryExtractPacket(out byte[]? packet) && packet != null)
                        {
                            DateTime timestamp = DateTime.Now;

                            // Write to both outputs
                            _osdpCapWriter.WritePacket(packet);
                            _parsedTextWriter.WritePacket(packet, timestamp);

                            packetCount++;

                            if (_logger.IsEnabled(LogLevel.Debug))
                            {
                                _logger.LogDebug("Packet #{Count}: {Length} bytes - {Data}",
                                    packetCount, packet.Length,
                                    BitConverter.ToString(packet, 0, Math.Min(16, packet.Length)));
                            }
                            else if (packetCount % 100 == 0)
                            {
                                _logger.LogInformation("Packets captured: {Count}", packetCount);
                            }
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // Normal - no data available, continue
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading from serial port");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("Monitor stopped.");
            _logger.LogInformation("Total packets captured: {Count}", packetCount);
            _logger.LogInformation("Total bytes read: {Bytes:N0}", totalBytes);

            _serialPort.Close();
            _osdpCapWriter.Dispose();
            _parsedTextWriter.Dispose();
        }
    }
}
