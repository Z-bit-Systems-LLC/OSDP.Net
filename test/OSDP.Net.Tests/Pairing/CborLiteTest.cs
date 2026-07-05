using System;
using NUnit.Framework;
using OSDP.Net.Pairing;

namespace OSDP.Net.Tests.Pairing;

[TestFixture]
[Category("Unit")]
public class CborLiteTest
{
    [Test]
    public void WriteUInt_UsesCanonicalMinimalEncoding()
    {
        // RFC 8949 canonical unsigned-integer encodings.
        Assert.Multiple(() =>
        {
            Assert.That(new CborLite.Writer().WriteUInt(0).ToArray(), Is.EqualTo(HexToBytes("00")));
            Assert.That(new CborLite.Writer().WriteUInt(23).ToArray(), Is.EqualTo(HexToBytes("17")));
            Assert.That(new CborLite.Writer().WriteUInt(24).ToArray(), Is.EqualTo(HexToBytes("1818")));
            Assert.That(new CborLite.Writer().WriteUInt(255).ToArray(), Is.EqualTo(HexToBytes("18FF")));
            Assert.That(new CborLite.Writer().WriteUInt(256).ToArray(), Is.EqualTo(HexToBytes("190100")));
            Assert.That(new CborLite.Writer().WriteUInt(65535).ToArray(), Is.EqualTo(HexToBytes("19FFFF")));
            Assert.That(new CborLite.Writer().WriteUInt(65536).ToArray(), Is.EqualTo(HexToBytes("1A00010000")));
            Assert.That(new CborLite.Writer().WriteUInt(4294967296).ToArray(),
                Is.EqualTo(HexToBytes("1B0000000100000000")));
        });
    }

    [Test]
    public void WriteTextString_EncodesLengthAndUtf8()
    {
        var encoded = new CborLite.Writer().WriteTextString("ACME").ToArray();
        Assert.That(encoded, Is.EqualTo(HexToBytes("6441434D45")));
    }

    [Test]
    public void WriteByteString_EncodesLengthAndBytes()
    {
        var encoded = new CborLite.Writer().WriteByteString(HexToBytes("DEADBEEF")).ToArray();
        Assert.That(encoded, Is.EqualTo(HexToBytes("44DEADBEEF")));
    }

    [Test]
    public void RoundTrip_ArrayOfMixedTypes()
    {
        var serial = HexToBytes("0102030405060708");
        var encoded = new CborLite.Writer()
            .WriteArrayHeader(4)
            .WriteUInt(1)
            .WriteByteString(serial)
            .WriteTextString("issuer")
            .WriteUInt(1000)
            .ToArray();

        var reader = new CborLite.Reader(encoded);
        Assert.Multiple(() =>
        {
            Assert.That(reader.ReadArrayHeader(), Is.EqualTo(4));
            Assert.That(reader.ReadUInt(), Is.EqualTo(1));
            Assert.That(reader.ReadByteString(), Is.EqualTo(serial));
            Assert.That(reader.ReadTextString(), Is.EqualTo("issuer"));
            Assert.That(reader.ReadUInt(), Is.EqualTo(1000));
            Assert.That(reader.AtEnd, Is.True);
        });
    }

    [Test]
    public void EncodeTwice_ProducesIdenticalBytes()
    {
        byte[] Encode() => new CborLite.Writer()
            .WriteArrayHeader(3)
            .WriteTextString("manufacturer")
            .WriteUInt(70000)
            .WriteByteString(HexToBytes("AABBCC"))
            .ToArray();

        Assert.That(Encode(), Is.EqualTo(Encode()));
    }

    [Test]
    public void Reader_TypeMismatch_Throws()
    {
        var encoded = new CborLite.Writer().WriteUInt(5).ToArray();
        var reader = new CborLite.Reader(encoded);
        Assert.Throws<FormatException>(() => reader.ReadTextString());
    }

    [Test]
    public void Reader_Truncated_Throws()
    {
        // Declares a 4-byte string but supplies only 2 bytes.
        var reader = new CborLite.Reader(HexToBytes("440102"));
        Assert.Throws<FormatException>(() => reader.ReadByteString());
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
