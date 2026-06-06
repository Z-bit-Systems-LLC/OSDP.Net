using System;
using System.Linq;
using NUnit.Framework;
using OSDP.Net.Tracing;
using OSDP.Net.Utilities;

namespace OSDP.Net.Tests.Tracing;

[TestFixture]
[Category("Unit")]
public class OSDPPacketTextFormatterTest
{
    // Real secure-channel handshake packets captured from a PD bringing up a secure session.
    private const string Challenge = "53-00-13-00-0E-03-11-00-76-9A-CC-78-E0-E2-06-FD-87-51-ED";
    private const string CrypticData =
        "FF-53-80-2B-00-0E-03-12-00-76-00-00-00-15-CD-5B-07-00-D6-6D-5A-91-E6-00-80-6A-53-FA-00-E5-E6-E1-52-AB-D5-7A-7E-10-00-0B-4A-5F-A0-52";
    private const string ServerCryptogram =
        "53-00-1B-00-0F-03-13-00-77-2E-82-30-7D-6D-65-5D-E1-66-E7-E7-B2-B0-2E-1A-7B-94-21";
    // Legacy OSDP.Net PD: RMAC_I carried SCS_12 with data 0x01 on success.
    private const string InitialRMac =
        "FF-53-80-1B-00-0F-03-12-01-78-A9-65-42-F6-D5-4E-3C-12-97-68-DB-80-DD-2F-DA-50-4F-C2";

    // Current PD: RMAC_I carries SCS_14 with data 0x01 on success.
    private const string InitialRMacScs14 =
        "FF-53-80-1B-00-0F-03-14-01-78-A9-65-42-F6-D5-4E-3C-12-97-68-DB-80-DD-2F-DA-50-4F-C2";

    // Parses each packet in order through a single spy (so secure-channel state carries across
    // the handshake), returning the formatted text of the last packet.
    private static string Format(params string[] hexPackets)
    {
        var spy = new MessageSpy();
        var formatter = new OSDPPacketTextFormatter();
        string result = null;
        foreach (var hex in hexPackets)
        {
            var packet = spy.ParsePacket(BinaryUtils.HexToBytes(hex).ToArray());
            result = formatter.FormatPacket(packet, new DateTime(2026, 6, 5, 20, 23, 2));
        }

        return result;
    }

    [Test]
    public void FormatPacket_SessionChallenge_ShowsRndAndRequestedKey()
    {
        var text = Format(Challenge);

        Assert.That(text, Does.Contain("osdp_CHLNG (Step 1 of 4 - ACU challenge)"));
        Assert.That(text, Does.Contain("Security Block: SCS_11, SEC_BLK_DATA: 00 (Default key (SCBK-D))"));
        Assert.That(text, Does.Contain("RND.A (ACU random): 9A-CC-78-E0-E2-06-FD-87"));
    }

    [Test]
    public void FormatPacket_CrypticData_ShowsCuidRndBAndClientCryptogram()
    {
        var text = Format(CrypticData);

        Assert.That(text, Does.Contain("osdp_CCRYPT (Step 2 of 4 - PD response)"));
        Assert.That(text, Does.Contain("Security Block: SCS_12, SEC_BLK_DATA: 00 (Default key (SCBK-D))"));
        Assert.That(text, Does.Contain("cUID (PD unique ID): 00-00-00-15-CD-5B-07-00"));
        Assert.That(text, Does.Contain("RND.B (PD random): D6-6D-5A-91-E6-00-80-6A"));
        Assert.That(text, Does.Contain("Client cryptogram: 53-FA-00-E5-E6-E1-52-AB-D5-7A-7E-10-00-0B-4A-5F"));
    }

    [Test]
    public void FormatPacket_ServerCryptogram_ShowsServerCryptogram()
    {
        // SCRYPT parsing derives keys seeded by the challenge, so prime the spy with it first.
        var text = Format(Challenge, ServerCryptogram);

        Assert.That(text, Does.Contain("osdp_SCRYPT (Step 3 of 4 - ACU server cryptogram)"));
        Assert.That(text, Does.Contain("Security Block: SCS_13, SEC_BLK_DATA: 00 (Default key (SCBK-D))"));
        Assert.That(text, Does.Contain("Server cryptogram: 2E-82-30-7D-6D-65-5D-E1-66-E7-E7-B2-B0-2E-1A-7B"));
    }

    [Test]
    public void FormatPacket_InitialRMac_ShowsAcceptedAndRMac()
    {
        var text = Format(InitialRMac);

        Assert.That(text, Does.Contain("osdp_RMAC_I (Step 4 of 4 - secure channel established)"));
        Assert.That(text, Does.Contain("Security Block: SCS_12, SEC_BLK_DATA: 01 (Server cryptogram accepted)"));
        Assert.That(text, Does.Contain("Initial R-MAC: A9-65-42-F6-D5-4E-3C-12-97-68-DB-80-DD-2F-DA-50"));
    }

    [Test]
    public void FormatPacket_InitialRMac_Scs14_ShowsScs14AndAccepted()
    {
        var text = Format(InitialRMacScs14);

        Assert.That(text, Does.Contain("osdp_RMAC_I (Step 4 of 4 - secure channel established)"));
        Assert.That(text, Does.Contain("Security Block: SCS_14, SEC_BLK_DATA: 01 (Server cryptogram accepted)"));
        Assert.That(text, Does.Contain("Initial R-MAC: A9-65-42-F6-D5-4E-3C-12-97-68-DB-80-DD-2F-DA-50"));
    }
}
