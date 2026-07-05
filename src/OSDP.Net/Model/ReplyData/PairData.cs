using System;
using System.Collections.Generic;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData;

/// <summary>
/// Reply data carrying one fragment of an asymmetric pairing response message (osdp_PAIRR, 0x8A).
/// Uses the same little-endian multi-part framing as <see cref="DataFragmentResponse"/> and is sent
/// in cleartext during pairing.
/// </summary>
public class PairData : PayloadData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PairData"/> class.
    /// </summary>
    /// <param name="wholeMessageLength">The total length of the complete response message.</param>
    /// <param name="offset">The offset of this fragment within the complete message.</param>
    /// <param name="fragment">The fragment bytes.</param>
    public PairData(ushort wholeMessageLength, ushort offset, byte[] fragment)
    {
        WholeMessageLength = wholeMessageLength;
        Offset = offset;
        Fragment = fragment;
    }

    /// <summary>Gets the total length of the complete response message.</summary>
    public ushort WholeMessageLength { get; }

    /// <summary>Gets the offset of this fragment within the complete message.</summary>
    public ushort Offset { get; }

    /// <summary>Gets the fragment bytes.</summary>
    public byte[] Fragment { get; }

    /// <inheritdoc />
    public override byte Code => (byte)ReplyType.PairData;

    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.ReplyMessageWithNoDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        var data = new List<byte>();
        data.AddRange(Message.ConvertShortToBytes(WholeMessageLength));
        data.AddRange(Message.ConvertShortToBytes(Offset));
        data.AddRange(Message.ConvertShortToBytes((ushort)Fragment.Length));
        data.AddRange(Fragment);
        return data.ToArray();
    }

    /// <summary>Parses the message payload bytes.</summary>
    /// <param name="data">Message payload as bytes.</param>
    /// <returns>A <see cref="DataFragmentResponse"/> representing the message payload.</returns>
    public static DataFragmentResponse ParseData(ReadOnlySpan<byte> data) => DataFragmentResponse.ParseData(data);
}
