#nullable enable

using System;
using OSDP.Net.Messages;

namespace OSDP.Net.Tracing;

/// <summary>
/// Buffers incoming OSDP data from a stream and extracts complete packets.
/// Use this for passive monitoring or any scenario where serial/TCP data
/// arrives in fragments that need to be reassembled into complete OSDP packets.
/// </summary>
public class PacketBuffer
{
    private const int DefaultBufferSize = 4096;
    private const int MinPacketLength = 6;
    private const int MaxPacketLength = 1024;

    private readonly byte[] _buffer;
    private int _position;

    /// <summary>
    /// Creates a new PacketBuffer with the default buffer size (4096 bytes).
    /// </summary>
    public PacketBuffer() : this(DefaultBufferSize)
    {
    }

    /// <summary>
    /// Creates a new PacketBuffer with a specified buffer size.
    /// </summary>
    /// <param name="bufferSize">The size of the internal buffer in bytes.</param>
    public PacketBuffer(int bufferSize)
    {
        _buffer = new byte[bufferSize];
    }

    /// <summary>
    /// Attempts to extract a complete OSDP packet from the buffer.
    /// </summary>
    /// <param name="packet">When successful, contains the extracted packet bytes.</param>
    /// <returns>True if a complete packet was extracted, false otherwise.</returns>
    public bool TryExtractPacket(out byte[]? packet)
    {
        packet = null;

        // Find SOM (Start of Message)
        int somIndex = Array.IndexOf(_buffer, Message.StartOfMessage, 0, _position);
        if (somIndex == -1) return false;

        // Discard any bytes before SOM
        if (somIndex > 0)
        {
            int remaining = _position - somIndex;
            Array.Copy(_buffer, somIndex, _buffer, 0, remaining);
            _position = remaining;
        }

        // Need at least minimum header: SOM + Addr + Len(2) + Ctrl + Type
        if (_position < MinPacketLength) return false;

        // Parse length field (LSB first)
        int length = _buffer[2] | (_buffer[3] << 8);

        // Validate length (sanity check)
        if (length < MinPacketLength || length > MaxPacketLength)
        {
            // Invalid length, skip this SOM and look for next
            Array.Copy(_buffer, 1, _buffer, 0, _position - 1);
            _position--;
            return false;
        }

        // Check if we have complete packet
        if (_position < length) return false;

        // Extract packet
        packet = new byte[length];
        Array.Copy(_buffer, 0, packet, 0, length);

        // Remove from buffer
        int remainingAfter = _position - length;
        if (remainingAfter > 0)
            Array.Copy(_buffer, length, _buffer, 0, remainingAfter);
        _position = remainingAfter;

        return true;
    }

    /// <summary>
    /// Appends data to the buffer.
    /// </summary>
    /// <param name="data">The data to append.</param>
    /// <param name="count">The number of bytes to append from the data array.</param>
    public void Append(byte[] data, int count)
    {
        if (_position + count > _buffer.Length)
        {
            // Buffer full, try to find and keep only data after last SOM
            int lastSom = Array.LastIndexOf(_buffer, Message.StartOfMessage, _position - 1);
            if (lastSom > 0)
            {
                Array.Copy(_buffer, lastSom, _buffer, 0, _position - lastSom);
                _position -= lastSom;
            }
            else
            {
                _position = 0; // Discard all if no SOM found
            }
        }

        Array.Copy(data, 0, _buffer, _position, count);
        _position += count;
    }

    /// <summary>
    /// Clears all data from the buffer.
    /// </summary>
    public void Clear()
    {
        _position = 0;
    }

    /// <summary>
    /// Gets the number of bytes currently in the buffer.
    /// </summary>
    public int Length => _position;
}
