using System;
using NUnit.Framework;
using OSDP.Net.Pairing;

namespace OSDP.Net.Tests.Pairing;

[TestFixture]
[Category("Unit")]
public class PairingKeyScheduleTest
{
    // Fixed inputs: ss = 0x00..0x1F, TH2 = 0x20..0x3F, TH4 = 0x40..0x5F.
    // Expected outputs were computed independently with the .NET built-in HKDF (RFC 5869).
    private static readonly byte[] SharedSecret = Range(0x00);
    private static readonly byte[] Th2 = Range(0x20);
    private static readonly byte[] Th4 = Range(0x40);

    [Test]
    public void DeriveConfirmationKeys_MatchesReferenceVectors()
    {
        var keys = PairingKeySchedule.DeriveConfirmationKeys(SharedSecret, Th2);

        Assert.Multiple(() =>
        {
            Assert.That(keys.Km2, Is.EqualTo(HexToBytes(
                "94151F36DE9FEB1CC8C74D7D846FBE5EA7C5CA7FC18979623D94C890ECEAD7AB")));
            Assert.That(keys.Km3, Is.EqualTo(HexToBytes(
                "BA43E76D8870ED58D77636D397D7D722513E879026A3021F6FDD07C023384829")));
            Assert.That(keys.Km4, Is.EqualTo(HexToBytes(
                "E542E59444C0776CE69DEA4FABC862F2ABD6782A3B7D7297F7E5F418D5DDF87A")));
        });
    }

    [Test]
    public void DeriveScbk_MatchesReferenceVector()
    {
        var scbk = PairingKeySchedule.DeriveScbk(SharedSecret, Th4);
        Assert.That(scbk, Is.EqualTo(HexToBytes(
            "8EAF7FD9DE1332FD2F3F18378B8AFB81E90E83238BA324CB7BDC3F38146835D4")));
    }

    [Test]
    public void TranscriptHashes_ChainDeterministically()
    {
        var th1 = PairingKeySchedule.Th1(new byte[] { 0x01, 0x02, 0x03 });
        var th2 = PairingKeySchedule.Th2(th1, new byte[] { 0x04 });
        var th2Again = PairingKeySchedule.Th2(th1, new byte[] { 0x04 });

        Assert.Multiple(() =>
        {
            Assert.That(th1.Length, Is.EqualTo(32));
            Assert.That(th2, Is.EqualTo(th2Again));
            Assert.That(th2, Is.Not.EqualTo(th1));
        });
    }

    private static byte[] Range(int start)
    {
        var bytes = new byte[32];
        for (var i = 0; i < 32; i++)
        {
            bytes[i] = (byte)(start + i);
        }

        return bytes;
    }

    private static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }
}
