using System;
using NUnit.Framework;
using OSDP.Net.Pairing;

namespace OSDP.Net.Tests.Pairing;

[TestFixture]
[Category("Unit")]
public class PairingCryptoProviderTest
{
    // Demo CA seed (0x40..0x5F) — the reproducible seed used for demonstration certificates.
    private static readonly byte[] DemoCaSeed = HexToBytes(
        "404142434445464748494A4B4C4D4E4F505152535455565758595A5B5C5D5E5F");

    [Test]
    public void GenerateMLKemKeyPair_FromSeed_IsDeterministic()
    {
        var seed = new byte[PairingCryptoProvider.MLKemSeedSize];
        for (var i = 0; i < seed.Length; i++)
        {
            seed[i] = (byte)i;
        }

        var a = PairingCryptoProvider.GenerateMLKemKeyPair(seed);
        var b = PairingCryptoProvider.GenerateMLKemKeyPair(seed);

        Assert.Multiple(() =>
        {
            Assert.That(a.PublicKey.Length, Is.EqualTo(PairingCryptoProvider.MLKemPublicKeySize));
            Assert.That(a.PublicKey, Is.EqualTo(b.PublicKey), "Seeded ML-KEM keygen must be deterministic");
            // Pinned public-key hash guards against a BouncyCastle behavior change.
            Assert.That(HexToBytes("0B7934C83125C788995E2BA6BD761E33046B3E40571BE53E023309A29F398CC9"),
                Is.EqualTo(PairingCryptoProvider.Sha256(a.PublicKey)));
        });
    }

    [Test]
    public void MLKem_EncapsulateDecapsulate_RoundTrips()
    {
        var keyPair = PairingCryptoProvider.GenerateMLKemKeyPair();

        var (ciphertext, encapsulatedSecret) = PairingCryptoProvider.MLKemEncapsulate(keyPair.PublicKey);
        var decapsulatedSecret = PairingCryptoProvider.MLKemDecapsulate(keyPair.PrivateKey, ciphertext);

        Assert.Multiple(() =>
        {
            Assert.That(ciphertext.Length, Is.EqualTo(PairingCryptoProvider.MLKemCiphertextSize));
            Assert.That(encapsulatedSecret.Length, Is.EqualTo(PairingCryptoProvider.SharedSecretSize));
            Assert.That(encapsulatedSecret, Is.EqualTo(decapsulatedSecret), "Shared secrets must match");
        });
    }

    [Test]
    public void MLKem_CorruptedCiphertext_ProducesDifferentSecret()
    {
        var keyPair = PairingCryptoProvider.GenerateMLKemKeyPair();
        var (ciphertext, encapsulatedSecret) = PairingCryptoProvider.MLKemEncapsulate(keyPair.PublicKey);
        ciphertext[0] ^= 0xFF;

        // ML-KEM uses implicit rejection: decapsulation of a corrupted ciphertext yields a
        // pseudo-random secret rather than throwing, which the handshake catches via MAC failure.
        var decapsulatedSecret = PairingCryptoProvider.MLKemDecapsulate(keyPair.PrivateKey, ciphertext);
        Assert.That(decapsulatedSecret, Is.Not.EqualTo(encapsulatedSecret));
    }

    [Test]
    public void GenerateMLDsaKeyPair_FromDemoSeed_MatchesPinnedPublicKey()
    {
        var keyPair = PairingCryptoProvider.GenerateMLDsaKeyPair(DemoCaSeed);

        Assert.Multiple(() =>
        {
            Assert.That(keyPair.PublicKey.Length, Is.EqualTo(PairingCryptoProvider.MLDsaPublicKeySize));
            Assert.That(HexToBytes("6C1C65071979225A139B3EC84688E2688EC30FABE8CC510CB688BC435F2D3CB9"),
                Is.EqualTo(PairingCryptoProvider.Sha256(keyPair.PublicKey)),
                "Demo CA public key changed — regenerate demo certificate vectors");
        });
    }

    [Test]
    public void MLDsa_SignVerify_RoundTripsAndIsDeterministic()
    {
        var keyPair = PairingCryptoProvider.GenerateMLDsaKeyPair(DemoCaSeed);
        var message = System.Text.Encoding.UTF8.GetBytes("OSDP-PAIR-TEST");

        var signature1 = PairingCryptoProvider.MLDsaSign(keyPair.PrivateKey, message);
        var signature2 = PairingCryptoProvider.MLDsaSign(keyPair.PrivateKey, message);

        Assert.Multiple(() =>
        {
            Assert.That(signature1.Length, Is.EqualTo(PairingCryptoProvider.MLDsaSignatureSize));
            Assert.That(signature1, Is.EqualTo(signature2), "Deterministic signing must be reproducible");
            Assert.That(HexToBytes("B66E3106C4624BE94798C68BB470DC52C29C189848B0A5522B14E121C102AEC4"),
                Is.EqualTo(PairingCryptoProvider.Sha256(signature1)));
            Assert.That(PairingCryptoProvider.MLDsaVerify(keyPair.PublicKey, message, signature1), Is.True);
        });
    }

    [Test]
    public void MLDsa_Verify_RejectsTamperedMessageAndSignature()
    {
        var keyPair = PairingCryptoProvider.GenerateMLDsaKeyPair(DemoCaSeed);
        var message = System.Text.Encoding.UTF8.GetBytes("OSDP-PAIR-TEST");
        var signature = PairingCryptoProvider.MLDsaSign(keyPair.PrivateKey, message);

        var tamperedMessage = (byte[])message.Clone();
        tamperedMessage[0] ^= 0x01;
        var tamperedSignature = (byte[])signature.Clone();
        tamperedSignature[100] ^= 0x01;

        Assert.Multiple(() =>
        {
            Assert.That(PairingCryptoProvider.MLDsaVerify(keyPair.PublicKey, tamperedMessage, signature), Is.False);
            Assert.That(PairingCryptoProvider.MLDsaVerify(keyPair.PublicKey, message, tamperedSignature), Is.False);
        });
    }

    [Test]
    public void Hkdf_MatchesRfc5869TestCase1()
    {
        // RFC 5869 Appendix A.1 (SHA-256)
        var ikm = HexToBytes("0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B0B");
        var salt = HexToBytes("000102030405060708090A0B0C");
        var info = HexToBytes("F0F1F2F3F4F5F6F7F8F9");

        var prk = PairingCryptoProvider.HkdfExtract(salt, ikm);
        var okm = PairingCryptoProvider.HkdfExpand(prk, info, 42);

        Assert.Multiple(() =>
        {
            Assert.That(prk, Is.EqualTo(HexToBytes(
                "077709362C2E32DF0DDC3F0DC47BBA6390B6C73BB50F9C3122EC844AD7C2B3E5")));
            Assert.That(okm, Is.EqualTo(HexToBytes(
                "3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865")));
        });
    }

    [Test]
    public void GenerateMLKemKeyPair_WrongSeedLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => PairingCryptoProvider.GenerateMLKemKeyPair(new byte[10]));
    }

    private static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }
}
