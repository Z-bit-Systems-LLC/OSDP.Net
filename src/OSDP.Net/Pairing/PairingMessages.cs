using System;

namespace OSDP.Net.Pairing;

/// <summary>
/// Deterministic CBOR encode/parse for the four pairing messages. Each message is a one-byte
/// type tag followed by a canonical CBOR body. Credential presentation is either a full
/// certificate (<see cref="CredentialTypeCertificate"/>) or a thumbprint reference
/// (<see cref="CredentialTypeThumbprint"/>).
/// </summary>
internal static class PairingMessages
{
    internal const byte TypeMessage1 = 0x01;
    internal const byte TypeMessage2 = 0x02;
    internal const byte TypeMessage3 = 0x03;
    internal const byte TypeResult = 0x04;

    internal const int CredentialTypeCertificate = 0;
    internal const int CredentialTypeThumbprint = 1;

    internal const int ProtocolVersion = 1;
    internal const int CipherSuite = 1;

    internal sealed class Message1
    {
        internal int Version { get; init; }
        internal int Suite { get; init; }
        internal byte[] NonceA { get; init; }
        internal byte[] EphemeralPublicKey { get; init; }
        internal int CredentialType { get; init; }
        internal byte[] Credential { get; init; }
        internal byte[] WireBytes { get; init; }
    }

    internal sealed class Message2
    {
        internal byte[] CoreBytes { get; init; }
        internal byte[] NonceP { get; init; }
        internal byte[] Ciphertext { get; init; }
        internal int CredentialType { get; init; }
        internal byte[] Credential { get; init; }
        internal byte[] SignatureP { get; init; }
        internal byte[] MacP { get; init; }
    }

    internal sealed class Message3
    {
        internal byte[] SignatureA { get; init; }
        internal byte[] MacA { get; init; }
    }

    internal sealed class ResultMessage
    {
        internal PairingStatus Status { get; init; }
        internal byte[] Mac { get; init; }
    }

    internal static byte[] EncodeMessage1(byte[] nonceA, byte[] ephemeralPublicKey, int credentialType,
        byte[] credential)
    {
        var body = new CborLite.Writer()
            .WriteArrayHeader(6)
            .WriteUInt(ProtocolVersion)
            .WriteUInt(CipherSuite)
            .WriteByteString(nonceA)
            .WriteByteString(ephemeralPublicKey)
            .WriteUInt((ulong)credentialType)
            .WriteByteString(credential)
            .ToArray();
        return Prefix(TypeMessage1, body);
    }

    internal static Message1 ParseMessage1(byte[] wire)
    {
        var reader = ReaderFor(wire, TypeMessage1);
        if (reader.ReadArrayHeader() != 6)
        {
            throw new FormatException("Pairing message 1 must be a 6-element array.");
        }

        return new Message1
        {
            Version = (int)reader.ReadUInt(),
            Suite = (int)reader.ReadUInt(),
            NonceA = reader.ReadByteString(),
            EphemeralPublicKey = reader.ReadByteString(),
            CredentialType = (int)reader.ReadUInt(),
            Credential = reader.ReadByteString(),
            WireBytes = wire
        };
    }

    internal static byte[] EncodeMessage2Core(byte[] nonceP, byte[] ciphertext, int credentialType, byte[] credential)
    {
        return new CborLite.Writer()
            .WriteArrayHeader(4)
            .WriteByteString(nonceP)
            .WriteByteString(ciphertext)
            .WriteUInt((ulong)credentialType)
            .WriteByteString(credential)
            .ToArray();
    }

    internal static byte[] EncodeMessage2(byte[] coreBytes, byte[] signatureP, byte[] macP)
    {
        var body = new CborLite.Writer()
            .WriteArrayHeader(3)
            .WriteByteString(coreBytes)
            .WriteByteString(signatureP)
            .WriteByteString(macP)
            .ToArray();
        return Prefix(TypeMessage2, body);
    }

    internal static Message2 ParseMessage2(byte[] wire)
    {
        var reader = ReaderFor(wire, TypeMessage2);
        if (reader.ReadArrayHeader() != 3)
        {
            throw new FormatException("Pairing message 2 must be a 3-element array.");
        }

        var coreBytes = reader.ReadByteString();
        var signatureP = reader.ReadByteString();
        var macP = reader.ReadByteString();

        var coreReader = new CborLite.Reader(coreBytes);
        if (coreReader.ReadArrayHeader() != 4)
        {
            throw new FormatException("Pairing message 2 core must be a 4-element array.");
        }

        return new Message2
        {
            CoreBytes = coreBytes,
            NonceP = coreReader.ReadByteString(),
            Ciphertext = coreReader.ReadByteString(),
            CredentialType = (int)coreReader.ReadUInt(),
            Credential = coreReader.ReadByteString(),
            SignatureP = signatureP,
            MacP = macP
        };
    }

    internal static byte[] EncodeMessage3(byte[] signatureA, byte[] macA)
    {
        var body = new CborLite.Writer()
            .WriteArrayHeader(2)
            .WriteByteString(signatureA)
            .WriteByteString(macA)
            .ToArray();
        return Prefix(TypeMessage3, body);
    }

    internal static Message3 ParseMessage3(byte[] wire)
    {
        var reader = ReaderFor(wire, TypeMessage3);
        if (reader.ReadArrayHeader() != 2)
        {
            throw new FormatException("Pairing message 3 must be a 2-element array.");
        }

        return new Message3
        {
            SignatureA = reader.ReadByteString(),
            MacA = reader.ReadByteString()
        };
    }

    internal static byte[] EncodeResult(PairingStatus status, byte[] mac)
    {
        var body = new CborLite.Writer()
            .WriteArrayHeader(2)
            .WriteUInt((byte)status)
            .WriteByteString(mac ?? Array.Empty<byte>())
            .ToArray();
        return Prefix(TypeResult, body);
    }

    internal static ResultMessage ParseResult(byte[] wire)
    {
        var reader = ReaderFor(wire, TypeResult);
        if (reader.ReadArrayHeader() != 2)
        {
            throw new FormatException("Pairing result must be a 2-element array.");
        }

        return new ResultMessage
        {
            Status = (PairingStatus)(byte)reader.ReadUInt(),
            Mac = reader.ReadByteString()
        };
    }

    private static byte[] Prefix(byte type, byte[] body)
    {
        var wire = new byte[body.Length + 1];
        wire[0] = type;
        Buffer.BlockCopy(body, 0, wire, 1, body.Length);
        return wire;
    }

    private static CborLite.Reader ReaderFor(byte[] wire, byte expectedType)
    {
        if (wire == null || wire.Length < 1 || wire[0] != expectedType)
        {
            throw new FormatException($"Expected pairing message type 0x{expectedType:X2}.");
        }

        var body = new byte[wire.Length - 1];
        Buffer.BlockCopy(wire, 1, body, 0, body.Length);
        return new CborLite.Reader(body);
    }
}
