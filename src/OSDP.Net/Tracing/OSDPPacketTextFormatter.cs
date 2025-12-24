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
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="packet"/> is null.</exception>
    public string FormatPacket(Packet packet, DateTime timestamp, TimeSpan? timeDelta = null)
    {
        if (packet == null)
            throw new ArgumentNullException(nameof(packet));

        var sb = new StringBuilder();

        string deltaString = FormatTimeDelta(timeDelta);
        string direction = packet.CommandType != null ? "ACU -> PD" : "PD -> ACU";
        string type = (packet.CommandType?.GetDisplayName() ?? packet.ReplyType?.GetDisplayName()) ?? "Unknown";

        sb.AppendLine($"{timestamp:yy-MM-dd HH:mm:ss.fff}{deltaString} {direction}: {type}");
        sb.Append($"    Address: {packet.Address} Sequence: {packet.Sequence}");
        if (packet.IsSecureMessage)
        {
            sb.Append(packet.IsUsingDefaultKey ? " [Secure - Default Key]" : " [Secure]");
        }
        else
        {
            sb.Append(" [Clear Text]");
        }
        sb.AppendLine();

        if (!packet.IsPayloadDecrypted)
        {
            sb.AppendLine("    *** Payload is encrypted - SCBK (Secure Channel Base Key) required to decrypt ***");
        }
        else
        {
            var payloadData = packet.ParsePayloadData();
            if (payloadData != null)
            {
                string payloadString = payloadData switch
                {
                    byte[] data => $"    {BitConverter.ToString(data)}",
                    _ => $"    {payloadData}"
                };
                sb.AppendLine(payloadString);
            }
        }

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
