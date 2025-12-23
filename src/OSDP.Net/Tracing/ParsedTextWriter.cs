using System;
using System.IO;
using OSDP.Net.Model;

namespace OSDP.Net.Tracing;

/// <summary>
/// Writes parsed OSDP packets to a human-readable text file.
/// </summary>
public class ParsedTextWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly MessageSpy _messageSpy;
    private readonly IPacketTextFormatter _formatter;
    private DateTime _lastPacketTime = DateTime.MinValue;
    private const byte ReplyAddressMask = 0x80;

    /// <summary>
    /// Creates a new ParsedTextWriter with the default formatter.
    /// </summary>
    /// <param name="outputPath">Path to the output text file.</param>
    /// <param name="securityKey">Optional security key for decrypting secure channel messages.</param>
    public ParsedTextWriter(string outputPath, byte[] securityKey = null)
        : this(outputPath, securityKey, new OSDPPacketTextFormatter())
    {
    }

    /// <summary>
    /// Creates a new ParsedTextWriter with a custom formatter.
    /// </summary>
    /// <param name="outputPath">Path to the output text file.</param>
    /// <param name="securityKey">Optional security key for decrypting secure channel messages.</param>
    /// <param name="formatter">The formatter to use for packet output.</param>
    public ParsedTextWriter(string outputPath, byte[] securityKey, IPacketTextFormatter formatter)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _writer = new StreamWriter(outputPath, append: true);
        _messageSpy = new MessageSpy(securityKey);
        _formatter = formatter;
    }

    /// <summary>
    /// Writes a packet to the output file.
    /// </summary>
    /// <param name="packetData">The raw packet bytes.</param>
    /// <param name="timestamp">The timestamp when the packet was captured.</param>
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

    /// <inheritdoc />
    public void Dispose()
    {
        _writer.Dispose();
    }
}
