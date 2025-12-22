using System;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Model;

namespace OSDP.Net.Tracing;

/// <summary>
/// Default implementation of <see cref="IPacketTextFormatter"/> for formatting OSDP packets as human-readable text.
/// </summary>
public class OSDPPacketTextFormatter : IPacketTextFormatter
{
    private const byte ReplyAddressMask = 0x80;
    private const int MsgTypeIndex = 5;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="packet"/> is null.</exception>
    public string FormatPacket(Packet packet, DateTime timestamp, TimeSpan? timeDelta = null)
    {
        if (packet == null)
            throw new ArgumentNullException(nameof(packet));

        var sb = new StringBuilder();

        string deltaString = FormatTimeDelta(timeDelta);
        string direction = packet.CommandType != null ? "ACU -> PD" : "PD -> ACU";
        string type = (packet.CommandType?.ToString() ?? packet.ReplyType?.ToString()) ?? "Unknown";

        sb.AppendLine($"{timestamp:yy-MM-dd HH:mm:ss.fff}{deltaString} {direction}: {type}");
        sb.AppendLine($"    Address: {packet.Address} Sequence: {packet.Sequence}");

        var payloadData = packet.ParsePayloadData();
        if (payloadData != null)
        {
            string payloadString = payloadData switch
            {
                byte[] data => $"    {BitConverter.ToString(data)}",
                string data => $"    {data}",
                _ => $"    {payloadData}"
            };
            sb.AppendLine(payloadString);
        }

        sb.AppendLine();
        return sb.ToString();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawData"/> is null.</exception>
    public string FormatEncryptedPacket(byte[] rawData, DateTime timestamp, TimeSpan? timeDelta = null)
    {
        if (rawData == null)
            throw new ArgumentNullException(nameof(rawData));

        var sb = new StringBuilder();
        string deltaString = FormatTimeDelta(timeDelta);

        if (rawData.Length >= 6)
        {
            byte addressByte = rawData[1];
            byte address = (byte)(addressByte & 0x7F);
            bool isReply = (addressByte & ReplyAddressMask) != 0;
            byte sequence = (byte)(rawData[4] & 0x03);
            bool isSecureBlockPresent = (rawData[4] & 0x08) != 0;
            byte secureBlockSize = (byte)(isSecureBlockPresent ? rawData[5] : 0);

            int targetIndex = MsgTypeIndex + secureBlockSize;
            if (targetIndex < rawData.Length)
            {
                byte commandOrReplyType = rawData[targetIndex];

                string direction = isReply ? "PD -> ACU" : "ACU -> PD";
                string type = isReply
                    ? (Enum.IsDefined(typeof(ReplyType), commandOrReplyType)
                        ? ((ReplyType)commandOrReplyType).ToString()
                        : $"Unknown Reply ({commandOrReplyType})")
                    : (Enum.IsDefined(typeof(CommandType), commandOrReplyType)
                        ? ((CommandType)commandOrReplyType).ToString()
                        : $"Unknown Command ({commandOrReplyType})");

                sb.AppendLine($"{timestamp:yy-MM-dd HH:mm:ss.fff}{deltaString} {direction}: {type}");
                sb.AppendLine($"    Address: {address} Sequence: {sequence}");
            }
            else
            {
                string direction = isReply ? "PD -> ACU" : "ACU -> PD";
                sb.AppendLine($"{timestamp:yy-MM-dd HH:mm:ss.fff}{deltaString} {direction}: Unknown");
                sb.AppendLine($"    Address: {address} Sequence: {sequence}");
                sb.AppendLine("    *** Malformed packet: message type index out of bounds ***");
            }

            sb.AppendLine("    *** Payload is encrypted - SCBK (Secure Channel Base Key) required to decrypt ***");
            sb.AppendLine("    To decrypt this data, provide the SCBK when initializing the parser.");
        }
        else
        {
            sb.AppendLine($"{timestamp:yy-MM-dd HH:mm:ss.fff}{deltaString}");
            sb.AppendLine("    *** Unable to parse encrypted packet: SCBK (Secure Channel Base Key) required ***");
        }

        sb.AppendLine($"    Raw data: {BitConverter.ToString(rawData)}");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawData"/> is null.</exception>
    public string FormatError(byte[] rawData, DateTime timestamp, TimeSpan? timeDelta, string errorMessage)
    {
        if (rawData == null)
            throw new ArgumentNullException(nameof(rawData));

        var sb = new StringBuilder();
        string deltaString = FormatTimeDelta(timeDelta);

        sb.AppendLine($"{timestamp:yy-MM-dd HH:mm:ss.fff}{deltaString}");
        sb.AppendLine($"*** Error parsing packet: {errorMessage} ***");
        sb.AppendLine($"    Raw data: {BitConverter.ToString(rawData)}");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string FormatTimeDelta(TimeSpan? timeDelta)
    {
        return timeDelta.HasValue ? $" [ {timeDelta.Value:g} ]" : string.Empty;
    }
}
