using System;
using OSDP.Net.Model;

namespace OSDP.Net.Tracing;

/// <summary>
/// Interface for formatting OSDP packets as human-readable text.
/// </summary>
public interface IPacketTextFormatter
{
    /// <summary>
    /// Formats a successfully parsed packet as text.
    /// </summary>
    /// <param name="packet">The parsed OSDP packet.</param>
    /// <param name="timestamp">The timestamp when the packet was captured.</param>
    /// <param name="timeDelta">Optional time difference from the previous packet.</param>
    /// <returns>Formatted text representation of the packet.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="packet"/> is null.</exception>
    string FormatPacket(Packet packet, DateTime timestamp, TimeSpan? timeDelta = null);

    /// <summary>
    /// Formats an encrypted packet that could not be fully parsed due to missing security key.
    /// Extracts and displays header information that is available without decryption.
    /// </summary>
    /// <param name="rawData">The raw packet bytes.</param>
    /// <param name="timestamp">The timestamp when the packet was captured.</param>
    /// <param name="timeDelta">Optional time difference from the previous packet.</param>
    /// <returns>Formatted text with available header information and encryption warning.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawData"/> is null.</exception>
    string FormatEncryptedPacket(byte[] rawData, DateTime timestamp, TimeSpan? timeDelta = null);

    /// <summary>
    /// Formats a packet that failed to parse due to an error.
    /// </summary>
    /// <param name="rawData">The raw packet bytes.</param>
    /// <param name="timestamp">The timestamp when the packet was captured.</param>
    /// <param name="timeDelta">Optional time difference from the previous packet.</param>
    /// <param name="errorMessage">The error message describing why parsing failed.</param>
    /// <returns>Formatted text with error information and raw data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawData"/> is null.</exception>
    string FormatError(byte[] rawData, DateTime timestamp, TimeSpan? timeDelta, string errorMessage);
}
