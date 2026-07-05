using System;
using System.Threading;
using System.Threading.Tasks;

namespace OSDP.Net.Pairing;

/// <summary>
/// Controls whether re-pairing is permitted once a device already holds a paired key.
/// </summary>
public enum RePairingPolicy
{
    /// <summary>Allow a new pairing exchange to replace an existing paired key.</summary>
    Allow,

    /// <summary>Reject pairing once a key has been provisioned.</summary>
    Deny
}

/// <summary>
/// The identity, trust, and policy inputs to a pairing exchange. The same type configures both
/// the ACU (initiator) and the PD (responder); PD-only members are noted.
/// </summary>
/// <remarks>
/// On the PD, assigning a configuration to <c>DeviceConfiguration.Pairing</c> is what opts a
/// device into asymmetric pairing. When it is left null the device behaves exactly as a
/// symmetric-only SC2 (pre-shared SCBK) device and rejects pairing commands.
/// </remarks>
public sealed class PairingConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PairingConfiguration"/> class.
    /// </summary>
    /// <param name="credentials">This side's certificate and private key.</param>
    /// <param name="trustAnchor">The trust anchor used to validate the peer certificate.</param>
    public PairingConfiguration(PairingCredentials credentials, PairingTrustAnchor trustAnchor)
    {
        Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        TrustAnchor = trustAnchor ?? throw new ArgumentNullException(nameof(trustAnchor));
    }

    /// <summary>Gets this side's certificate and signing key.</summary>
    public PairingCredentials Credentials { get; }

    /// <summary>Gets the trust anchor used to validate the peer certificate.</summary>
    public PairingTrustAnchor TrustAnchor { get; }

    /// <summary>
    /// Gets or sets an optional policy hook invoked with the peer certificate after trust-anchor
    /// validation. Return <c>false</c> to reject the peer.
    /// </summary>
    public Func<C509Certificate, bool> ApprovePeer { get; set; }

    /// <summary>
    /// Gets or sets an optional resolver that maps a 32-byte certificate thumbprint to a cached
    /// certificate, enabling by-reference credential presentation. Return null if unknown.
    /// </summary>
    public Func<byte[], C509Certificate> ResolvePeerCertificate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the peer certificate validity window is enforced.
    /// Defaults to false because constrained PDs may lack a real-time clock.
    /// </summary>
    public bool EnforceValidityPeriod { get; set; }

    /// <summary>
    /// Gets or sets whether re-pairing is permitted (PD-only). Defaults to
    /// <see cref="RePairingPolicy.Allow"/>.
    /// </summary>
    public RePairingPolicy RePairingPolicy { get; set; } = RePairingPolicy.Allow;

    /// <summary>
    /// Gets or sets a predicate reporting whether a paired key is already provisioned (PD-only).
    /// Used with <see cref="RePairingPolicy.Deny"/> to reject re-pairing.
    /// </summary>
    public Func<bool> ScbkIsProvisioned { get; set; }

    /// <summary>
    /// Gets or sets the persistence callback invoked with the pairing result (derived SCBK and the
    /// authenticated peer identity) before the responder confirms success (PD-only). Return
    /// <c>true</c> once the key is durably stored; return <c>false</c> to signal a persistence
    /// failure so neither side commits the key.
    /// </summary>
    public Func<PairingResult, CancellationToken, Task<bool>> OnScbkEstablished { get; set; }
}
