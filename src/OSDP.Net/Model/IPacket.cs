#nullable enable

using System;
using OSDP.Net.Messages;

namespace OSDP.Net.Model;

/// <summary>
/// Represents a parsed OSDP packet with decoded message information.
/// This interface enables easier mocking and testing of packet consumers.
/// </summary>
public interface IPacket
{
    /// <summary>
    /// Address of the message
    /// </summary>
    byte Address { get; }

    /// <summary>
    /// Sequence number of the message
    /// </summary>
    byte Sequence { get; }

    /// <summary>
    /// The type of command sent or null if it is a reply
    /// </summary>
    CommandType? CommandType { get; }

    /// <summary>
    /// The type of reply sent or null if it is a command
    /// </summary>
    ReplyType? ReplyType { get; }

    /// <summary>
    /// Is CRC being used
    /// </summary>
    bool IsUsingCrc { get; }

    /// <summary>
    /// Indicates whether the payload was successfully decrypted.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, the payload contains decrypted data that can be parsed.
    /// When <c>false</c>, the payload is still encrypted because the secure channel was not established
    /// or the required key was not available.
    /// </remarks>
    bool IsPayloadDecrypted { get; }

    /// <summary>
    /// Indicates if the message was sent via an established secure channel.
    /// </summary>
    bool IsSecureMessage { get; }

    /// <summary>
    /// Indicates if the secure channel is using the default key.
    /// </summary>
    bool IsUsingDefaultKey { get; }

    /// <summary>
    /// Raw bytes of the payload data
    /// </summary>
    ReadOnlyMemory<byte> RawPayloadData { get; }

    /// <summary>
    /// Raw bytes of the entire message data
    /// </summary>
    ReadOnlyMemory<byte> RawData { get; }

    /// <summary>
    /// Parse the payload data into an object
    /// </summary>
    /// <returns>A message data object representation of the payload data</returns>
    object? ParsePayloadData();
}
