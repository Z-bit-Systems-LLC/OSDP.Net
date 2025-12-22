using Microsoft.Extensions.Logging;
using OSDP.Net.Connections;
using OSDP.Net.Tracing;
using PassiveOsdpMonitor.Configuration;
using PassiveOsdpMonitor.PacketCapture;

namespace PassiveOsdpMonitor;

public class PassiveMonitor(MonitorConfiguration config, ILogger logger)
{
    private readonly IOsdpConnection _connection = new ReadOnlySerialPortOsdpConnection(config.SerialPort, config.BaudRate);
    private readonly PacketBuffer _buffer = new();
    private readonly OSDPCaptureFileWriter _osdpCaptureWriter = new(config.OsdpCapFilePath, "PassiveOsdpMonitor", append: true);
    private readonly ParsedTextWriter _parsedTextWriter = new(config.ParsedTextFilePath, config.SecurityKey);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _connection.Open();
        logger.LogInformation("Connection {Connection} opened at {BaudRate} baud",
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
                            _osdpCaptureWriter.WritePacket(packet, TraceDirection.Trace);
                            _parsedTextWriter.WritePacket(packet, timestamp);

                            packetCount++;

                            if (logger.IsEnabled(LogLevel.Debug))
                            {
                                logger.LogDebug("Packet #{Count}: {Length} bytes - {Data}",
                                    packetCount, packet.Length,
                                    BitConverter.ToString(packet, 0, Math.Min(16, packet.Length)));
                            }
                            else if (packetCount % 100 == 0)
                            {
                                logger.LogInformation("Packets captured: {Count}", packetCount);
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
                    logger.LogError(ex, "Error reading from connection");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        finally
        {
            logger.LogInformation("Monitor stopped");
            logger.LogInformation("Total packets captured: {Count}", packetCount);
            logger.LogInformation("Total bytes read: {Bytes:N0}", totalBytes);

            await _connection.Close();
            _connection.Dispose();
            _osdpCaptureWriter.Dispose();
            _parsedTextWriter.Dispose();
        }
    }
}
