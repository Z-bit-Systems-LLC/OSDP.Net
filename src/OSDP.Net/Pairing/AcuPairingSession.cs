using System;

namespace OSDP.Net.Pairing;

/// <summary>
/// The ACU (initiator) side of the pairing exchange, driven as a transport-free state machine:
/// create message 1, process message 2 to produce message 3, then process the result. Randomness
/// can be injected for deterministic testing.
/// </summary>
internal sealed class AcuPairingSession
{
    private enum State
    {
        Created,
        AwaitMessage2,
        AwaitResult,
        Complete,
        Failed
    }

    private readonly PairingConfiguration _configuration;
    private readonly byte[] _ephemeralSeed;
    private readonly byte[] _nonceA;

    private State _state = State.Created;
    private byte[] _ephemeralPrivateKey;
    private byte[] _th1;
    private byte[] _th4;
    private byte[] _confirmationKm4;
    private byte[] _scbk;
    private C509Certificate _peerCertificate;

    internal AcuPairingSession(PairingConfiguration configuration, byte[] ephemeralSeed = null, byte[] nonceA = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _ephemeralSeed = ephemeralSeed;
        _nonceA = nonceA;
    }

    /// <summary>Gets the derived SCBK once the exchange has completed successfully.</summary>
    internal byte[] DerivedScbk => _state == State.Complete ? _scbk : null;

    /// <summary>Builds pairing message 1 and advances to awaiting message 2.</summary>
    internal byte[] CreateMessage1()
    {
        Expect(State.Created);

        var ephemeral = PairingCryptoProvider.GenerateMLKemKeyPair(_ephemeralSeed);
        _ephemeralPrivateKey = ephemeral.PrivateKey;
        var nonceA = _nonceA ?? PairingCryptoProvider.RandomBytes(16);
        var credential = _configuration.Credentials.Certificate.Encode();

        var wire = PairingMessages.EncodeMessage1(nonceA, ephemeral.PublicKey,
            PairingMessages.CredentialTypeCertificate, credential);
        _th1 = PairingKeySchedule.Th1(wire);
        _state = State.AwaitMessage2;
        return wire;
    }

    /// <summary>
    /// Validates message 2 (peer certificate, signature and key confirmation), derives the SCBK,
    /// and produces message 3.
    /// </summary>
    internal byte[] ProcessMessage2(byte[] message2Wire)
    {
        Expect(State.AwaitMessage2);

        try
        {
            var message2 = PairingMessages.ParseMessage2(message2Wire);
            _peerCertificate = PairingPeer.ResolveAndValidate(_configuration, message2.CredentialType,
                message2.Credential);

            var th2 = PairingKeySchedule.Th2(_th1, message2.CoreBytes);
            if (!PairingCryptoProvider.MLDsaVerify(_peerCertificate.PublicKey,
                    PairingKeySchedule.SignedMessage2(th2), message2.SignatureP))
            {
                throw new PairingException(PairingStatus.PeerCertificateRejected,
                    "PD signature over the pairing transcript did not verify.");
            }

            var sharedSecret = PairingCryptoProvider.MLKemDecapsulate(_ephemeralPrivateKey, message2.Ciphertext);
            var confirmationKeys = PairingKeySchedule.DeriveConfirmationKeys(sharedSecret, th2);

            var expectedMacP = PairingCryptoProvider.HmacSha256(confirmationKeys.Km2, th2);
            if (!ConstantTimeEquals(expectedMacP, message2.MacP))
            {
                throw new PairingException(PairingStatus.KeyConfirmationFailed,
                    "PD key-confirmation MAC did not verify (shared secret mismatch).");
            }

            var th3 = PairingKeySchedule.Th3(th2, message2.SignatureP, message2.MacP);
            var signatureA = _configuration.Credentials.Sign(PairingKeySchedule.SignedMessage3(th3));
            var macA = PairingCryptoProvider.HmacSha256(confirmationKeys.Km3, th3);

            _th4 = PairingKeySchedule.Th4(th3, signatureA, macA);
            _confirmationKm4 = confirmationKeys.Km4;
            _scbk = PairingKeySchedule.DeriveScbk(sharedSecret, _th4);

            _state = State.AwaitResult;
            return PairingMessages.EncodeMessage3(signatureA, macA);
        }
        catch (Exception ex) when (ex is not PairingException)
        {
            _state = State.Failed;
            throw new PairingException(PairingStatus.ProtocolError, "Failed to process pairing message 2.", ex);
        }
        catch (PairingException)
        {
            _state = State.Failed;
            throw;
        }
    }

    /// <summary>
    /// Verifies the final result message and, on success, returns the paired result. A non-success
    /// status or a failed key-confirmation MAC raises <see cref="PairingException"/>.
    /// </summary>
    internal PairingResult ProcessResult(byte[] resultWire)
    {
        Expect(State.AwaitResult);

        try
        {
            var result = PairingMessages.ParseResult(resultWire);
            if (result.Status != PairingStatus.Success)
            {
                throw new PairingException(result.Status,
                    $"PD rejected pairing with status {result.Status}.");
            }

            var expectedMacR = PairingCryptoProvider.HmacSha256(_confirmationKm4, _th4);
            if (!ConstantTimeEquals(expectedMacR, result.Mac))
            {
                throw new PairingException(PairingStatus.KeyConfirmationFailed,
                    "PD final key-confirmation MAC did not verify.");
            }

            _state = State.Complete;
            return new PairingResult(_scbk, _peerCertificate);
        }
        catch (Exception ex) when (ex is not PairingException)
        {
            _state = State.Failed;
            throw new PairingException(PairingStatus.ProtocolError, "Failed to process pairing result.", ex);
        }
        catch (PairingException)
        {
            _state = State.Failed;
            throw;
        }
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
