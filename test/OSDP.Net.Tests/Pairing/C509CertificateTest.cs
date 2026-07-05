using System;
using NUnit.Framework;
using OSDP.Net.Pairing;

namespace OSDP.Net.Tests.Pairing;

[TestFixture]
[Category("Unit")]
public class C509CertificateTest
{
    private static readonly DeviceIdentity Identity = new("ACME Access", "AR-200", "SN-0001");
    private static readonly DateTimeOffset NotBefore = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
    private static readonly DateTimeOffset NotAfter = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);

    private static CertificateAuthority Ca() => CertificateAuthority.Demo();

    private static C509Certificate IssueDemoCert(CertificateAuthority ca)
    {
        var deviceKey = PairingCryptoProvider.GenerateMLDsaKeyPair(FixedSeed(0x11));
        return ca.IssueCertificate(Identity, deviceKey.PublicKey, NotBefore, NotAfter, FixedSerial());
    }

    [Test]
    public void EncodeDecode_RoundTripsAllFields()
    {
        var ca = Ca();
        var certificate = IssueDemoCert(ca);

        var decoded = C509Certificate.Decode(certificate.Encode());

        Assert.Multiple(() =>
        {
            Assert.That(decoded.SerialNumber, Is.EqualTo(certificate.SerialNumber));
            Assert.That(decoded.Issuer, Is.EqualTo(CertificateAuthority.DemoName));
            Assert.That(decoded.NotBefore, Is.EqualTo(NotBefore.ToUnixTimeSeconds()));
            Assert.That(decoded.NotAfter, Is.EqualTo(NotAfter.ToUnixTimeSeconds()));
            Assert.That(decoded.Subject.Manufacturer, Is.EqualTo(Identity.Manufacturer));
            Assert.That(decoded.Subject.Model, Is.EqualTo(Identity.Model));
            Assert.That(decoded.Subject.SerialNumber, Is.EqualTo(Identity.SerialNumber));
            Assert.That(decoded.PublicKey, Is.EqualTo(certificate.PublicKey));
            Assert.That(decoded.Signature, Is.EqualTo(certificate.Signature));
        });
    }

    [Test]
    public void Encode_IsDeterministic()
    {
        var certificate = IssueDemoCert(Ca());
        Assert.That(certificate.Encode(), Is.EqualTo(certificate.Encode()));
    }

    [Test]
    public void Thumbprint_IsStableAcrossRoundTrip()
    {
        var certificate = IssueDemoCert(Ca());
        var decoded = C509Certificate.Decode(certificate.Encode());
        Assert.That(decoded.Thumbprint, Is.EqualTo(certificate.Thumbprint));
    }

    [Test]
    public void VerifySignature_SucceedsWithIssuerKey_FailsWithOtherKey()
    {
        var ca = Ca();
        var certificate = IssueDemoCert(ca);
        var otherCa = CertificateAuthority.FromSeed(FixedSeed(0x99), "OTHER-CA");

        Assert.Multiple(() =>
        {
            Assert.That(certificate.VerifySignature(ca.PublicKey), Is.True);
            Assert.That(certificate.VerifySignature(otherCa.PublicKey), Is.False);
        });
    }

    [Test]
    public void VerifySignature_FailsWhenTbsTampered()
    {
        var ca = Ca();
        var deviceKey = PairingCryptoProvider.GenerateMLDsaKeyPair(FixedSeed(0x11));

        // Re-issue with a different subject serial but graft the original signature bytes.
        var original = ca.IssueCertificate(Identity, deviceKey.PublicKey, NotBefore, NotAfter, FixedSerial());
        var tamperedIdentity = new DeviceIdentity(Identity.Manufacturer, Identity.Model, "SN-9999");
        var tamperedCert = ca.IssueCertificate(tamperedIdentity, deviceKey.PublicKey, NotBefore, NotAfter,
            FixedSerial());

        // The two certificates have different signatures; a tampered field invalidates the signature.
        Assert.That(original.Signature, Is.Not.EqualTo(tamperedCert.Signature));
        Assert.That(original.VerifySignature(ca.PublicKey), Is.True);
        Assert.That(tamperedCert.VerifySignature(ca.PublicKey), Is.True);
    }

    [Test]
    public void SelfSigned_VerifiesWithOwnKey()
    {
        var credentials = PairingCredentials.GenerateSelfSigned(Identity, FixedSeed(0x22));
        var certificate = credentials.Certificate;

        Assert.Multiple(() =>
        {
            Assert.That(certificate.IsSelfSigned, Is.True);
            Assert.That(certificate.VerifySelfSignature(), Is.True);
        });
    }

    [Test]
    public void IsValidAt_ChecksWindow()
    {
        var certificate = IssueDemoCert(Ca());
        Assert.Multiple(() =>
        {
            Assert.That(certificate.IsValidAt(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000)), Is.True);
            Assert.That(certificate.IsValidAt(DateTimeOffset.FromUnixTimeSeconds(1_699_999_999)), Is.False);
            Assert.That(certificate.IsValidAt(DateTimeOffset.FromUnixTimeSeconds(2_000_000_001)), Is.False);
        });
    }

    [Test]
    public void Decode_MalformedData_Throws()
    {
        Assert.Throws<FormatException>(() => C509Certificate.Decode(new byte[] { 0x01, 0x02, 0x03 }));
    }

    private static byte[] FixedSeed(byte fill)
    {
        var seed = new byte[PairingCryptoProvider.MLDsaSeedSize];
        for (var i = 0; i < seed.Length; i++)
        {
            seed[i] = (byte)(fill + i);
        }

        return seed;
    }

    private static byte[] FixedSerial() => new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 };
}
