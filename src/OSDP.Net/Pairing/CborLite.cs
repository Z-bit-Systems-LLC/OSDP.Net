using System;
using System.Collections.Generic;
using System.Text;

namespace OSDP.Net.Pairing;

/// <summary>
/// A minimal, deterministic CBOR (RFC 8949) writer and reader supporting the small,
/// fixed-shape structures used by the OSDP asymmetric pairing profile: unsigned
/// integers, byte strings, text strings, and definite-length arrays.
/// </summary>
/// <remarks>
/// The writer always emits the shortest ("canonical") length encoding, so a given
/// logical value has exactly one byte representation. Determinism is required because
/// certificate signatures and thumbprints are computed over the encoded bytes.
/// This is intentionally not a general-purpose CBOR library; it implements only what
/// the pairing profile needs and can be replaced by <c>System.Formats.Cbor</c> if a
/// dependency is preferred.
/// </remarks>
internal static class CborLite
{
    private const int MajorUnsigned = 0;
    private const int MajorByteString = 2;
    private const int MajorTextString = 3;
    private const int MajorArray = 4;

    /// <summary>
    /// Builds a deterministic CBOR byte stream.
    /// </summary>
    internal sealed class Writer
    {
        private readonly List<byte> _buffer = new();

        internal Writer WriteUInt(ulong value)
        {
            WriteTypeAndLength(MajorUnsigned, value);
            return this;
        }

        internal Writer WriteByteString(ReadOnlySpan<byte> value)
        {
            WriteTypeAndLength(MajorByteString, (ulong)value.Length);
            foreach (var b in value)
            {
                _buffer.Add(b);
            }

            return this;
        }

        internal Writer WriteTextString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteTypeAndLength(MajorTextString, (ulong)bytes.Length);
            _buffer.AddRange(bytes);
            return this;
        }

        internal Writer WriteArrayHeader(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            WriteTypeAndLength(MajorArray, (ulong)count);
            return this;
        }

        internal byte[] ToArray() => _buffer.ToArray();

        private void WriteTypeAndLength(int majorType, ulong length)
        {
            var prefix = majorType << 5;
            if (length < 24)
            {
                _buffer.Add((byte)(prefix | (int)length));
            }
            else if (length < 0x100)
            {
                _buffer.Add((byte)(prefix | 24));
                _buffer.Add((byte)length);
            }
            else if (length < 0x10000)
            {
                _buffer.Add((byte)(prefix | 25));
                _buffer.Add((byte)(length >> 8));
                _buffer.Add((byte)length);
            }
            else if (length < 0x100000000)
            {
                _buffer.Add((byte)(prefix | 26));
                _buffer.Add((byte)(length >> 24));
                _buffer.Add((byte)(length >> 16));
                _buffer.Add((byte)(length >> 8));
                _buffer.Add((byte)length);
            }
            else
            {
                _buffer.Add((byte)(prefix | 27));
                for (var shift = 56; shift >= 0; shift -= 8)
                {
                    _buffer.Add((byte)(length >> shift));
                }
            }
        }
    }

    /// <summary>
    /// Reads values from a CBOR byte stream in the order written. Type mismatches and
    /// truncated input raise <see cref="FormatException"/>.
    /// </summary>
    internal sealed class Reader
    {
        private readonly byte[] _data;
        private int _position;

        internal Reader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        internal int Position => _position;

        internal bool AtEnd => _position >= _data.Length;

        internal ulong ReadUInt() => ReadTypeAndLength(MajorUnsigned);

        internal byte[] ReadByteString()
        {
            var length = checked((int)ReadTypeAndLength(MajorByteString));
            EnsureAvailable(length);
            var result = new byte[length];
            Buffer.BlockCopy(_data, _position, result, 0, length);
            _position += length;
            return result;
        }

        internal string ReadTextString()
        {
            var length = checked((int)ReadTypeAndLength(MajorTextString));
            EnsureAvailable(length);
            var result = Encoding.UTF8.GetString(_data, _position, length);
            _position += length;
            return result;
        }

        internal int ReadArrayHeader() => checked((int)ReadTypeAndLength(MajorArray));

        private ulong ReadTypeAndLength(int expectedMajor)
        {
            EnsureAvailable(1);
            var initial = _data[_position++];
            var major = initial >> 5;
            if (major != expectedMajor)
            {
                throw new FormatException(
                    $"CBOR type mismatch at offset {_position - 1}: expected major type {expectedMajor}, found {major}.");
            }

            var additional = initial & 0x1F;
            switch (additional)
            {
                case < 24:
                    return (ulong)additional;
                case 24:
                    EnsureAvailable(1);
                    return _data[_position++];
                case 25:
                    return ReadBigEndian(2);
                case 26:
                    return ReadBigEndian(4);
                case 27:
                    return ReadBigEndian(8);
                default:
                    throw new FormatException(
                        $"CBOR indefinite or reserved length not supported at offset {_position - 1}.");
            }
        }

        private ulong ReadBigEndian(int byteCount)
        {
            EnsureAvailable(byteCount);
            ulong value = 0;
            for (var i = 0; i < byteCount; i++)
            {
                value = (value << 8) | _data[_position++];
            }

            return value;
        }

        private void EnsureAvailable(int count)
        {
            if (_position + count > _data.Length)
            {
                throw new FormatException("Unexpected end of CBOR data.");
            }
        }
    }
}
