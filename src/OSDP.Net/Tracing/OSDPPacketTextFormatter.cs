using System;
using System.Collections.Generic;
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
            string secureChannelDetails = FormatSecureChannelHandshake(packet);
            if (secureChannelDetails != null)
            {
                sb.AppendLine(secureChannelDetails);
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
        }

        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Produces a human-readable breakdown of the secure channel initialization handshake
    /// (osdp_CHLNG, osdp_CCRYPT, osdp_SCRYPT, osdp_RMAC_I), or null if the packet is not one
    /// of those handshake messages.
    /// </summary>
    private static string FormatSecureChannelHandshake(Packet packet)
    {
        switch (packet.CommandType)
        {
            case CommandType.SessionChallenge:
                return FormatSessionChallenge(packet);
            case CommandType.ServerCryptogram:
                return FormatServerCryptogram(packet);
        }

        switch (packet.ReplyType)
        {
            case ReplyType.CrypticData:
                return FormatCrypticData(packet);
            case ReplyType.InitialRMac:
                return FormatInitialRMac(packet);
        }

        return null;
    }

    private static string FormatSessionChallenge(Packet packet)
    {
        var payload = packet.RawPayloadData;
        var lines = new List<string>
        {
            "    Secure Channel Handshake: osdp_CHLNG (Step 1 of 4 - ACU challenge)",
            FormatSecurityBlockLine(packet, DescribeKeyType(packet))
        };
        lines.Add(payload.Length >= 8
            ? $"    RND.A (ACU random): {Hex(payload.Slice(0, 8))}"
            : $"    Payload: {Hex(payload)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatCrypticData(Packet packet)
    {
        var payload = packet.RawPayloadData;
        var lines = new List<string>
        {
            "    Secure Channel Handshake: osdp_CCRYPT (Step 2 of 4 - PD response)",
            FormatSecurityBlockLine(packet, DescribeKeyType(packet))
        };
        if (payload.Length >= 32)
        {
            lines.Add($"    cUID (PD unique ID): {Hex(payload.Slice(0, 8))}");
            lines.Add($"    RND.B (PD random): {Hex(payload.Slice(8, 8))}");
            lines.Add($"    Client cryptogram: {Hex(payload.Slice(16, 16))}");
        }
        else
        {
            lines.Add($"    Payload: {Hex(payload)}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatServerCryptogram(Packet packet)
    {
        return string.Join(Environment.NewLine,
            "    Secure Channel Handshake: osdp_SCRYPT (Step 3 of 4 - ACU server cryptogram)",
            FormatSecurityBlockLine(packet, DescribeKeyType(packet)),
            $"    Server cryptogram: {Hex(packet.RawPayloadData)}");
    }

    private static string FormatInitialRMac(Packet packet)
    {
        return string.Join(Environment.NewLine,
            "    Secure Channel Handshake: osdp_RMAC_I (Step 4 of 4 - secure channel established)",
            FormatSecurityBlockLine(packet, DescribeCryptogramAcceptance(packet)),
            $"    Initial R-MAC: {Hex(packet.RawPayloadData)}");
    }

    /// <summary>
    /// Renders the security control block of a handshake packet, e.g.
    /// "Security Block: SCS_11, SEC_BLK_DATA: 00 (Default key (SCBK-D))".
    /// </summary>
    private static string FormatSecurityBlockLine(Packet packet, string interpretation)
    {
        string scsType = DescribeSecurityBlockType(packet.IncomingMessage.SecurityBlockType);
        string data = FormatSecureBlockData(packet.IncomingMessage.SecureBlockData);
        string suffix = string.IsNullOrEmpty(interpretation) ? string.Empty : $" ({interpretation})";
        return $"    Security Block: {scsType}, SEC_BLK_DATA: {data}{suffix}";
    }

    private static string DescribeSecurityBlockType(byte type)
    {
        return type switch
        {
            0x11 => "SCS_11",
            0x12 => "SCS_12",
            0x13 => "SCS_13",
            0x14 => "SCS_14",
            0x15 => "SCS_15",
            0x16 => "SCS_16",
            0x17 => "SCS_17",
            0x18 => "SCS_18",
            _ => $"0x{type:X2}"
        };
    }

    /// <summary>
    /// In osdp_CHLNG/osdp_CCRYPT/osdp_SCRYPT the security block data byte indicates the key in use:
    /// 0x00 = the well-known default key (SCBK-D), anything else = the per-installation key (SCBK).
    /// </summary>
    private static string DescribeKeyType(Packet packet)
    {
        var secureBlockData = packet.IncomingMessage.SecureBlockData;
        if (secureBlockData == null || secureBlockData.Length == 0)
        {
            return null;
        }

        return secureBlockData[0] == 0x00 ? "Default key (SCBK-D)" : "Configured key (SCBK)";
    }

    /// <summary>
    /// In osdp_RMAC_I the security block data byte indicates whether the PD accepted the ACU's
    /// server cryptogram (and thus established the secure channel). Per the OSDP spec (section
    /// D.1.3.4) the value is 0x01 on success.
    /// </summary>
    private static string DescribeCryptogramAcceptance(Packet packet)
    {
        var secureBlockData = packet.IncomingMessage.SecureBlockData;
        if (secureBlockData == null || secureBlockData.Length == 0)
        {
            return null;
        }

        return secureBlockData[0] == 0x01
            ? "Server cryptogram accepted"
            : "Server cryptogram rejected";
    }

    private static string FormatSecureBlockData(byte[] data) =>
        data == null || data.Length == 0 ? "(none)" : BitConverter.ToString(data);

    private static string Hex(ReadOnlySpan<byte> data) => BitConverter.ToString(data.ToArray());

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
