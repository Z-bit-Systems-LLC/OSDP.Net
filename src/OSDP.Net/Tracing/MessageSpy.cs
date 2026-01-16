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
/// Provides functionality to parse and decode OSDP messages from raw byte data,
/// tracking secure channel state for encrypted communications.
/// </summary>
public class MessageSpy
{
    private readonly SecurityContext _context;
    private readonly MessageSecureChannel _commandSpyChannel;
    private readonly MessageSecureChannel _replySpyChannel;

    /// <summary>
    /// Initializes a new instance of the MessageSpy class with an optional security key.
    /// </summary>
    /// <param name="securityKey">Optional encryption key for decrypting secure channel packets. If null, only non-encrypted packets can be fully parsed.</param>
    public MessageSpy(byte[]? securityKey = null)
    {
        _context = new SecurityContext(securityKey);
        _commandSpyChannel = new PdMessageSecureChannelBase(_context);
        _replySpyChannel = new ACUMessageSecureChannel(_context);
    }

    /// <summary>
    /// Retrieves the address byte from raw OSDP data without parsing the entire message.
    /// </summary>
    /// <param name="data">Raw OSDP message data starting with SOM byte.</param>
    /// <returns>The address byte from the message.</returns>
    public byte PeekAddressByte(ReadOnlySpan<byte> data)
    {
        return data[1];
    }

    /// <summary>
    /// Parses raw OSDP command data into an IncomingMessage, handling the secure channel establishment.
    /// </summary>
    /// <param name="data">Raw OSDP command data starting with SOM byte.</param>
    /// <returns>Parsed IncomingMessage containing the command details.</returns>
    public IncomingMessage ParseCommand(byte[] data)
    {
        var command = new IncomingMessage(data, _commandSpyChannel);

        return (CommandType)command.Type switch
        {
            CommandType.SessionChallenge => HandleSessionChallenge(command),
            CommandType.ServerCryptogram => HandleSCrypt(command),
            _ => command
        };
    }

    /// <summary>
    /// Parses raw OSDP reply data into an IncomingMessage, handling the secure channel establishment.
    /// </summary>
    /// <param name="data">Raw OSDP reply data starting with SOM byte.</param>
    /// <returns>Parsed IncomingMessage containing the reply details.</returns>
    public IncomingMessage ParseReply(byte[] data)
    {
        var reply = new IncomingMessage(data, _replySpyChannel);

        return (ReplyType)reply.Type switch
        {
            ReplyType.InitialRMac => HandleInitialRMac(reply),
            _ => reply
        };
    }

    private IncomingMessage HandleSessionChallenge(IncomingMessage command)
    {
        byte[] rndA = command.Payload;
        var crypto = _context.CreateCypher(true);
        _context.Enc = SecurityContext.GenerateKey(crypto,
            [0x01, 0x82, rndA[0], rndA[1], rndA[2], rndA[3], rndA[4], rndA[5]]);
        _context.SMac1 = SecurityContext.GenerateKey(crypto,
            [0x01, 0x01, rndA[0], rndA[1], rndA[2], rndA[3], rndA[4], rndA[5]]);
        _context.SMac2 = SecurityContext.GenerateKey(crypto,
            [0x01, 0x02, rndA[0], rndA[1], rndA[2], rndA[3], rndA[4], rndA[5]]);
        return command;
    }

    private IncomingMessage HandleSCrypt(IncomingMessage command)
    {
        var serverCryptogram = command.Payload;
        using var crypto = _context.CreateCypher(true, _context.SMac1);
        var intermediate = SecurityContext.GenerateKey(crypto, serverCryptogram);
        crypto.Key = _context.SMac2;
        _context.RMac = SecurityContext.GenerateKey(crypto, intermediate);
        return command;
    }

    private IncomingMessage HandleInitialRMac(IncomingMessage reply)
    {
        _context.IsSecurityEstablished = true;
        return reply;
    }

    /// <summary>
    /// Parses raw OSDP data into a Packet, automatically detecting command vs. reply.
    /// </summary>
    /// <param name="data">Raw OSDP message data starting with SOM byte.</param>
    /// <returns>Parsed Packet containing the message details.</returns>
    public Packet ParsePacket(byte[] data)
    {
        const byte replyAddress = 0x80;
        var message = PeekAddressByte(data) < replyAddress
            ? ParseCommand(data)
            : ParseReply(data);
        return new Packet(message);
    }

    /// <summary>
    /// Attempts to parse raw OSDP data into a Packet without throwing exceptions.
    /// </summary>
    /// <param name="data">Raw OSDP message data starting with SOM byte.</param>
    /// <param name="packet">When successful, contains the parsed Packet.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public bool TryParsePacket(byte[] data, out Packet? packet)
    {
        try
        {
            packet = ParsePacket(data);
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
    /// <returns>An enumerable of parsed capture entries.</returns>
    public IEnumerable<OSDPCaptureEntry> ParseCaptureFile(string json)
    {
        var lines = json.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            dynamic? entry = JsonSerializer.Deserialize<ExpandoObject>(line);
            if (entry == null) continue;

            DateTime dateTime = new DateTime(1970, 1, 1).AddSeconds(double.Parse(entry.timeSec.ToString()))
                .AddTicks(long.Parse(entry.timeNano.ToString()) / 100L);
            Enum.TryParse(entry.io.ToString(), true, out TraceDirection io);
            string data = entry.data.ToString();

            var rawData = BinaryUtils.HexToBytes(data).ToArray();
            var packet = ParsePacket(rawData);

            yield return new OSDPCaptureEntry(
                dateTime,
                io,
                packet,
                entry.osdpTraceVersion.ToString(),
                entry.osdpSource.ToString());
        }
    }
}