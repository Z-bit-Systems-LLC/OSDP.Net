using System;
using NUnit.Framework;
using OSDP.Net.Pairing;

namespace OSDP.Net.Tests.Pairing;

[TestFixture]
[Category("Unit")]
public class PairingTrustAnchorTest
{
    private static readonly DeviceIdentity Identity = new("ACME Access", "AR-200", "SN-0001");

    [Test]
    public void CaAnchor_AcceptsCertificateFromSameCa()
    {
        var ca = CertificateAuthority.Demo();
        var credentials = PairingCredentials.Generate(Identity, ca);
        var anchor = PairingTrustAnchor.FromCa(ca);

        Assert.That(anchor.Validate(credentials.Certificate), Is.True);
    }

    [Test]
    public void CaAnchor_RejectsCertificateFromDifferentCa()
    {
        var issuingCa = CertificateAuthority.Demo();
        var credentials = PairingCredentials.Generate(Identity, issuingCa);
        var otherAnchor = PairingTrustAnchor.FromCa(CertificateAuthority.Create("OTHER-CA"));

        Assert.That(otherAnchor.Validate(credentials.Certificate), Is.False);
    }

    [Test]
    public void CaAnchor_EnforcesValidityWindowWhenNowProvided()
    {
        var ca = CertificateAuthority.Demo();
        var credentials = PairingCredentials.Generate(Identity, ca);
        var anchor = PairingTrustAnchor.FromCa(ca);

        Assert.Multiple(() =>
        {
            Assert.That(anchor.Validate(credentials.Certificate, DateTimeOffset.UtcNow), Is.True);
            Assert.That(anchor.Validate(credentials.Certificate,
                DateTimeOffset.UtcNow + TimeSpan.FromDays(4000)), Is.False, "Expired certificate must be rejected");
        });
    }

    [Test]
    public void PinnedAnchor_AcceptsPinnedSelfSignedCertificate()
    {
        var credentials = PairingCredentials.GenerateSelfSigned(Identity);
        var anchor = PairingTrustAnchor.FromPinnedThumbprints(new[] { credentials.Certificate.Thumbprint });

        Assert.That(anchor.Validate(credentials.Certificate), Is.True);
    }

    [Test]
    public void PinnedAnchor_RejectsUnpinnedCertificate()
    {
        var pinned = PairingCredentials.GenerateSelfSigned(Identity);
        var other = PairingCredentials.GenerateSelfSigned(new DeviceIdentity("X", "Y", "Z"));
        var anchor = PairingTrustAnchor.FromPinnedThumbprints(new[] { pinned.Certificate.Thumbprint });

        Assert.That(anchor.Validate(other.Certificate), Is.False);
    }

    [Test]
    public void PinnedAnchor_RejectsPinnedThumbprintWithBrokenSelfSignature()
    {
        // A CA-issued certificate is not self-signed, so a pinned anchor must reject it even if
        // its thumbprint is pinned (VerifySelfSignature fails).
        var ca = CertificateAuthority.Demo();
        var credentials = PairingCredentials.Generate(Identity, ca);
        var anchor = PairingTrustAnchor.FromPinnedThumbprints(new[] { credentials.Certificate.Thumbprint });

        Assert.That(anchor.Validate(credentials.Certificate), Is.False);
    }

    [Test]
    public void Validate_NullCertificate_ReturnsFalse()
    {
        var anchor = PairingTrustAnchor.FromCa(CertificateAuthority.Demo());
        Assert.That(anchor.Validate(null), Is.False);
    }
}
