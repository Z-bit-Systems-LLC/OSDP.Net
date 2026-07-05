namespace OSDP.Net.Pairing;

/// <summary>
/// The stage of an asymmetric pairing exchange, reported for progress display.
/// </summary>
public enum PairingStage
{
    /// <summary>Sending pairing message 1 (the ACU credential and ephemeral key).</summary>
    SendingRequest,

    /// <summary>Receiving pairing message 2 (the PD certificate and key agreement).</summary>
    AwaitingResponse,

    /// <summary>Sending pairing message 3 (the ACU authentication).</summary>
    SendingConfirmation,

    /// <summary>Receiving the pairing result and confirming the key.</summary>
    AwaitingResult,

    /// <summary>Pairing has completed.</summary>
    Completed
}

/// <summary>
/// A progress update for an asymmetric pairing exchange, suitable for driving a progress bar.
/// </summary>
public sealed class PairingProgress
{
    internal PairingProgress(PairingStage stage, double fraction)
    {
        Stage = stage;
        Fraction = fraction < 0 ? 0 : fraction > 1 ? 1 : fraction;
    }

    /// <summary>Gets the current stage of the exchange.</summary>
    public PairingStage Stage { get; }

    /// <summary>Gets the overall completion fraction, from 0.0 to 1.0.</summary>
    public double Fraction { get; }
}
