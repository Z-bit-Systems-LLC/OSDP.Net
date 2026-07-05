using System;

namespace OSDP.Net.Pairing;

/// <summary>
/// The private key material and certificate a device presents during pairing: an ML-DSA-44
/// key pair whose public half is bound by <see cref="Certificate"/>. The device signs the
/// pairing transcript with the private key to prove possession of the certified identity.
/// </summary>
public sealed class PairingCredentials
{
    private readonly byte[] _privateKey;

    private PairingCredentials(C509Certificate certificate, byte[] privateKey)
    {
        Certificate = certificate;
        _privateKey = privateKey;
    }

    /// <summary>Gets the certificate that binds this device's identity to its public key.</summary>
    public C509Certificate Certificate { get; }

    /// <summary>Gets the 1312-byte ML-DSA-44 public key certified by <see cref="Certificate"/>.</summary>
    public byte[] PublicKey => Certificate.PublicKey;

    /// <summary>
    /// Generates a fresh device key pair and obtains a certificate for it from the supplied CA.
    /// </summary>
    /// <param name="identity">The device identity to certify.</param>
    /// <param name="certificateAuthority">The issuing certificate authority.</param>
    /// <param name="seed">Optional 32-byte ML-DSA seed for a reproducible device key (test/demo use).</param>
    public static PairingCredentials Generate(DeviceIdentity identity, CertificateAuthority certificateAuthority,
        byte[] seed = null)
    {
        if (certificateAuthority == null)
        {
            throw new ArgumentNullException(nameof(certificateAuthority));
        }

        var keyPair = PairingCryptoProvider.GenerateMLDsaKeyPair(seed);
        var certificate = certificateAuthority.IssueCertificate(identity, keyPair.PublicKey);
        return new PairingCredentials(certificate, keyPair.PrivateKey);
    }

    /// <summary>
    /// Generates a fresh device key pair and a self-signed certificate for it. Peers must trust
    /// the certificate by pinned thumbprint rather than by CA.
    /// </summary>
    /// <param name="identity">The device identity to certify.</param>
    /// <param name="seed">Optional 32-byte ML-DSA seed for a reproducible device key (test/demo use).</param>
    public static PairingCredentials GenerateSelfSigned(DeviceIdentity identity, byte[] seed = null)
    {
        var keyPair = PairingCryptoProvider.GenerateMLDsaKeyPair(seed);
        var now = DateTimeOffset.UtcNow;
        var certificate = C509Certificate.Create(PairingCryptoProvider.RandomBytes(8), C509Certificate.SelfIssuer,
            now.ToUnixTimeSeconds(), (now + CertificateAuthority.DefaultValidity).ToUnixTimeSeconds(), identity,
            keyPair.PublicKey, keyPair.PrivateKey);
        return new PairingCredentials(certificate, keyPair.PrivateKey);
    }

    /// <summary>
    /// Reconstructs credentials from an existing certificate and its matching encoded private key
    /// (for example, credentials provisioned at the factory).
    /// </summary>
    /// <param name="certificate">The device certificate.</param>
    /// <param name="encodedPrivateKey">The encoded ML-DSA-44 private key matching the certificate public key.</param>
    public static PairingCredentials FromExisting(C509Certificate certificate, byte[] encodedPrivateKey)
    {
        if (certificate == null)
        {
            throw new ArgumentNullException(nameof(certificate));
        }

        if (encodedPrivateKey == null)
        {
            throw new ArgumentNullException(nameof(encodedPrivateKey));
        }

        return new PairingCredentials(certificate, (byte[])encodedPrivateKey.Clone());
    }

    internal byte[] Sign(byte[] message) => PairingCryptoProvider.MLDsaSign(_privateKey, message);
}
