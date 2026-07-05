using System;
using NUnit.Framework;
using OSDP.Net.Pairing;

namespace OSDP.Net.Tests.Pairing;

[TestFixture]
[Category("Unit")]
public class PairingSessionTest
{
    private static readonly DeviceIdentity AcuIdentity = new("ACME Controllers", "ACU-9", "ACU-0001");
    private static readonly DeviceIdentity PdIdentity = new("ACME Access", "AR-200", "PD-0001");

    private sealed class Harness
    {
        internal AcuPairingSession Acu { get; init; }
        internal PdPairingSession Pd { get; init; }
    }

    private static Harness CreateHarness(Action<PairingConfiguration> configureAcu = null,
        Action<PairingConfiguration> configurePd = null, CertificateAuthority acuIssuer = null,
        CertificateAuthority pdIssuer = null, PairingTrustAnchor acuAnchor = null,
        PairingTrustAnchor pdAnchor = null)
    {
        var demoCa = CertificateAuthority.Demo();
        acuIssuer ??= demoCa;
        pdIssuer ??= demoCa;

        var acuCredentials = PairingCredentials.Generate(AcuIdentity, acuIssuer, Seed(0x30));
        var pdCredentials = PairingCredentials.Generate(PdIdentity, pdIssuer, Seed(0x60));

        var acuConfig = new PairingConfiguration(acuCredentials, pdAnchor ?? PairingTrustAnchor.FromCa(demoCa));
        var pdConfig = new PairingConfiguration(pdCredentials, acuAnchor ?? PairingTrustAnchor.FromCa(demoCa));
        configureAcu?.Invoke(acuConfig);
        configurePd?.Invoke(pdConfig);

        return new Harness
        {
            Acu = new AcuPairingSession(acuConfig, Seed64(0x01), Nonce(0xA0)),
            Pd = new PdPairingSession(pdConfig, Nonce(0xB0))
        };
    }

    [Test]
    public void FullExchange_BothSidesDeriveIdenticalScbk()
    {
        var harness = CreateHarness();

        var message1 = harness.Acu.CreateMessage1();
        var message2 = harness.Pd.ProcessMessage1(message1);
        var message3 = harness.Acu.ProcessMessage2(message2);
        var outcome = harness.Pd.ProcessMessage3(message3);

        Assert.That(outcome.Success, Is.True);

        var result = harness.Pd.BuildResult(PairingStatus.Success);
        var pairingResult = harness.Acu.ProcessResult(result);

        Assert.Multiple(() =>
        {
            Assert.That(pairingResult.Scbk.Length, Is.EqualTo(32));
            Assert.That(pairingResult.Scbk, Is.EqualTo(outcome.Scbk), "ACU and PD must derive the same SCBK");
            Assert.That(pairingResult.PeerIdentity.SerialNumber, Is.EqualTo(PdIdentity.SerialNumber));
        });
    }

    [Test]
    public void FullExchange_WithPinnedSelfSignedCertificates_Succeeds()
    {
        var acuCredentials = PairingCredentials.GenerateSelfSigned(AcuIdentity, Seed(0x30));
        var pdCredentials = PairingCredentials.GenerateSelfSigned(PdIdentity, Seed(0x60));

        var acuConfig = new PairingConfiguration(acuCredentials,
            PairingTrustAnchor.FromPinnedThumbprints(new[] { pdCredentials.Certificate.Thumbprint }));
        var pdConfig = new PairingConfiguration(pdCredentials,
            PairingTrustAnchor.FromPinnedThumbprints(new[] { acuCredentials.Certificate.Thumbprint }));

        var acu = new AcuPairingSession(acuConfig);
        var pd = new PdPairingSession(pdConfig);

        var message3 = acu.ProcessMessage2(pd.ProcessMessage1(acu.CreateMessage1()));
        var outcome = pd.ProcessMessage3(message3);
        var pairingResult = acu.ProcessResult(pd.BuildResult(PairingStatus.Success));

        Assert.That(pairingResult.Scbk, Is.EqualTo(outcome.Scbk));
    }

    [Test]
    public void AcuRejectsPd_WhenPdCertFromUntrustedCa()
    {
        var untrustedPdCa = CertificateAuthority.Create("ROGUE-CA");
        var harness = CreateHarness(pdIssuer: untrustedPdCa);

        var message2 = harness.Pd.ProcessMessage1(harness.Acu.CreateMessage1());

        var ex = Assert.Throws<PairingException>(() => harness.Acu.ProcessMessage2(message2));
        Assert.That(ex.Status, Is.EqualTo(PairingStatus.PeerCertificateRejected));
    }

    [Test]
    public void PdRejectsAcu_WhenAcuCertFromUntrustedCa()
    {
        var untrustedAcuCa = CertificateAuthority.Create("ROGUE-CA");
        var harness = CreateHarness(acuIssuer: untrustedAcuCa);

        var message1 = harness.Acu.CreateMessage1();

        var ex = Assert.Throws<PairingException>(() => harness.Pd.ProcessMessage1(message1));
        Assert.That(ex.Status, Is.EqualTo(PairingStatus.PeerCertificateRejected));
    }

    [Test]
    public void AcuRejectsTamperedMessage2()
    {
        var harness = CreateHarness();
        var message2 = harness.Pd.ProcessMessage1(harness.Acu.CreateMessage1());
        message2[message2.Length / 2] ^= 0xFF;

        Assert.Throws<PairingException>(() => harness.Acu.ProcessMessage2(message2));
    }

    [Test]
    public void PdReturnsAuthFailure_WhenMessage3SignatureTampered()
    {
        var harness = CreateHarness();
        var message3 = harness.Acu.ProcessMessage2(harness.Pd.ProcessMessage1(harness.Acu.CreateMessage1()));
        message3[message3.Length / 2] ^= 0xFF;

        var outcome = harness.Pd.ProcessMessage3(message3);
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Success, Is.False);
            var result = PairingMessages.ParseResult(outcome.FailureResult);
            Assert.That(result.Status, Is.EqualTo(PairingStatus.AuthenticationFailed));
        });
    }

    [Test]
    public void AcuThrows_WhenResultReportsPersistenceFailure()
    {
        var harness = CreateHarness();
        var message3 = harness.Acu.ProcessMessage2(harness.Pd.ProcessMessage1(harness.Acu.CreateMessage1()));
        harness.Pd.ProcessMessage3(message3);

        var failureResult = harness.Pd.BuildResult(PairingStatus.PersistenceFailed);
        var ex = Assert.Throws<PairingException>(() => harness.Acu.ProcessResult(failureResult));
        Assert.That(ex.Status, Is.EqualTo(PairingStatus.PersistenceFailed));
    }

    [Test]
    public void PdRejects_WhenApprovePeerReturnsFalse()
    {
        var harness = CreateHarness(configurePd: config => config.ApprovePeer = _ => false);
        var ex = Assert.Throws<PairingException>(() => harness.Pd.ProcessMessage1(harness.Acu.CreateMessage1()));
        Assert.That(ex.Status, Is.EqualTo(PairingStatus.PolicyRejected));
    }

    [Test]
    public void PdRejects_WhenRePairingDeniedAndKeyProvisioned()
    {
        var harness = CreateHarness(configurePd: config =>
        {
            config.RePairingPolicy = RePairingPolicy.Deny;
            config.ScbkIsProvisioned = () => true;
        });

        var ex = Assert.Throws<PairingException>(() => harness.Pd.ProcessMessage1(harness.Acu.CreateMessage1()));
        Assert.That(ex.Status, Is.EqualTo(PairingStatus.PolicyRejected));
    }

    [Test]
    public void OutOfOrderCall_Throws()
    {
        var harness = CreateHarness();
        var ex = Assert.Throws<PairingException>(() => harness.Acu.ProcessResult(new byte[] { 0x04, 0x82, 0x00, 0x40 }));
        Assert.That(ex.Status, Is.EqualTo(PairingStatus.ProtocolError));
    }

    private static byte[] Seed(byte fill)
    {
        var seed = new byte[PairingCryptoProvider.MLDsaSeedSize];
        for (var i = 0; i < seed.Length; i++)
        {
            seed[i] = (byte)(fill + i);
        }

        return seed;
    }

    private static byte[] Seed64(byte fill)
    {
        var seed = new byte[PairingCryptoProvider.MLKemSeedSize];
        for (var i = 0; i < seed.Length; i++)
        {
            seed[i] = (byte)(fill + i);
        }

        return seed;
    }

    private static byte[] Nonce(byte fill)
    {
        var nonce = new byte[16];
        for (var i = 0; i < nonce.Length; i++)
        {
            nonce[i] = (byte)(fill + i);
        }

        return nonce;
    }
}
