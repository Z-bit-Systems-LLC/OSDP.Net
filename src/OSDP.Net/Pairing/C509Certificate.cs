using System;
using System.Text;

namespace OSDP.Net.Pairing;

/// <summary>
/// A compact, C.509-style device certificate encoded as deterministic CBOR. It binds a
/// <see cref="DeviceIdentity"/> to an ML-DSA-44 public key and is signed with ML-DSA-44 by
/// an issuing authority (a <see cref="CertificateAuthority"/> or the subject itself for a
/// self-signed certificate).
/// </summary>
/// <remarks>
/// The encoding is intentionally small enough for the OSDP link and constrained-PD storage.
/// It is not an X.509 certificate and carries no path-validation state; it is used only as a
/// signed carrier of a device identity and its verification key within the pairing profile.
/// </remarks>
public sealed class C509Certificate
{
    /// <summary>Algorithm identifier value denoting ML-DSA-44 (FIPS 204).</summary>
    public const int AlgorithmMLDsa44 = 1;

    /// <summary>The issuer string used to mark a self-signed certificate.</summary>
    public const string SelfIssuer = "self";

    private const int CurrentVersion = 1;
    private const int SerialNumberSize = 8;
    private static readonly byte[] SignatureDomain = Encoding.UTF8.GetBytes("OSDP-C509-v1");

    private C509Certificate(byte[] serialNumber, string issuer, long notBefore, long notAfter,
        DeviceIdentity subject, byte[] publicKey, byte[] signature)
    {
        SerialNumber = serialNumber;
        Issuer = issuer;
        NotBefore = notBefore;
        NotAfter = notAfter;
        Subject = subject;
        PublicKey = publicKey;
        Signature = signature;
    }

    /// <summary>Gets the 8-byte issuer-assigned serial number.</summary>
    public byte[] SerialNumber { get; }

    /// <summary>Gets the issuer name, or <see cref="SelfIssuer"/> for a self-signed certificate.</summary>
    public string Issuer { get; }

    /// <summary>Gets the start of the validity window as Unix time in seconds.</summary>
    public long NotBefore { get; }

    /// <summary>Gets the end of the validity window as Unix time in seconds.</summary>
    public long NotAfter { get; }

    /// <summary>Gets the subject device identity.</summary>
    public DeviceIdentity Subject { get; }

    /// <summary>Gets the 1312-byte ML-DSA-44 subject public key.</summary>
    public byte[] PublicKey { get; }

    /// <summary>Gets the 2420-byte ML-DSA-44 signature over the to-be-signed content.</summary>
    public byte[] Signature { get; }

    /// <summary>Gets a value indicating whether this certificate is self-signed.</summary>
    public bool IsSelfSigned => Issuer == SelfIssuer;

    /// <summary>
    /// Gets the SHA-256 thumbprint of the canonical certificate encoding. Stable across
    /// encode/decode round-trips and used for by-reference presentation.
    /// </summary>
    public byte[] Thumbprint => PairingCryptoProvider.Sha256(Encode());

    /// <summary>
    /// Encodes the certificate as canonical CBOR: a two-element array of the to-be-signed
    /// content and the signature.
    /// </summary>
    public byte[] Encode()
    {
        var writer = new CborLite.Writer().WriteArrayHeader(2);
        WriteTbs(writer);
        writer.WriteByteString(Signature);
        return writer.ToArray();
    }

    /// <summary>
    /// Verifies the certificate signature against the supplied issuer ML-DSA-44 public key.
    /// </summary>
    /// <param name="issuerPublicKey">The 1312-byte issuer public key.</param>
    /// <returns><c>true</c> if the signature is valid; otherwise <c>false</c>.</returns>
    public bool VerifySignature(byte[] issuerPublicKey) =>
        PairingCryptoProvider.MLDsaVerify(issuerPublicKey, SignedMessage(), Signature);

    /// <summary>
    /// Verifies a self-signed certificate against its own subject public key.
    /// </summary>
    /// <returns><c>true</c> if self-signed and the signature is valid; otherwise <c>false</c>.</returns>
    public bool VerifySelfSignature() => IsSelfSigned && VerifySignature(PublicKey);

    /// <summary>
    /// Returns whether the certificate is within its validity window at the supplied time.
    /// </summary>
    /// <param name="now">The reference time.</param>
    public bool IsValidAt(DateTimeOffset now)
    {
        var seconds = now.ToUnixTimeSeconds();
        return seconds >= NotBefore && seconds <= NotAfter;
    }

    /// <summary>Parses a certificate from its canonical CBOR encoding.</summary>
    /// <param name="data">The encoded certificate bytes.</param>
    /// <returns>The decoded certificate.</returns>
    /// <exception cref="FormatException">Thrown when the encoding is malformed.</exception>
    public static C509Certificate Decode(byte[] data)
    {
        var reader = new CborLite.Reader(data);
        if (reader.ReadArrayHeader() != 2)
        {
            throw new FormatException("C.509 certificate must be a 2-element array.");
        }

        var (serialNumber, issuer, notBefore, notAfter, subject, publicKey) = ReadTbs(reader);
        var signature = reader.ReadByteString();
        return new C509Certificate(serialNumber, issuer, notBefore, notAfter, subject, publicKey, signature);
    }

    internal static C509Certificate Create(byte[] serialNumber, string issuer, long notBefore, long notAfter,
        DeviceIdentity subject, byte[] subjectPublicKey, byte[] signingPrivateKey)
    {
        if (serialNumber == null || serialNumber.Length != SerialNumberSize)
        {
            throw new ArgumentException($"Serial number must be {SerialNumberSize} bytes.", nameof(serialNumber));
        }

        var unsigned = new C509Certificate(serialNumber, issuer, notBefore, notAfter, subject, subjectPublicKey,
            Array.Empty<byte>());
        var signature = PairingCryptoProvider.MLDsaSign(signingPrivateKey, unsigned.SignedMessage());
        return new C509Certificate(serialNumber, issuer, notBefore, notAfter, subject, subjectPublicKey, signature);
    }

    private byte[] SignedMessage()
    {
        var tbs = EncodeTbs();
        var message = new byte[SignatureDomain.Length + tbs.Length];
        Buffer.BlockCopy(SignatureDomain, 0, message, 0, SignatureDomain.Length);
        Buffer.BlockCopy(tbs, 0, message, SignatureDomain.Length, tbs.Length);
        return message;
    }

    private byte[] EncodeTbs()
    {
        var writer = new CborLite.Writer();
        WriteTbs(writer);
        return writer.ToArray();
    }

    private void WriteTbs(CborLite.Writer writer)
    {
        writer.WriteArrayHeader(8)
            .WriteUInt(CurrentVersion)
            .WriteByteString(SerialNumber)
            .WriteTextString(Issuer)
            .WriteArrayHeader(2)
            .WriteUInt((ulong)NotBefore)
            .WriteUInt((ulong)NotAfter)
            .WriteArrayHeader(3)
            .WriteTextString(Subject.Manufacturer)
            .WriteTextString(Subject.Model)
            .WriteTextString(Subject.SerialNumber)
            .WriteUInt(AlgorithmMLDsa44)
            .WriteByteString(PublicKey)
            .WriteUInt(AlgorithmMLDsa44);
    }

    private static (byte[] serialNumber, string issuer, long notBefore, long notAfter, DeviceIdentity subject, byte[]
        publicKey) ReadTbs(CborLite.Reader reader)
    {
        if (reader.ReadArrayHeader() != 8)
        {
            throw new FormatException("C.509 to-be-signed content must be an 8-element array.");
        }

        if (reader.ReadUInt() != CurrentVersion)
        {
            throw new FormatException("Unsupported C.509 certificate version.");
        }

        var serialNumber = reader.ReadByteString();
        var issuer = reader.ReadTextString();

        if (reader.ReadArrayHeader() != 2)
        {
            throw new FormatException("C.509 validity must be a 2-element array.");
        }

        var notBefore = (long)reader.ReadUInt();
        var notAfter = (long)reader.ReadUInt();

        if (reader.ReadArrayHeader() != 3)
        {
            throw new FormatException("C.509 subject must be a 3-element array.");
        }

        var subject = new DeviceIdentity(reader.ReadTextString(), reader.ReadTextString(), reader.ReadTextString());

        var publicKeyAlgorithm = (int)reader.ReadUInt();
        var publicKey = reader.ReadByteString();
        var signatureAlgorithm = (int)reader.ReadUInt();

        if (publicKeyAlgorithm != AlgorithmMLDsa44 || signatureAlgorithm != AlgorithmMLDsa44)
        {
            throw new FormatException("Only ML-DSA-44 keys and signatures are supported.");
        }

        return (serialNumber, issuer, notBefore, notAfter, subject, publicKey);
    }
}
