using System;

namespace OSDP.Net.Pairing;

/// <summary>
/// Raised when an asymmetric pairing exchange fails. <see cref="Status"/> classifies the failure.
/// </summary>
public class PairingException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PairingException"/> class.
    /// </summary>
    /// <param name="status">The classification of the failure.</param>
    /// <param name="message">A description of the failure.</param>
    public PairingException(PairingStatus status, string message) : base(message)
    {
        Status = status;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PairingException"/> class with an inner exception.
    /// </summary>
    /// <param name="status">The classification of the failure.</param>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The underlying exception.</param>
    public PairingException(PairingStatus status, string message, Exception innerException)
        : base(message, innerException)
    {
        Status = status;
    }

    /// <summary>Gets the classification of the failure.</summary>
    public PairingStatus Status { get; }
}
