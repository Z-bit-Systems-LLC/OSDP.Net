namespace PassiveOsdpMonitor.PacketCapture;

public class PacketBuffer
{
    private readonly byte[] _buffer = new byte[4096];
    private int _position;
    private const byte SOM = 0x53;

    public bool TryExtractPacket(out byte[]? packet)
    {
        packet = null;

        // Find SOM
        int somIndex = Array.IndexOf(_buffer, SOM, 0, _position);
        if (somIndex == -1) return false;

        // Discard any bytes before SOM
        if (somIndex > 0)
        {
            int remaining = _position - somIndex;
            Array.Copy(_buffer, somIndex, _buffer, 0, remaining);
            _position = remaining;
        }

        // Need at least 6 bytes: SOM + Addr + Len(2) + Ctrl + Type
        if (_position < 6) return false;

        // Parse length field (LSB first)
        int length = _buffer[2] | (_buffer[3] << 8);

        // Validate length (sanity check)
        if (length < 6 || length > 1024)
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

    public void Append(byte[] data, int count)
    {
        if (_position + count > _buffer.Length)
        {
            // Buffer full, try to find and keep only data after last SOM
            int lastSom = Array.LastIndexOf(_buffer, SOM, _position - 1);
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
}
