using System;
using Microsoft.Extensions.Logging;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// Abstract base class for SC2 secure channel implementations.
/// Uses AES-256 GCM for authenticated encryption instead of AES-128 CBC.
/// </summary>
internal abstract class SC2MessageSecureChannel : IMessageSecureChannel
{
    private byte[] _lastTag = Array.Empty<byte>();

    /// <summary>
    /// Initializes a new SC2 message secure channel.
    /// </summary>
    /// <param name="context">The SC2 security context holding session state.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    protected SC2MessageSecureChannel(SC2SecurityContext context, ILoggerFactory loggerFactory = null)
    {
        SC2Context = context;
        Logger = loggerFactory?.CreateLogger(GetType());
    }

    /// <summary>
    /// The SC2 security context.
    /// </summary>
    protected SC2SecurityContext SC2Context { get; }

    /// <summary>
    /// Optional logger instance.
    /// </summary>
    protected ILogger Logger { get; }

    /// <inheritdoc />
    public bool IsSecurityEstablished => SC2Context.IsSecurityEstablished;

    /// <inheritdoc />
    public bool IsInitialized => SC2Context.IsInitialized;

    /// <inheritdoc />
    public byte[] ServerRandomNumber => SC2Context.ServerRandomNumber;

    /// <inheritdoc />
    public byte[] ServerCryptogram => SC2Context.ServerCryptogram;

    /// <inheritdoc />
    public bool IsUsingDefaultKey => false;

    /// <inheritdoc />
    public bool IsSecureChannelV2 => true;

    /// <inheritdoc />
    public int AuthenticationTagSize => 16;

    /// <inheritdoc />
    public void EncodePayload(byte[] payload, Span<byte> destination)
    {
        EncodePayload(payload, destination, ReadOnlySpan<byte>.Empty);
    }

    /// <summary>
    /// Encrypts the payload using AES-256 GCM with associated authenticated data (AAD).
    /// The AAD is typically the message header bytes (SOM through security block).
    /// </summary>
    /// <param name="payload">The plaintext to encrypt.</param>
    /// <param name="destination">The destination for ciphertext.</param>
    /// <param name="associatedData">The header bytes to authenticate but not encrypt.</param>
    public void EncodePayload(byte[] payload, Span<byte> destination, ReadOnlySpan<byte> associatedData)
    {
        if (!IsSecurityEstablished)
        {
            throw new SecureChannelRequired();
        }

        if (payload.Length == 0)
        {
            _lastTag = Array.Empty<byte>();
            return;
        }

        var nonce = SC2Context.ComputeNonce();
        var tag = new byte[16];
        SC2CryptoProvider.AesGcmEncrypt(SC2Context.SENC, nonce, payload,
            destination.Slice(0, payload.Length), tag, associatedData);
        _lastTag = tag;
        SC2Context.IncrementCounter();
    }

    /// <inheritdoc />
    public byte[] DecodePayload(byte[] payload)
    {
        return DecodePayload(payload, ReadOnlySpan<byte>.Empty);
    }

    /// <summary>
    /// Decrypts the payload using AES-256 GCM with associated authenticated data (AAD).
    /// The payload includes ciphertext followed by the 16-byte GCM tag.
    /// </summary>
    /// <param name="payload">The ciphertext + 16-byte tag.</param>
    /// <param name="associatedData">The header bytes that were authenticated.</param>
    /// <returns>The decrypted plaintext.</returns>
    public byte[] DecodePayload(byte[] payload, ReadOnlySpan<byte> associatedData)
    {
        if (!IsSecurityEstablished)
        {
            throw new SecureChannelRequired();
        }

        if (payload.Length == 0)
        {
            return Array.Empty<byte>();
        }

        // The last 16 bytes are the GCM tag
        var ciphertextLength = payload.Length - 16;
        var ciphertext = payload.AsSpan(0, ciphertextLength);
        var tag = payload.AsSpan(ciphertextLength, 16);

        var nonce = SC2Context.ComputeNonce();
        var plaintext = new byte[ciphertextLength];
        SC2CryptoProvider.AesGcmDecrypt(SC2Context.SENC, nonce, ciphertext, tag, plaintext, associatedData);
        SC2Context.IncrementCounter();
        _lastTag = tag.ToArray();
        return plaintext;
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> GenerateMac(ReadOnlySpan<byte> message, bool isIncoming)
    {
        // For SC2, the GCM tag serves as the MAC. It was computed during
        // the most recent EncodePayload or DecodePayload call.
        return _lastTag;
    }

    /// <inheritdoc />
    public void InitializeACU(byte[] clientRandomNumber, byte[] clientCryptogram)
    {
        throw new NotSupportedException("Use the SC2-specific InitializeACU overload with cUID parameter.");
    }

    /// <summary>
    /// Initializes the ACU side of the SC2 handshake.
    /// </summary>
    /// <param name="clientRandomNumber">The 16-byte client random number (RndB).</param>
    /// <param name="clientCryptogram">The 32-byte client cryptogram.</param>
    /// <param name="cUID">The 8-byte client unique identifier.</param>
    public void InitializeACU(byte[] clientRandomNumber, byte[] clientCryptogram, byte[] cUID)
    {
        SC2Context.InitializeACU(clientRandomNumber, clientCryptogram, cUID);
    }

    /// <inheritdoc />
    public void ResetSecureChannelSession() => SC2Context.Reset();

    /// <inheritdoc />
    public void Establish(byte[] rmac)
    {
        // SC2 does not use RMAC; delegate to parameterless Establish
        Establish();
    }

    /// <inheritdoc />
    public void Establish() => SC2Context.Establish();

    /// <inheritdoc />
    public ReadOnlySpan<byte> PadTheData(ReadOnlySpan<byte> payload)
    {
        // GCM handles arbitrary-length plaintext; no padding needed
        return payload;
    }
}
