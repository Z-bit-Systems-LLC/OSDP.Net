using OSDP.Net;
using OSDP.Net.Messages;
using OSDP.Net.Model;
using OSDP.Net.Tracing;

namespace PassiveOsdpMonitor.PacketCapture;

public class ParsedTextWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly MessageSpy _messageSpy;
    private DateTime _lastPacketTime = DateTime.MinValue;
    private const byte ReplyAddressMask = 0x80;

    public ParsedTextWriter(string outputPath, byte[]? securityKey = null)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _writer = new StreamWriter(outputPath, append: true);
        _messageSpy = new MessageSpy(securityKey);
    }

    public void WritePacket(byte[] packetData, DateTime timestamp)
    {
        // Calculate time delta
        TimeSpan delta = _lastPacketTime > DateTime.MinValue
            ? timestamp - _lastPacketTime
            : TimeSpan.Zero;
        _lastPacketTime = timestamp;

        try
        {
            // Parse packet using MessageSpy
            byte addressByte = packetData.Length > 1 ? packetData[1] : (byte)0;
            bool isReply = (addressByte & ReplyAddressMask) != 0;

            IncomingMessage message = isReply
                ? _messageSpy.ParseReply(packetData)
                : _messageSpy.ParseCommand(packetData);

            var packet = new Packet(message);

            // Determine direction and type
            string direction = "Unknown";
            string type = "Unknown";

            if (packet.CommandType != null)
            {
                direction = "ACU -> PD";
                type = packet.CommandType.Value.ToString();
            }
            else if (packet.ReplyType != null)
            {
                direction = "PD -> ACU";
                type = packet.ReplyType.Value.ToString();
            }

            // Format output (matching ACUConsole format)
            _writer.WriteLine($"{timestamp:yy-MM-dd HH:mm:ss.fff} [ {delta:g} ] {direction}: {type}");
            _writer.WriteLine($"    Address: {packet.Address} Sequence: {packet.Sequence}");

            // Parse and write payload data
            var payloadData = packet.ParsePayloadData();
            if (payloadData != null)
            {
                string payloadString = payloadData switch
                {
                    byte[] data => $"    {BitConverter.ToString(data)}",
                    string data => $"    {data}",
                    _ => $"    {payloadData}"
                };
                _writer.WriteLine(payloadString);
            }

            _writer.WriteLine(); // Blank line between packets
            _writer.Flush();
        }
        catch (SecureChannelRequired)
        {
            // Even though payload is encrypted, we can still parse the header
            if (packetData.Length >= 6)
            {
                byte addressByte = packetData[1];
                byte address = (byte)(addressByte & 0x7F);
                bool isReply = (addressByte & ReplyAddressMask) != 0;
                byte sequence = (byte)(packetData[4] & 0x03);
                bool isSecureBlockPresent = (packetData[4] & 0x08) != 0;
                byte secureBlockSize = (byte)(isSecureBlockPresent ? packetData[5] : 0);

                // Command/Reply type is at position 5 (MsgTypeIndex) + secureBlockSize
                const int msgTypeIndex = 5;
                byte commandOrReplyType = packetData[msgTypeIndex + secureBlockSize];

                string direction = isReply ? "PD -> ACU" : "ACU -> PD";
                string type;
                if (isReply)
                {
                    type = Enum.IsDefined(typeof(ReplyType), commandOrReplyType)
                        ? ((ReplyType)commandOrReplyType).ToString()
                        : $"Unknown Reply ({commandOrReplyType})";
                }
                else
                {
                    type = Enum.IsDefined(typeof(CommandType), commandOrReplyType)
                        ? ((CommandType)commandOrReplyType).ToString()
                        : $"Unknown Command ({commandOrReplyType})";
                }

                _writer.WriteLine($"{timestamp:yy-MM-dd HH:mm:ss.fff} [ {delta:g} ] {direction}: {type}");
                _writer.WriteLine($"    Address: {address} Sequence: {sequence}");
                _writer.WriteLine($"    *** Payload is encrypted - SCBK (Secure Channel Base Key) required to decrypt ***");
                _writer.WriteLine($"    To decrypt this data, provide the SCBK when initializing the parser.");
            }
            else
            {
                _writer.WriteLine($"{timestamp:yy-MM-dd HH:mm:ss.fff} [ {delta:g} ]");
                _writer.WriteLine($"    *** Unable to parse encrypted packet: SCBK (Secure Channel Base Key) required ***");
            }

            _writer.WriteLine($"    Raw data: {BitConverter.ToString(packetData)}");
            _writer.WriteLine();
            _writer.Flush();
        }
        catch (Exception ex)
        {
            _writer.WriteLine($"{timestamp:yy-MM-dd HH:mm:ss.fff} [ {delta:g} ]");
            _writer.WriteLine($"*** Error parsing packet: {ex.Message} ***");
            _writer.WriteLine($"    Raw data: {BitConverter.ToString(packetData)}");
            _writer.WriteLine();
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
