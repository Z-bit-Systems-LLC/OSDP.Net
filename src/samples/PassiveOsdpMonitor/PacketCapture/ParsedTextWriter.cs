using OSDP.Net;
using OSDP.Net.Model;
using OSDP.Net.Tracing;

namespace PassiveOsdpMonitor.PacketCapture;

public class ParsedTextWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly MessageSpy _messageSpy;
    private readonly IPacketTextFormatter _formatter;
    private DateTime _lastPacketTime = DateTime.MinValue;
    private const byte ReplyAddressMask = 0x80;

    public ParsedTextWriter(string outputPath, byte[]? securityKey = null)
        : this(outputPath, securityKey, new OSDPPacketTextFormatter())
    {
    }

    public ParsedTextWriter(string outputPath, byte[]? securityKey, IPacketTextFormatter formatter)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _writer = new StreamWriter(outputPath, append: true);
        _messageSpy = new MessageSpy(securityKey);
        _formatter = formatter;
    }

    public void WritePacket(byte[] packetData, DateTime timestamp)
    {
        TimeSpan? delta = _lastPacketTime > DateTime.MinValue
            ? timestamp - _lastPacketTime
            : null;
        _lastPacketTime = timestamp;

        try
        {
            var packet = ParsePacket(packetData);
            _writer.Write(_formatter.FormatPacket(packet, timestamp, delta));
            _writer.Flush();
        }
        catch (SecureChannelRequired)
        {
            _writer.Write(_formatter.FormatEncryptedPacket(packetData, timestamp, delta));
            _writer.Flush();
        }
        catch (Exception ex)
        {
            _writer.Write(_formatter.FormatError(packetData, timestamp, delta, ex.Message));
            _writer.Flush();
        }
    }

    private Packet ParsePacket(byte[] packetData)
    {
        byte addressByte = packetData.Length > 1 ? packetData[1] : (byte)0;
        bool isReply = (addressByte & ReplyAddressMask) != 0;

        var message = isReply
            ? _messageSpy.ParseReply(packetData)
            : _messageSpy.ParseCommand(packetData);

        return new Packet(message);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
