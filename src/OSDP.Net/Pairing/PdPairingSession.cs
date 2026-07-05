using System;

namespace OSDP.Net.Pairing;

/// <summary>
/// The PD (responder) side of the pairing exchange, driven as a transport-free state machine:
/// process message 1 to produce message 2, then process message 3 and build the result. The SCBK
/// is only committed after the host persistence callback succeeds.
/// </summary>
internal sealed class PdPairingSession
{
    private enum State
    {
        Idle,
        AwaitMessage3,
        AwaitPersist,
        Complete,
        Failed
    }

    /// <summary>The result of processing message 3, before the SCBK is persisted.</summary>
    internal sealed class Message3Outcome
    {
        internal bool Success { get; init; }
        internal byte[] Scbk { get; init; }
        internal byte[] FailureResult { get; init; }
    }

    private readonly PairingConfiguration _configuration;
    private readonly byte[] _nonceP;

    private State _state = State.Idle;
    private byte[] _th2;
    private byte[] _signatureP;
    private byte[] _macP;
    private byte[] _km3;
    private byte[] _km4;
    private byte[] _sharedSecret;
    private byte[] _th4;
    private byte[] _scbk;
    private C509Certificate _peerCertificate;

    internal PdPairingSession(PairingConfiguration configuration, byte[] nonceP = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _nonceP = nonceP;
    }

    /// <summary>Gets the authenticated ACU certificate once message 1 has been processed.</summary>
    internal C509Certificate PeerCertificate => _peerCertificate;

    /// <summary>
    /// Validates message 1, enforces re-pairing policy, encapsulates to the ACU ephemeral key, and
    /// produces message 2.
    /// </summary>
    internal byte[] ProcessMessage1(byte[] message1Wire)
    {
        Expect(State.Idle);

        try
        {
            if (_configuration.RePairingPolicy == RePairingPolicy.Deny &&
                _configuration.ScbkIsProvisioned != null && _configuration.ScbkIsProvisioned())
            {
                throw new PairingException(PairingStatus.PolicyRejected,
                    "Re-pairing is disabled and a paired key is already provisioned.");
            }

            var message1 = PairingMessages.ParseMessage1(message1Wire);
            if (message1.Version != PairingMessages.ProtocolVersion || message1.Suite != PairingMessages.CipherSuite)
            {
                throw new PairingException(PairingStatus.ProtocolError,
                    "Unsupported pairing protocol version or cipher suite.");
            }

            _peerCertificate = PairingPeer.ResolveAndValidate(_configuration, message1.CredentialType,
                message1.Credential);

            var th1 = PairingKeySchedule.Th1(message1Wire);
            var (ciphertext, sharedSecret) = PairingCryptoProvider.MLKemEncapsulate(message1.EphemeralPublicKey);
            _sharedSecret = sharedSecret;

            var nonceP = _nonceP ?? PairingCryptoProvider.RandomBytes(16);
            var credential = _configuration.Credentials.Certificate.Encode();
            var coreBytes = PairingMessages.EncodeMessage2Core(nonceP, ciphertext,
                PairingMessages.CredentialTypeCertificate, credential);

            _th2 = PairingKeySchedule.Th2(th1, coreBytes);
            _signatureP = _configuration.Credentials.Sign(PairingKeySchedule.SignedMessage2(_th2));

            var confirmationKeys = PairingKeySchedule.DeriveConfirmationKeys(sharedSecret, _th2);
            _km3 = confirmationKeys.Km3;
            _km4 = confirmationKeys.Km4;
            _macP = PairingCryptoProvider.HmacSha256(confirmationKeys.Km2, _th2);

            _state = State.AwaitMessage3;
            return PairingMessages.EncodeMessage2(coreBytes, _signatureP, _macP);
        }
        catch (Exception ex) when (ex is not PairingException)
        {
            _state = State.Failed;
            throw new PairingException(PairingStatus.ProtocolError, "Failed to process pairing message 1.", ex);
        }
        catch (PairingException)
        {
            _state = State.Failed;
            throw;
        }
    }

    /// <summary>
    /// Verifies the ACU signature and key confirmation in message 3. On success the SCBK is derived
    /// (but not yet committed); on failure a ready-to-send authentication-failure result is returned.
    /// </summary>
    internal Message3Outcome ProcessMessage3(byte[] message3Wire)
    {
        Expect(State.AwaitMessage3);

        PairingMessages.Message3 message3;
        try
        {
            message3 = PairingMessages.ParseMessage3(message3Wire);
        }
        catch (Exception ex)
        {
            _state = State.Failed;
            throw new PairingException(PairingStatus.ProtocolError, "Failed to parse pairing message 3.", ex);
        }

        var th3 = PairingKeySchedule.Th3(_th2, _signatureP, _macP);

        var signatureValid = PairingCryptoProvider.MLDsaVerify(_peerCertificate.PublicKey,
            PairingKeySchedule.SignedMessage3(th3), message3.SignatureA);
        var expectedMacA = PairingCryptoProvider.HmacSha256(_km3, th3);
        var macValid = ConstantTimeEquals(expectedMacA, message3.MacA);

        if (!signatureValid || !macValid)
        {
            _state = State.Failed;
            return new Message3Outcome
            {
                Success = false,
                FailureResult = PairingMessages.EncodeResult(PairingStatus.AuthenticationFailed, Array.Empty<byte>())
            };
        }

        _th4 = PairingKeySchedule.Th4(th3, message3.SignatureA, message3.MacA);
        _scbk = PairingKeySchedule.DeriveScbk(_sharedSecret, _th4);
        _state = State.AwaitPersist;
        return new Message3Outcome { Success = true, Scbk = _scbk };
    }

    /// <summary>
    /// Builds the final result message. A success status carries the final key-confirmation MAC and
    /// marks the session complete; any other status marks it failed and carries no MAC.
    /// </summary>
    internal byte[] BuildResult(PairingStatus status)
    {
        Expect(State.AwaitPersist);

        if (status == PairingStatus.Success)
        {
            var macR = PairingCryptoProvider.HmacSha256(_km4, _th4);
            _state = State.Complete;
            return PairingMessages.EncodeResult(PairingStatus.Success, macR);
        }

        _state = State.Failed;
        return PairingMessages.EncodeResult(status, Array.Empty<byte>());
    }

    private void Expect(State expected)
    {
        if (_state != expected)
        {
            throw new PairingException(PairingStatus.ProtocolError,
                $"Pairing message received out of sequence (state {_state}).");
        }
    }

    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }
}
