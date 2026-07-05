using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Command data carrying one fragment of an asymmetric pairing message (osdp_PAIR, 0xB0).
/// Pairing runs in cleartext before any secure channel exists, so the fragment uses a
/// no-data-security control block, mirroring an ordinary unsecured command.
/// </summary>
public class PairFragment : CommandData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PairFragment"/> class.
    /// </summary>
    /// <param name="fragment">The message data fragment.</param>
    public PairFragment(MessageDataFragment fragment)
    {
        Fragment = fragment;
    }

    /// <summary>Gets the message data fragment.</summary>
    public MessageDataFragment Fragment { get; }

    /// <inheritdoc />
    public override CommandType CommandType => CommandType.Pair;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;

    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithNoDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData() => Fragment.BuildData().ToArray();

    /// <summary>Parses the message payload bytes.</summary>
    /// <param name="data">Message payload as bytes.</param>
    /// <returns>An instance of <see cref="PairFragment"/> representing the message payload.</returns>
    public static PairFragment ParseData(ReadOnlySpan<byte> data) =>
        new(MessageDataFragment.ParseData(data, MessageDataFragmentFieldSize.TwoBytes));
}
