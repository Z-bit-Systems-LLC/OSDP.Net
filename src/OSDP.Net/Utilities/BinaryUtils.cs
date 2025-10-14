using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OSDP.Net.Utilities;

/// <summary>
/// Helper routines for manipulating binary representations of data that are used
/// within the OSDP.NET library and its unit tests
/// </summary>
public static class BinaryUtils
{
    /// <summary>
    /// Convert a string containing hex digits into BitArray
    /// </summary>
    /// <param name="hex">String to be converted</param>
    /// <returns>BitArray instance with most-significant bit first order</returns>
    public static BitArray HexStringToBitArray(string hex) => 
        HexStringToBitArray((IEnumerable<char>)hex);

    /// <summary>
    /// Convert a sequence of hex digit characters into BitArray
    /// </summary>
    /// <param name="hex">Character sequence to be converted</param>
    /// <returns>BitArray instance with most-significant bit first order</returns>
    public static BitArray HexStringToBitArray(IEnumerable<char> hex) => 
        new(HexToBytes(hex).Select(x => BinaryExtensions.ReverseBits(x)).ToArray());

    /// <summary>
    /// Convert a sequence of hex digit characters into their byte representation
    /// </summary>
    /// <param name="seq">character digit sequence to be converted</param>
    /// <returns>Sequence of bytes</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if an invalid character is encountered
    /// </exception>
    public static IEnumerable<byte> HexToBytes(IEnumerable<char> seq)
    {
        using var enumerator = seq.GetEnumerator();

        while (enumerator.MoveNext())
        {
            if (enumerator.Current is '-' or ' ')
            {
                if (!enumerator.MoveNext()) throw new InvalidOperationException("Not a valid hex string");
            }

            var a = enumerator.Current.HexToInt();
            if (!enumerator.MoveNext()) throw new InvalidOperationException("Not a valid hex string");
            var b = enumerator.Current.HexToInt();

            // .NET's BitArray reads array of bytes in Least Significant Bit (LSB) first.
            //  However, the way we use this thing and to make it compatible with human-
            // readable hex strings, we want to store data as MSB first.
            yield return ((byte)((a << 4) | b));
        }
    }

    /// <summary>
    /// Convert the BitArray instance to a binary string representation
    /// </summary>
    /// <param name="arr">BitArray to be converted</param>
    /// <returns>A string consisting almost entirely of 0s and 1s</returns>
    public static string BitArrayToString(BitArray arr)
    {
        var builder = new StringBuilder();
        foreach (bool bit in arr)
        {
            builder.Append(bit ? "1" : "0");
        }

        return builder.ToString();
    }
}