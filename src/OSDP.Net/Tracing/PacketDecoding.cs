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
    /// <param name="secureChannel"></param>
    /// <returns>The parse data of a packet</returns>
    public static Packet ParseMessage(ReadOnlySpan<byte> data, IMessageSecureChannel secureChannel)
    {
        var message = new IncomingMessage(data, secureChannel);
        return new Packet(message);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="json"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static IEnumerable<OSDPCaptureEntry> OSDPCapParser(string json, byte[] key = null)
    {
        const byte replyAddress = 0x80;
        var messageSpy = new MessageSpy(key);

        var lines = json.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            dynamic entry = JsonSerializer.Deserialize<ExpandoObject>(line);

            DateTime dateTime = new DateTime(1970, 1, 1).AddSeconds(Double.Parse(entry.timeSec.ToString()))
                .AddTicks(long.Parse(entry.timeNano.ToString()) / 100L);
            Enum.TryParse(entry.io.ToString(), true, out TraceDirection io);
            string data = entry.data.ToString();

            Packet packet;
            try
            {
                var rawData = BinaryUtils.HexToBytes(data).ToArray();
                packet = messageSpy.PeekAddressByte(rawData) < replyAddress
                    ? new Packet(messageSpy.ParseCommand(rawData))
                    : new Packet(messageSpy.ParseReply(rawData));
            }
            catch (SecureChannelRequired)
            {
                throw new SecureChannelRequired(
                    "Unable to parse encrypted OSDP packet. The packet data is encrypted and requires the SCBK (Secure Channel Base Key) to decrypt. " +
                    "Provide the security key when calling OSDPCapParser or ensure the SCBK has been properly negotiated during the secure channel establishment.");
            }

            yield return new OSDPCaptureEntry(
                dateTime,
                io,
                packet,
                entry.osdpTraceVersion.ToString(),
                entry.osdpSource.ToString());
        }
    }
}