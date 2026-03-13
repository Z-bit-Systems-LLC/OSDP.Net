using System;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// Specifies the version of the OSDP secure channel protocol.
/// </summary>
public enum SecureChannelVersion
{
    /// <summary>
    /// OSDP Secure Channel v1: AES-128 CBC with CBC-MAC
    /// </summary>
    V1 = 1,

    /// <summary>
    /// OSDP Secure Channel v2: AES-256 GCM with KMAC key derivation
    /// </summary>
    V2 = 2
}

/// <summary>
/// Represents the secure channel context within which a message is packed/unpacked
/// </summary>
public interface IMessageSecureChannel
{
    /// <summary>
    /// Indicates whether or not a secure channel has been established on the
    /// current message channel
    /// </summary>
    bool IsSecurityEstablished { get; }

    /// <summary>
    /// Gets a value indicating whether the secure channel is initialized.
    /// </summary>
    /// <value>
    /// <c>true</c> if the secure channel is initialized; otherwise, <c>false</c>.
    /// </value>
    public bool IsInitialized { get; }

    /// <summary>
    /// Represents the server random number used in the secure channel context.
    /// </summary>
    public byte[] ServerRandomNumber { get; }

    /// <summary>
    /// Represents the secure channel cryptogram used by the server.
    /// </summary>
    /// <remarks>
    /// This property is used to retrieve the server cryptogram for the secure channel context within which a message is packed/unpacked.
    /// </remarks>
    public byte[] ServerCryptogram { get; }

    /// <summary>
    /// Gets a value indicating whether the secure channel is using the default key.
    /// </summary>
    public bool IsUsingDefaultKey { get; }

    /// <summary>
    /// Encrypts the payload using the secure channel context. This function can
    /// only be called once IsSecurityEstablished is true.
    /// </summary>
    /// <param name="payload">Payload to be encrypted. NOTE: This data MUST already
    /// be padded in according to OSDP specification: a) there must be at least one
    /// byte of padding; b) payloaded is padded with 0x00, except for the very first
    /// byte of padding which must be 0x80; c) total length must be divisible by
    /// AES block size of 16 bytes.</param>
    /// <param name="destination">Identifies where encoded data is to written to</param>
    void EncodePayload(byte[] payload, Span<byte> destination);

    /// <summary>
    /// Decodes the payload using the secure channel context. This function can
    /// only be called once IsSecurityEstablished is true
    /// </summary>
    /// <param name="payload">Payload to be decrypted</param>
    /// <returns>
    /// Plaintext message payload. Similarly to how original payload must have been
    /// padded prior to being encrypted, the plaintext byte buffer returned here will
    /// also be padded and therefore its length will always be divisible by 16. The 
    /// padding at the end of the buffer will always consist of zero or more 0x00
    /// bytes preceeded by 0x80 byte.
    /// </returns>
    byte[] DecodePayload(byte[] payload);

    /// <summary>
    /// Generates a new Message Authentication Code. Note this command IS NOT
    /// idempotent as every time it is called it will calculate a new rolling MAC
    /// value based on the results of the previous call
    /// </summary>
    /// <param name="message">Message to be signed with a MAC</param>
    /// <param name="isIncoming">If true, indicates that the message was received
    /// from the wire. If false, indicates that the message is being sent out 
    /// on the wire</param>
    /// <returns>16-byte MAC value of the message</returns>
    ReadOnlySpan<byte> GenerateMac(ReadOnlySpan<byte> message, bool isIncoming);

    /// <summary>
    /// Initializes the Access Control Unit (ACU) in the secure channel context.
    /// </summary>
    /// <param name="clientRandomNumber">The client random number.</param>
    /// <param name="clientCryptogram">The client cryptogram.</param>
    void InitializeACU(byte[] clientRandomNumber, byte[] clientCryptogram);

    /// <summary>
    /// Resets the secure channel session by creating a new random number.
    /// </summary>
    void ResetSecureChannelSession();

    /// <summary>
    /// Establishes the secure channel with the provided RMAC.
    /// </summary>
    /// <param name="rmac">The RMAC.</param>
    void Establish(byte[] rmac);

    /// <summary>
    /// Pads the given data with padding bytes to make it a multiple of a specified length.
    /// </summary>
    /// <param name="payload">The payload data to pad.</param>
    /// <returns>The padded data.</returns>
    ReadOnlySpan<byte> PadTheData(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Indicates whether this channel uses Secure Channel v2 (AES-256 GCM).
    /// </summary>
    bool IsSecureChannelV2 { get; }

    /// <summary>
    /// The size of the authentication tag appended to secure messages.
    /// 4 bytes for SC1 (truncated CBC-MAC), 16 bytes for SC2 (GCM tag).
    /// </summary>
    int AuthenticationTagSize { get; }

    /// <summary>
    /// Establishes the secure channel without an RMAC (used by SC2 where
    /// the initial nonce state is computed deterministically).
    /// </summary>
    void Establish();
}