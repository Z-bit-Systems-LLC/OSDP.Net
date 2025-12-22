using Microsoft.Extensions.Logging;
using OSDP.Net.Connections;
using PassiveOsdpMonitor.Configuration;
using PassiveOsdpMonitor.PacketCapture;

namespace PassiveOsdpMonitor;

public class PassiveMonitor
{
    private readonly IOsdpConnection _connection;
    private readonly PacketBuffer _buffer;
    private readonly OsdpCapWriter _osdpCapWriter;
    private readonly ParsedTextWriter _parsedTextWriter;
    private readonly ILogger _logger;

    public PassiveMonitor(MonitorConfiguration config, ILogger logger)
    {
        _connection = new ReadOnlySerialPortOsdpConnection(config.SerialPort, config.BaudRate);
        _buffer = new PacketBuffer();
        _osdpCapWriter = new OsdpCapWriter(config.OsdpCapFilePath);
        _parsedTextWriter = new ParsedTextWriter(config.ParsedTextFilePath, config.SecurityKey);
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _connection.Open();
        _logger.LogInformation("Connection {Connection} opened at {BaudRate} baud",
            _connection, _connection.BaudRate);

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
                    int bytesRead = await _connection.ReadAsync(readBuffer, cancellationToken);

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
                    _logger.LogError(ex, "Error reading from connection");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("Monitor stopped.");
            _logger.LogInformation("Total packets captured: {Count}", packetCount);
            _logger.LogInformation("Total bytes read: {Bytes:N0}", totalBytes);

            await _connection.Close();
            _connection.Dispose();
            _osdpCapWriter.Dispose();
            _parsedTextWriter.Dispose();
        }
    }
}
