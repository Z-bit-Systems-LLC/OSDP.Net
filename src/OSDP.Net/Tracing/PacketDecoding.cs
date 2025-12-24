#nullable enable

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json;

using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tracing;

/// <summary>
/// Methods t decode packets that are recorded from a tracer
/// </summary>
public static class PacketDecoding
{
    /// <summary>
    /// Parse a raw message
    /// </summary>
    /// <param name="data">The byte data of the raw message starting with the SOM byte</param>
    /// <param name="secureChannel">The secure channel for decryption</param>
    /// <returns>The parse data of a packet</returns>
    public static Packet ParseMessage(ReadOnlySpan<byte> data, IMessageSecureChannel secureChannel)
    {
        var message = new IncomingMessage(data, secureChannel);
        return new Packet(message);
    }

    /// <summary>
    /// Attempts to parse a raw message without throwing exceptions.
    /// </summary>
    /// <param name="data">The byte data of the raw message starting with the SOM byte</param>
    /// <param name="secureChannel">The secure channel for decryption</param>
    /// <param name="packet">The parsed packet if successful, null otherwise</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryParseMessage(ReadOnlySpan<byte> data, IMessageSecureChannel secureChannel, out Packet? packet)
    {
        try
        {
            packet = ParseMessage(data, secureChannel);
            return true;
        }
        catch
        {
            packet = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to parse a raw message without throwing exceptions, using a MessageSpy for secure channel handling.
    /// </summary>
    /// <param name="data">The byte data of the raw message starting with the SOM byte</param>
    /// <param name="packet">The parsed packet if successful, null otherwise</param>
    /// <param name="securityKey">Optional encryption key for decrypting secure channel packets</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryParseMessage(ReadOnlySpan<byte> data, out Packet? packet, byte[]? securityKey = null)
    {
        try
        {
            const byte replyAddress = 0x80;
            var messageSpy = new MessageSpy(securityKey);
            var rawData = data.ToArray();

            packet = messageSpy.PeekAddressByte(rawData) < replyAddress
                ? new Packet(messageSpy.ParseCommand(rawData))
                : new Packet(messageSpy.ParseReply(rawData));
            return true;
        }
        catch
        {
            packet = null;
            return false;
        }
    }

    /// <summary>
    /// Parses an OSDP capture file in JSON format.
    /// </summary>
    /// <param name="json">The JSON content of the capture file.</param>
    /// <param name="key">Optional encryption key for decrypting secure channel packets.</param>
    /// <returns>An enumerable of parsed capture entries.</returns>
    public static IEnumerable<OSDPCaptureEntry> OSDPCapParser(string json, byte[]? key = null)
    {
        const byte replyAddress = 0x80;
        var messageSpy = new MessageSpy(key);

        var lines = json.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            dynamic? entry = JsonSerializer.Deserialize<ExpandoObject>(line);
            if (entry == null) continue;

            DateTime dateTime = new DateTime(1970, 1, 1).AddSeconds(Double.Parse(entry.timeSec.ToString()))
                .AddTicks(long.Parse(entry.timeNano.ToString()) / 100L);
            Enum.TryParse(entry.io.ToString(), true, out TraceDirection io);
            string data = entry.data.ToString();

            var rawData = BinaryUtils.HexToBytes(data).ToArray();
            var packet = messageSpy.PeekAddressByte(rawData) < replyAddress
                ? new Packet(messageSpy.ParseCommand(rawData))
                : new Packet(messageSpy.ParseReply(rawData));

            yield return new OSDPCaptureEntry(
                dateTime,
                io,
                packet,
                entry.osdpTraceVersion.ToString(),
                entry.osdpSource.ToString());
        }
    }
}