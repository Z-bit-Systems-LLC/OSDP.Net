using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Kems;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace OSDP.Net.Pairing;

/// <summary>
/// Post-quantum cryptographic primitives for the OSDP asymmetric pairing profile:
/// ML-KEM-768 key encapsulation (FIPS 203), ML-DSA-44 signatures (FIPS 204), and
/// HKDF-SHA256 key derivation (RFC 5869).
/// </summary>
/// <remarks>
/// BouncyCastle is used for the post-quantum primitives on every target framework.
/// The native <c>System.Security.Cryptography.MLKem</c>/<c>MLDsa</c> types on .NET 10
/// are platform-gated (Windows CNG PQC / OpenSSL 3.5+), so a single BouncyCastle code
/// path keeps behavior — and the deterministic test vectors — identical everywhere.
/// A native fast path can be added later following the SC2CryptoProvider pattern.
/// HKDF, HMAC and SHA-256 use the native framework implementations.
/// </remarks>
internal static class PairingCryptoProvider
{
    /// <summary>Length in bytes of an ML-KEM-768 key-generation seed (d || z).</summary>
    internal const int MLKemSeedSize = 64;

    /// <summary>Length in bytes of an ML-KEM-768 encapsulation (public) key.</summary>
    internal const int MLKemPublicKeySize = 1184;

    /// <summary>Length in bytes of an ML-KEM-768 ciphertext.</summary>
    internal const int MLKemCiphertextSize = 1088;

    /// <summary>Length in bytes of an ML-KEM / ML-DSA shared or hash output.</summary>
    internal const int SharedSecretSize = 32;

    /// <summary>Length in bytes of an ML-DSA-44 key-generation seed.</summary>
    internal const int MLDsaSeedSize = 32;

    /// <summary>Length in bytes of an ML-DSA-44 public key.</summary>
    internal const int MLDsaPublicKeySize = 1312;

    /// <summary>Length in bytes of an ML-DSA-44 signature.</summary>
    internal const int MLDsaSignatureSize = 2420;

    /// <summary>An ML-KEM-768 key pair as encoded byte arrays.</summary>
    internal readonly struct MLKemKeyPair
    {
        internal MLKemKeyPair(byte[] publicKey, byte[] privateKey)
        {
            PublicKey = publicKey;
            PrivateKey = privateKey;
        }

        /// <summary>The 1184-byte encapsulation (public) key.</summary>
        internal byte[] PublicKey { get; }

        /// <summary>The encoded decapsulation (private) key.</summary>
        internal byte[] PrivateKey { get; }
    }

    /// <summary>An ML-DSA-44 key pair as encoded byte arrays.</summary>
    internal readonly struct MLDsaKeyPair
    {
        internal MLDsaKeyPair(byte[] publicKey, byte[] privateKey)
        {
            PublicKey = publicKey;
            PrivateKey = privateKey;
        }

        /// <summary>The 1312-byte public key.</summary>
        internal byte[] PublicKey { get; }

        /// <summary>The encoded private key.</summary>
        internal byte[] PrivateKey { get; }
    }

    /// <summary>Returns <paramref name="count"/> cryptographically secure random bytes.</summary>
    internal static byte[] RandomBytes(int count)
    {
        var bytes = new byte[count];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// Generates an ML-KEM-768 key pair. When <paramref name="seed"/> is supplied it must
    /// be 64 bytes and the key pair is deterministic (used for test vectors); otherwise a
    /// random seed is used.
    /// </summary>
    internal static MLKemKeyPair GenerateMLKemKeyPair(byte[] seed = null)
    {
        var actualSeed = seed ?? RandomBytes(MLKemSeedSize);
        if (actualSeed.Length != MLKemSeedSize)
        {
            throw new ArgumentException($"ML-KEM seed must be {MLKemSeedSize} bytes.", nameof(seed));
        }

        var privateKey = MLKemPrivateKeyParameters.FromSeed(MLKemParameters.ml_kem_768, actualSeed);
        return new MLKemKeyPair(privateKey.GetPublicKeyEncoded(), privateKey.GetEncoded());
    }

    /// <summary>
    /// Encapsulates to an ML-KEM-768 public key, producing the ciphertext to transmit and
    /// the 32-byte shared secret.
    /// </summary>
    internal static (byte[] ciphertext, byte[] sharedSecret) MLKemEncapsulate(byte[] publicKey)
    {
        var key = MLKemPublicKeyParameters.FromEncoding(MLKemParameters.ml_kem_768, publicKey);
        var encapsulator = new MLKemEncapsulator(MLKemParameters.ml_kem_768);
        encapsulator.Init(key);

        var ciphertext = new byte[encapsulator.EncapsulationLength];
        var sharedSecret = new byte[encapsulator.SecretLength];
        encapsulator.Encapsulate(ciphertext, 0, ciphertext.Length, sharedSecret, 0, sharedSecret.Length);
        return (ciphertext, sharedSecret);
    }

    /// <summary>
    /// Decapsulates an ML-KEM-768 ciphertext with the private key, recovering the 32-byte
    /// shared secret. ML-KEM's implicit rejection means a corrupted ciphertext yields a
    /// pseudo-random secret rather than an error, which the handshake catches via MAC failure.
    /// </summary>
    internal static byte[] MLKemDecapsulate(byte[] privateKey, byte[] ciphertext)
    {
        var key = MLKemPrivateKeyParameters.FromEncoding(MLKemParameters.ml_kem_768, privateKey);
        var decapsulator = new MLKemDecapsulator(MLKemParameters.ml_kem_768);
        decapsulator.Init(key);

        var sharedSecret = new byte[decapsulator.SecretLength];
        decapsulator.Decapsulate(ciphertext, 0, ciphertext.Length, sharedSecret, 0, sharedSecret.Length);
        return sharedSecret;
    }

    /// <summary>
    /// Generates an ML-DSA-44 key pair. When <paramref name="seed"/> is supplied it must be
    /// 32 bytes and the key pair is deterministic (used for reproducible demo certificates
    /// and test vectors); otherwise a random seed is used.
    /// </summary>
    internal static MLDsaKeyPair GenerateMLDsaKeyPair(byte[] seed = null)
    {
        var actualSeed = seed ?? RandomBytes(MLDsaSeedSize);
        if (actualSeed.Length != MLDsaSeedSize)
        {
            throw new ArgumentException($"ML-DSA seed must be {MLDsaSeedSize} bytes.", nameof(seed));
        }

        var privateKey = MLDsaPrivateKeyParameters.FromSeed(MLDsaParameters.ml_dsa_44, actualSeed);
        return new MLDsaKeyPair(privateKey.GetPublicKeyEncoded(), privateKey.GetEncoded());
    }

    /// <summary>
    /// Produces a deterministic ML-DSA-44 signature over <paramref name="message"/> using the
    /// encoded private key.
    /// </summary>
    internal static byte[] MLDsaSign(byte[] privateKey, byte[] message)
    {
        var key = MLDsaPrivateKeyParameters.FromEncoding(MLDsaParameters.ml_dsa_44, privateKey);
        var signer = new MLDsaSigner(MLDsaParameters.ml_dsa_44, deterministic: true);
        signer.Init(true, key);
        signer.BlockUpdate(message, 0, message.Length);
        return signer.GenerateSignature();
    }

    /// <summary>
    /// Verifies an ML-DSA-44 signature over <paramref name="message"/> using the encoded public key.
    /// </summary>
    internal static bool MLDsaVerify(byte[] publicKey, byte[] message, byte[] signature)
    {
        var key = MLDsaPublicKeyParameters.FromEncoding(MLDsaParameters.ml_dsa_44, publicKey);
        var signer = new MLDsaSigner(MLDsaParameters.ml_dsa_44, deterministic: true);
        signer.Init(false, key);
        signer.BlockUpdate(message, 0, message.Length);
        return signer.VerifySignature(signature);
    }

    /// <summary>Computes SHA-256 over <paramref name="data"/>.</summary>
    internal static byte[] Sha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }

    /// <summary>Computes HMAC-SHA256 over <paramref name="data"/> using <paramref name="key"/>.</summary>
    internal static byte[] HmacSha256(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    /// <summary>
    /// HKDF-Extract (RFC 5869): PRK = HMAC-SHA256(salt, ikm). An empty salt is treated as a
    /// string of 32 zero bytes per the RFC.
    /// </summary>
    internal static byte[] HkdfExtract(byte[] salt, byte[] ikm)
    {
        var actualSalt = salt is { Length: > 0 } ? salt : new byte[SharedSecretSize];
        return HmacSha256(actualSalt, ikm);
    }

    /// <summary>
    /// HKDF-Expand (RFC 5869) using SHA-256, producing <paramref name="length"/> bytes of
    /// output keying material from a pseudo-random key and context <paramref name="info"/>.
    /// </summary>
    internal static byte[] HkdfExpand(byte[] prk, byte[] info, int length)
    {
        if (length < 0 || length > 255 * SharedSecretSize)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var output = new byte[length];
        var previous = Array.Empty<byte>();
        var generated = 0;
        byte counter = 1;
        var contextInfo = info ?? Array.Empty<byte>();

        using var hmac = new HMACSHA256(prk);
        while (generated < length)
        {
            hmac.Initialize();
            var input = new byte[previous.Length + contextInfo.Length + 1];
            Buffer.BlockCopy(previous, 0, input, 0, previous.Length);
            Buffer.BlockCopy(contextInfo, 0, input, previous.Length, contextInfo.Length);
            input[input.Length - 1] = counter;

            previous = hmac.ComputeHash(input);
            var toCopy = Math.Min(previous.Length, length - generated);
            Buffer.BlockCopy(previous, 0, output, generated, toCopy);
            generated += toCopy;
            counter++;
        }

        return output;
    }

    /// <summary>
    /// Convenience one-shot HKDF (RFC 5869): Extract then Expand, using SHA-256.
    /// </summary>
    internal static byte[] Hkdf(byte[] salt, byte[] ikm, byte[] info, int length)
    {
        var prk = HkdfExtract(salt, ikm);
        return HkdfExpand(prk, info, length);
    }
}
