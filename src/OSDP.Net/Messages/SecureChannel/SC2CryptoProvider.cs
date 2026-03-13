using System;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// Provides platform-optimized cryptographic operations for SC2.
/// On .NET 10+, uses native System.Security.Cryptography where supported.
/// On .NET 8, uses native AES-GCM with BouncyCastle KMAC256.
/// On netstandard2.0, uses BouncyCastle for all operations (no AES-NI acceleration).
/// </summary>
internal static class SC2CryptoProvider
{
    /// <summary>
    /// Computes KMAC256(key, data, outputBitLength, "").
    /// Uses native .NET implementation on .NET 10+ when available, BouncyCastle otherwise.
    /// </summary>
    /// <param name="key">The KMAC key.</param>
    /// <param name="data">The input data.</param>
    /// <param name="outputBitLength">The desired output length in bits.</param>
    /// <returns>The KMAC256 output.</returns>
    internal static byte[] Kmac256(byte[] key, byte[] data, int outputBitLength)
    {
        var outputLength = outputBitLength / 8;

#if NET10_0_OR_GREATER
        if (System.Security.Cryptography.Kmac256.IsSupported)
        {
            return System.Security.Cryptography.Kmac256.HashData(key, data, outputLength);
        }
#endif

        return Kmac256BouncyCastle(key, data, outputLength);
    }

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM with optional associated data.
    /// Uses native AES-GCM on .NET 8+ (hardware-accelerated), BouncyCastle on netstandard2.0.
    /// </summary>
    /// <param name="key">The 32-byte AES-256 key.</param>
    /// <param name="nonce">The 12-byte GCM nonce.</param>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <param name="ciphertext">Destination span for the ciphertext (same length as plaintext).</param>
    /// <param name="tag">Destination span for the 16-byte GCM authentication tag.</param>
    /// <param name="associatedData">Optional associated data to authenticate but not encrypt.</param>
    internal static void AesGcmEncrypt(byte[] key, byte[] nonce, ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData)
    {
#if NET8_0_OR_GREATER
        using var aesGcm = new System.Security.Cryptography.AesGcm(key, 16);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
#else
        AesGcmEncryptBouncyCastle(key, nonce, plaintext, ciphertext, tag, associatedData);
#endif
    }

    /// <summary>
    /// Decrypts ciphertext using AES-256-GCM with optional associated data.
    /// Uses native AES-GCM on .NET 8+ (hardware-accelerated), BouncyCastle on netstandard2.0.
    /// </summary>
    /// <param name="key">The 32-byte AES-256 key.</param>
    /// <param name="nonce">The 12-byte GCM nonce.</param>
    /// <param name="ciphertext">The ciphertext to decrypt.</param>
    /// <param name="tag">The 16-byte GCM authentication tag.</param>
    /// <param name="plaintext">Destination span for the decrypted plaintext.</param>
    /// <param name="associatedData">Optional associated data that was authenticated.</param>
    internal static void AesGcmDecrypt(byte[] key, byte[] nonce, ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
#if NET8_0_OR_GREATER
        using var aesGcm = new System.Security.Cryptography.AesGcm(key, 16);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
#else
        AesGcmDecryptBouncyCastle(key, nonce, ciphertext, tag, plaintext, associatedData);
#endif
    }

    private static byte[] Kmac256BouncyCastle(byte[] key, byte[] data, int outputLength)
    {
        var kmac = new KMac(256, Array.Empty<byte>());
        kmac.Init(new KeyParameter(key));
        kmac.BlockUpdate(data, 0, data.Length);
        var output = new byte[outputLength];
        kmac.OutputFinal(output, 0, outputLength);
        return output;
    }

#if !NET8_0_OR_GREATER
    private static void AesGcmEncryptBouncyCastle(byte[] key, byte[] nonce, ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext, Span<byte> tag, ReadOnlySpan<byte> associatedData)
    {
        var cipher = new Org.BouncyCastle.Crypto.Modes.GcmBlockCipher(
            new Org.BouncyCastle.Crypto.Engines.AesEngine());
        var parameters = new AeadParameters(new KeyParameter(key), 128, nonce,
            associatedData.Length > 0 ? associatedData.ToArray() : null);
        cipher.Init(true, parameters);

        var plaintextArray = plaintext.ToArray();
        var output = new byte[cipher.GetOutputSize(plaintextArray.Length)];
        var len = cipher.ProcessBytes(plaintextArray, 0, plaintextArray.Length, output, 0);
        cipher.DoFinal(output, len);

        // Output contains ciphertext + tag concatenated
        output.AsSpan(0, plaintextArray.Length).CopyTo(ciphertext);
        output.AsSpan(plaintextArray.Length, 16).CopyTo(tag);
    }

    private static void AesGcmDecryptBouncyCastle(byte[] key, byte[] nonce, ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        var cipher = new Org.BouncyCastle.Crypto.Modes.GcmBlockCipher(
            new Org.BouncyCastle.Crypto.Engines.AesEngine());
        var parameters = new AeadParameters(new KeyParameter(key), 128, nonce,
            associatedData.Length > 0 ? associatedData.ToArray() : null);
        cipher.Init(false, parameters);

        // BouncyCastle expects ciphertext + tag concatenated for decryption
        var input = new byte[ciphertext.Length + tag.Length];
        ciphertext.ToArray().CopyTo(input, 0);
        tag.ToArray().CopyTo(input, ciphertext.Length);

        var output = new byte[cipher.GetOutputSize(input.Length)];
        var len = cipher.ProcessBytes(input, 0, input.Length, output, 0);
        cipher.DoFinal(output, len);

        output.AsSpan(0, ciphertext.Length).CopyTo(plaintext);
    }
#endif
}
