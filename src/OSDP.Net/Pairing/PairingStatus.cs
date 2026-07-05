namespace OSDP.Net.Pairing;

/// <summary>
/// The outcome of a pairing exchange. Values 0x00–0x04 are carried on the wire in the final
/// pairing Result message; values 0x10 and above are local-only failures raised by
/// <see cref="PairingException"/> and never transmitted.
/// </summary>
public enum PairingStatus : byte
{
    /// <summary>Pairing completed successfully and an SCBK was derived.</summary>
    Success = 0x00,

    /// <summary>A peer signature or key-confirmation MAC failed on the responder side.</summary>
    AuthenticationFailed = 0x01,

    /// <summary>The responder derived the SCBK but failed to persist it; no key was committed.</summary>
    PersistenceFailed = 0x02,

    /// <summary>The responder declined pairing by policy (for example re-pairing is disabled).</summary>
    PolicyRejected = 0x03,

    /// <summary>A message was malformed or arrived out of sequence.</summary>
    ProtocolError = 0x04,

    /// <summary>The peer certificate was not accepted by the trust anchor or approval hook (local only).</summary>
    PeerCertificateRejected = 0x10,

    /// <summary>A key-confirmation MAC failed on the initiator side (local only).</summary>
    KeyConfirmationFailed = 0x11,

    /// <summary>The peer does not support pairing (local only).</summary>
    NotSupported = 0x12,

    /// <summary>The pairing exchange timed out (local only).</summary>
    Timeout = 0x13,

    /// <summary>A certificate was presented by reference but could not be resolved (local only).</summary>
    UnknownCredentialReference = 0x14
}
