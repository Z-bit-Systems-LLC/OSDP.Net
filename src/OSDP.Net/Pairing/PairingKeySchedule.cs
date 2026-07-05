using System;
using System.Text;

namespace OSDP.Net.Pairing;

/// <summary>
/// Computes the pairing transcript hashes and the HKDF-SHA256 key schedule shared by both sides.
/// The confirmation keys bind possession of the ML-KEM shared secret to the transcript; the SCBK
/// is bound to the full transcript (through TH4) so both identities are covered.
/// </summary>
internal static class PairingKeySchedule
{
    internal static readonly byte[] SignatureContextMessage2 = Encoding.UTF8.GetBytes("OSDP-PAIR-v1-msg2");
    internal static readonly byte[] SignatureContextMessage3 = Encoding.UTF8.GetBytes("OSDP-PAIR-v1-msg3");

    private static readonly byte[] InfoConfirm2 = Encoding.UTF8.GetBytes("osdp-pair confirm2");
    private static readonly byte[] InfoConfirm3 = Encoding.UTF8.GetBytes("osdp-pair confirm3");
    private static readonly byte[] InfoConfirm4 = Encoding.UTF8.GetBytes("osdp-pair confirm4");
    private static readonly byte[] InfoScbk = Encoding.UTF8.GetBytes("osdp-pair scbk");

    /// <summary>TH1 = SHA-256(message 1 wire bytes).</summary>
    internal static byte[] Th1(byte[] message1Wire) => PairingCryptoProvider.Sha256(message1Wire);

    /// <summary>TH2 = SHA-256(TH1 || message 2 core bytes).</summary>
    internal static byte[] Th2(byte[] th1, byte[] message2Core) => PairingCryptoProvider.Sha256(Concat(th1, message2Core));

    /// <summary>TH3 = SHA-256(TH2 || sig_P || mac_P).</summary>
    internal static byte[] Th3(byte[] th2, byte[] signatureP, byte[] macP) =>
        PairingCryptoProvider.Sha256(Concat(th2, signatureP, macP));

    /// <summary>TH4 = SHA-256(TH3 || sig_A || mac_A).</summary>
    internal static byte[] Th4(byte[] th3, byte[] signatureA, byte[] macA) =>
        PairingCryptoProvider.Sha256(Concat(th3, signatureA, macA));

    /// <summary>The confirmation keys derived from the ML-KEM shared secret and TH2.</summary>
    internal readonly struct ConfirmationKeys
    {
        internal ConfirmationKeys(byte[] km2, byte[] km3, byte[] km4)
        {
            Km2 = km2;
            Km3 = km3;
            Km4 = km4;
        }

        internal byte[] Km2 { get; }
        internal byte[] Km3 { get; }
        internal byte[] Km4 { get; }
    }

    /// <summary>
    /// Derives the three confirmation keys: PRK = HKDF-Extract(salt=TH2, ikm=ss), then
    /// K_mN = HKDF-Expand(PRK, "osdp-pair confirmN", 32).
    /// </summary>
    internal static ConfirmationKeys DeriveConfirmationKeys(byte[] sharedSecret, byte[] th2)
    {
        var prk = PairingCryptoProvider.HkdfExtract(th2, sharedSecret);
        return new ConfirmationKeys(
            PairingCryptoProvider.HkdfExpand(prk, InfoConfirm2, PairingCryptoProvider.SharedSecretSize),
            PairingCryptoProvider.HkdfExpand(prk, InfoConfirm3, PairingCryptoProvider.SharedSecretSize),
            PairingCryptoProvider.HkdfExpand(prk, InfoConfirm4, PairingCryptoProvider.SharedSecretSize));
    }

    /// <summary>
    /// Derives the 32-byte SCBK: SCBK = HKDF-Expand(HKDF-Extract(salt=TH4, ikm=ss),
    /// "osdp-pair scbk", 32).
    /// </summary>
    internal static byte[] DeriveScbk(byte[] sharedSecret, byte[] th4)
    {
        var prk = PairingCryptoProvider.HkdfExtract(th4, sharedSecret);
        return PairingCryptoProvider.HkdfExpand(prk, InfoScbk, PairingCryptoProvider.SharedSecretSize);
    }

    /// <summary>Builds the ML-DSA message signed by the PD in message 2: context || TH2.</summary>
    internal static byte[] SignedMessage2(byte[] th2) => Concat(SignatureContextMessage2, th2);

    /// <summary>Builds the ML-DSA message signed by the ACU in message 3: context || TH3.</summary>
    internal static byte[] SignedMessage3(byte[] th3) => Concat(SignatureContextMessage3, th3);

    private static byte[] Concat(params byte[][] parts)
    {
        var length = 0;
        foreach (var part in parts)
        {
            length += part.Length;
        }

        var result = new byte[length];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }
}
