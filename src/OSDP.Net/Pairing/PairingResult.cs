namespace OSDP.Net.Pairing;

/// <summary>
/// The successful outcome of a pairing exchange: the derived secure channel base key and the
/// authenticated peer identity.
/// </summary>
public sealed class PairingResult
{
    internal PairingResult(byte[] scbk, C509Certificate peerCertificate)
    {
        Scbk = scbk;
        PeerCertificate = peerCertificate;
    }

    /// <summary>
    /// Gets the 32-byte secure channel base key derived from the exchange. Supply this to
    /// <c>AddDevice</c>/<c>DeviceConfiguration</c> with <c>SecureChannelVersion.V2</c> to run the
    /// symmetric SC2 handshake.
    /// </summary>
    public byte[] Scbk { get; }

    /// <summary>Gets the authenticated peer certificate.</summary>
    public C509Certificate PeerCertificate { get; }

    /// <summary>Gets the authenticated peer device identity.</summary>
    public DeviceIdentity PeerIdentity => PeerCertificate.Subject;
}
