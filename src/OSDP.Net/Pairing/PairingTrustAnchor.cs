using System;
using System.Collections.Generic;
using System.Linq;

namespace OSDP.Net.Pairing;

/// <summary>
/// Describes which peer certificates a side is willing to accept during pairing, either by
/// trusting a certificate authority public key or by pinning a set of certificate thumbprints.
/// </summary>
public sealed class PairingTrustAnchor
{
    private readonly byte[] _caPublicKey;
    private readonly HashSet<string> _pinnedThumbprints;

    private PairingTrustAnchor(byte[] caPublicKey, HashSet<string> pinnedThumbprints)
    {
        _caPublicKey = caPublicKey;
        _pinnedThumbprints = pinnedThumbprints;
    }

    /// <summary>Gets a value indicating whether this anchor validates against a CA public key.</summary>
    public bool IsCaAnchor => _caPublicKey != null;

    /// <summary>Creates a trust anchor that accepts any certificate issued by the given CA.</summary>
    /// <param name="certificateAuthority">The trusted certificate authority.</param>
    public static PairingTrustAnchor FromCa(CertificateAuthority certificateAuthority)
    {
        if (certificateAuthority == null)
        {
            throw new ArgumentNullException(nameof(certificateAuthority));
        }

        return FromCaPublicKey(certificateAuthority.PublicKey);
    }

    /// <summary>Creates a trust anchor that accepts any certificate signed by the given CA public key.</summary>
    /// <param name="caPublicKey">The 1312-byte ML-DSA-44 CA public key.</param>
    public static PairingTrustAnchor FromCaPublicKey(byte[] caPublicKey)
    {
        if (caPublicKey == null)
        {
            throw new ArgumentNullException(nameof(caPublicKey));
        }

        return new PairingTrustAnchor((byte[])caPublicKey.Clone(), null);
    }

    /// <summary>
    /// Creates a trust anchor that accepts only certificates whose thumbprint is pinned. Suitable
    /// for self-signed certificates.
    /// </summary>
    /// <param name="thumbprints">The set of accepted SHA-256 certificate thumbprints.</param>
    public static PairingTrustAnchor FromPinnedThumbprints(IEnumerable<byte[]> thumbprints)
    {
        if (thumbprints == null)
        {
            throw new ArgumentNullException(nameof(thumbprints));
        }

        var pinned = new HashSet<string>(thumbprints.Select(ToHex), StringComparer.OrdinalIgnoreCase);
        if (pinned.Count == 0)
        {
            throw new ArgumentException("At least one thumbprint is required.", nameof(thumbprints));
        }

        return new PairingTrustAnchor(null, pinned);
    }

    /// <summary>
    /// Validates a peer certificate against this trust anchor. For a CA anchor the certificate
    /// signature must verify under the CA public key; for a pinned anchor the certificate must be
    /// self-consistent and its thumbprint must be pinned. When <paramref name="now"/> is supplied,
    /// the certificate validity window is also enforced.
    /// </summary>
    /// <param name="certificate">The peer certificate to validate.</param>
    /// <param name="now">Optional reference time for validity-window enforcement.</param>
    /// <returns><c>true</c> if the certificate is acceptable; otherwise <c>false</c>.</returns>
    public bool Validate(C509Certificate certificate, DateTimeOffset? now = null)
    {
        if (certificate == null)
        {
            return false;
        }

        if (now.HasValue && !certificate.IsValidAt(now.Value))
        {
            return false;
        }

        if (IsCaAnchor)
        {
            return certificate.VerifySignature(_caPublicKey);
        }

        return certificate.VerifySelfSignature() && _pinnedThumbprints.Contains(ToHex(certificate.Thumbprint));
    }

    private static string ToHex(byte[] value) => BitConverter.ToString(value).Replace("-", string.Empty);
}
