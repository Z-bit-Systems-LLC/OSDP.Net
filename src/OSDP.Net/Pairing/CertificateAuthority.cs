using System;

namespace OSDP.Net.Pairing;

/// <summary>
/// An ML-DSA-44 certificate authority that issues <see cref="C509Certificate"/> device
/// certificates. Provides a deterministic demonstration CA derived from a fixed seed so that
/// demo/test certificates are reproducible; production deployments supply their own key.
/// </summary>
public sealed class CertificateAuthority
{
    /// <summary>The default validity period applied to issued certificates when none is specified.</summary>
    public static readonly TimeSpan DefaultValidity = TimeSpan.FromDays(3650);

    /// <summary>The name of the built-in demonstration certificate authority.</summary>
    public const string DemoName = "OSDP-DEMO-CA";

    /// <summary>
    /// The 32-byte seed for the built-in demonstration CA. This value is deliberately identical
    /// to the well-known SC2 demo key so a single constant identifies all demo material; it must
    /// never be used in production.
    /// </summary>
    public static readonly byte[] DemoSeed =
    {
        0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
        0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F
    };

    private readonly byte[] _privateKey;

    private CertificateAuthority(string name, byte[] publicKey, byte[] privateKey)
    {
        Name = name;
        PublicKey = publicKey;
        _privateKey = privateKey;
    }

    /// <summary>Gets the CA name placed in the issuer field of issued certificates.</summary>
    public string Name { get; }

    /// <summary>Gets the 1312-byte ML-DSA-44 CA public key used to verify issued certificates.</summary>
    public byte[] PublicKey { get; }

    /// <summary>
    /// Creates a certificate authority from a 32-byte ML-DSA-44 seed. The same seed always
    /// produces the same key pair.
    /// </summary>
    /// <param name="seed">The 32-byte key-generation seed.</param>
    /// <param name="name">The CA name.</param>
    public static CertificateAuthority FromSeed(byte[] seed, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("CA name is required.", nameof(name));
        }

        var keyPair = PairingCryptoProvider.GenerateMLDsaKeyPair(seed);
        return new CertificateAuthority(name, keyPair.PublicKey, keyPair.PrivateKey);
    }

    /// <summary>Creates a certificate authority with a freshly generated random key pair.</summary>
    /// <param name="name">The CA name.</param>
    public static CertificateAuthority Create(string name) => FromSeed(PairingCryptoProvider.RandomBytes(
        PairingCryptoProvider.MLDsaSeedSize), name);

    /// <summary>Gets the built-in reproducible demonstration certificate authority.</summary>
    public static CertificateAuthority Demo() => FromSeed(DemoSeed, DemoName);

    /// <summary>
    /// Issues a certificate binding a device identity to its ML-DSA-44 public key, valid from
    /// now for <see cref="DefaultValidity"/>.
    /// </summary>
    /// <param name="identity">The subject device identity.</param>
    /// <param name="devicePublicKey">The 1312-byte device ML-DSA-44 public key.</param>
    public C509Certificate IssueCertificate(DeviceIdentity identity, byte[] devicePublicKey) =>
        IssueCertificate(identity, devicePublicKey, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow + DefaultValidity);

    /// <summary>
    /// Issues a certificate binding a device identity to its ML-DSA-44 public key over an explicit
    /// validity window.
    /// </summary>
    /// <param name="identity">The subject device identity.</param>
    /// <param name="devicePublicKey">The 1312-byte device ML-DSA-44 public key.</param>
    /// <param name="notBefore">The start of the validity window.</param>
    /// <param name="notAfter">The end of the validity window.</param>
    /// <param name="serialNumber">Optional 8-byte serial number; a random one is assigned when omitted.</param>
    public C509Certificate IssueCertificate(DeviceIdentity identity, byte[] devicePublicKey,
        DateTimeOffset notBefore, DateTimeOffset notAfter, byte[] serialNumber = null)
    {
        if (identity == null)
        {
            throw new ArgumentNullException(nameof(identity));
        }

        var serial = serialNumber ?? PairingCryptoProvider.RandomBytes(8);
        return C509Certificate.Create(serial, Name, notBefore.ToUnixTimeSeconds(), notAfter.ToUnixTimeSeconds(),
            identity, devicePublicKey, _privateKey);
    }
}
