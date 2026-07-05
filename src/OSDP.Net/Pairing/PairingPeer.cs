using System;

namespace OSDP.Net.Pairing;

/// <summary>
/// Resolves a peer's presented credential (a full certificate or a thumbprint reference) into a
/// certificate and validates it against the configured trust anchor and optional approval hook.
/// </summary>
internal static class PairingPeer
{
    internal static C509Certificate ResolveAndValidate(PairingConfiguration configuration, int credentialType,
        byte[] credential)
    {
        C509Certificate certificate;
        switch (credentialType)
        {
            case PairingMessages.CredentialTypeCertificate:
                certificate = C509Certificate.Decode(credential);
                break;
            case PairingMessages.CredentialTypeThumbprint:
                certificate = configuration.ResolvePeerCertificate?.Invoke(credential);
                if (certificate == null)
                {
                    throw new PairingException(PairingStatus.UnknownCredentialReference,
                        "Peer presented a certificate thumbprint that could not be resolved.");
                }

                break;
            default:
                throw new PairingException(PairingStatus.ProtocolError,
                    $"Unknown credential presentation type {credentialType}.");
        }

        var now = configuration.EnforceValidityPeriod ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
        if (!configuration.TrustAnchor.Validate(certificate, now))
        {
            throw new PairingException(PairingStatus.PeerCertificateRejected,
                "Peer certificate was not accepted by the trust anchor.");
        }

        if (configuration.ApprovePeer != null && !configuration.ApprovePeer(certificate))
        {
            throw new PairingException(PairingStatus.PolicyRejected,
                "Peer certificate was rejected by the approval policy.");
        }

        return certificate;
    }
}
