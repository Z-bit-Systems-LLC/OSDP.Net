using System.Text.Json;

namespace PassiveOsdpMonitor.PacketCapture;

public class OsdpCapWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public OsdpCapWriter(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _writer = new StreamWriter(outputPath, append: true);
    }

    public void WritePacket(byte[] packet)
    {
        var unixTime = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1));
        long timeSec = (long)Math.Floor(unixTime.TotalSeconds);
        long timeNano = (unixTime.Ticks - timeSec * TimeSpan.TicksPerSecond) * 100L;

        var entry = new
        {
            timeSec = timeSec.ToString("F0"),
            timeNano = timeNano.ToString("D9").PadLeft(9, '0'),
            io = "trace",  // Always "trace" for passive monitoring
            data = BitConverter.ToString(packet),
            osdpTraceVersion = "1",
            osdpSource = "PassiveOsdpMonitor"
        };

        string json = JsonSerializer.Serialize(entry);
        _writer.WriteLine(json);
        _writer.Flush(); // Ensure data is written immediately
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }
}
