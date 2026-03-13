using System;
using System.Security.Cryptography;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// Security context for OSDP Secure Channel v2 (SC2).
/// Uses AES-256 GCM for authenticated encryption and KMAC256 for key derivation.
/// </summary>
internal class SC2SecurityContext
{
    private const uint TerminalCount = 500_000_000;
    private const int KeySize = 32;
    private const int RandomSize = 16;
    private const int NonceSize = 12;
    private const int CryptogramSize = 32;

    private readonly byte[] _scbk;

    /// <summary>
    /// Creates a new SC2 security context with the specified base key.
    /// </summary>
    /// <param name="scbk">The 32-byte secure channel base key (SCBK).</param>
    /// <exception cref="ArgumentException">Thrown when the key is not 32 bytes.</exception>
    public SC2SecurityContext(byte[] scbk)
    {
        if (scbk == null || scbk.Length != KeySize)
        {
            throw new ArgumentException($"SC2 requires a {KeySize}-byte secure channel base key.", nameof(scbk));
        }

        _scbk = (byte[])scbk.Clone();
        Reset();
    }

    /// <summary>
    /// The server random number (RndA) used in the handshake. 16 bytes for SC2.
    /// </summary>
    internal byte[] ServerRandomNumber { get; } = new byte[RandomSize];

    /// <summary>
    /// The 32-byte session encryption key derived via KMAC256.
    /// </summary>
    internal byte[] SENC { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// The 32-byte session nonce key derived via KMAC256.
    /// </summary>
    internal byte[] SNONCE { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// The 8-byte client unique identifier received during the handshake.
    /// </summary>
    internal byte[] ClientUID { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// The message counter used for GCM nonce construction. Little-endian 4 bytes.
    /// </summary>
    internal uint Counter { get; private set; }

    /// <summary>
    /// The 32-byte server cryptogram computed during ACU initialization.
    /// </summary>
    internal byte[] ServerCryptogram { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// Indicates whether session keys have been derived and cryptograms validated.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Indicates whether the secure channel has been fully established.
    /// </summary>
    internal bool IsSecurityEstablished { get; set; }

    /// <summary>
    /// Resets the security context to its initial state with a new random number.
    /// </summary>
    internal void Reset()
    {
        IsInitialized = false;
        IsSecurityEstablished = false;
        Counter = 0;
        ClientUID = Array.Empty<byte>();
        SENC = Array.Empty<byte>();
        SNONCE = Array.Empty<byte>();
        ServerCryptogram = Array.Empty<byte>();
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(ServerRandomNumber);
        }
    }

    /// <summary>
    /// Derives session keys (SENC and SNONCE) from the base key and random numbers using KMAC256.
    /// </summary>
    /// <param name="rndA">The 16-byte server random number.</param>
    /// <param name="rndB">The 16-byte client random number.</param>
    internal void DeriveSessionKeys(byte[] rndA, byte[] rndB)
    {
        // SENC = KMAC256(SCBK, RNDA || RNDB, 256, "")
        var sencInput = new byte[rndA.Length + rndB.Length];
        Buffer.BlockCopy(rndA, 0, sencInput, 0, rndA.Length);
        Buffer.BlockCopy(rndB, 0, sencInput, rndA.Length, rndB.Length);
        SENC = SC2CryptoProvider.Kmac256(_scbk, sencInput, KeySize * 8);

        // SNONCE = KMAC256(SCBK, RNDB || RNDA, 256, "")
        var snonceInput = new byte[rndB.Length + rndA.Length];
        Buffer.BlockCopy(rndB, 0, snonceInput, 0, rndB.Length);
        Buffer.BlockCopy(rndA, 0, snonceInput, rndB.Length, rndA.Length);
        SNONCE = SC2CryptoProvider.Kmac256(_scbk, snonceInput, KeySize * 8);
    }

    /// <summary>
    /// Computes a 32-byte cryptogram by AES-256 ECB encrypting the concatenation of two random numbers.
    /// </summary>
    /// <param name="rnd1">First 16-byte random number.</param>
    /// <param name="rnd2">Second 16-byte random number.</param>
    /// <returns>The 32-byte cryptogram.</returns>
    internal byte[] ComputeCryptogram(byte[] rnd1, byte[] rnd2)
    {
        var plaintext = new byte[CryptogramSize];
        Buffer.BlockCopy(rnd1, 0, plaintext, 0, rnd1.Length);
        Buffer.BlockCopy(rnd2, 0, plaintext, rnd1.Length, rnd2.Length);

        using var aes = Aes.Create();
        aes.Key = SENC;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
    }

    /// <summary>
    /// Computes the 12-byte GCM nonce from the client UID and current counter value.
    /// The nonce is derived by padding, AES-256-CBC encrypting with SNONCE, and truncating.
    /// </summary>
    /// <returns>The 12-byte GCM nonce.</returns>
    internal byte[] ComputeNonce()
    {
        // Step 1: Build 12-byte plain nonce = cUID(8) || counter(4 LE)
        var plainNonce = new byte[16];
        Buffer.BlockCopy(ClientUID, 0, plainNonce, 0, ClientUID.Length);
        plainNonce[8] = (byte)(Counter & 0xFF);
        plainNonce[9] = (byte)((Counter >> 8) & 0xFF);
        plainNonce[10] = (byte)((Counter >> 16) & 0xFF);
        plainNonce[11] = (byte)((Counter >> 24) & 0xFF);

        // Step 2: Pad to 16 bytes with 0x80, 0x00, 0x00, 0x00
        plainNonce[12] = 0x80;
        // plainNonce[13..15] are already 0x00

        // Step 3: AES-256-CBC encrypt with SNONCE key, IV = zeros
        using var aes = Aes.Create();
        aes.Key = SNONCE;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.IV = new byte[16];

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(plainNonce, 0, plainNonce.Length);

        // Step 4: Take first 12 bytes as GCM nonce
        var nonce = new byte[NonceSize];
        Buffer.BlockCopy(encrypted, 0, nonce, 0, NonceSize);
        return nonce;
    }

    /// <summary>
    /// Increments the message counter and checks against the terminal count.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the counter reaches the terminal count.</exception>
    internal void IncrementCounter()
    {
        Counter++;
        if (Counter >= TerminalCount)
        {
            throw new InvalidOperationException(
                $"SC2 session counter has reached the terminal count of {TerminalCount}. Session must be re-established.");
        }
    }

    /// <summary>
    /// Initializes the ACU side of the SC2 handshake.
    /// Derives session keys, validates the client cryptogram, and computes the server cryptogram.
    /// </summary>
    /// <param name="clientRandomNumber">The 16-byte client random number (RndB).</param>
    /// <param name="clientCryptogram">The 32-byte client cryptogram to validate.</param>
    /// <param name="cUID">The 8-byte client unique identifier.</param>
    /// <exception cref="Exception">Thrown when the client cryptogram is invalid.</exception>
    internal void InitializeACU(byte[] clientRandomNumber, byte[] clientCryptogram, byte[] cUID)
    {
        ClientUID = (byte[])cUID.Clone();
        DeriveSessionKeys(ServerRandomNumber, clientRandomNumber);

        // Validate client cryptogram: AES256_ECB(RNDA || RNDB, SENC)
        var expectedClientCryptogram = ComputeCryptogram(ServerRandomNumber, clientRandomNumber);
        if (!clientCryptogram.AsSpan().SequenceEqual(expectedClientCryptogram))
        {
            throw new Exception("Invalid client cryptogram");
        }

        // Compute server cryptogram: AES256_ECB(RNDB || RNDA, SENC)
        ServerCryptogram = ComputeCryptogram(clientRandomNumber, ServerRandomNumber);
        IsInitialized = true;
    }

    /// <summary>
    /// Establishes the SC2 secure channel. No RMAC is needed for SC2 —
    /// the initial nonce state is computed deterministically from the client UID and counter.
    /// </summary>
    internal void Establish()
    {
        Counter = 0;
        IsSecurityEstablished = true;
    }
}
