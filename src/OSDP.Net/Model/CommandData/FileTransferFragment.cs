using System;
using System.Collections.Generic;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Command data to send a data fragment of a file to a PD.
/// </summary>
/// <remarks>
/// Sent by the ACU as osdp_FILETRANSFER (0x7C) and received by a PD, which answers with an
/// <see cref="Model.ReplyData.FileTransferStatus"/>. See OSDP v2.2.2 subclause 6.26.
/// </remarks>
public class FileTransferFragment : CommandData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransferFragment"/> class.
    /// </summary>
    /// <param name="type">File transfer type</param>
    /// <param name="fragment">Message data fragment</param>
    public FileTransferFragment(byte type, MessageDataFragment fragment)
    {
        Type = type;
        Fragment = fragment;
    }

    /// <summary>
    /// Get the file transfer type
    /// </summary>
    public byte Type { get; }

    /// <summary>
    /// Get the message data fragment
    /// </summary>
    public MessageDataFragment Fragment { get; }

    /// <inheritdoc />
    public override CommandType CommandType => CommandType.FileTransfer;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;

    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        var data = new List<byte> {Type};
        data.AddRange(Fragment.BuildData().ToArray());
        return data.ToArray();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var build = new StringBuilder();
        build.AppendLine($"     Transfer Type: 0x{Type:X2} ({DescribeType(Type)})");
        build.AppendLine($"        Total Size: {Fragment.TotalSize}");
        build.AppendLine($"            Offset: {Fragment.Offset}");
        build.AppendLine($"     Fragment Size: {Fragment.FragmentSize}");
        return build.ToString();
    }

    /// <summary>
    /// Describes a file transfer type code as defined by OSDP v2.2.2 Table 34.
    /// </summary>
    private static string DescribeType(byte type) => type switch
    {
        0x01 => "Opaque file contents",
        0x02 => "Template for the next osdp_BIOMATCH",
        0x03 => "PD-specific opaque data for display",
        <= 0x7F => "Reserved for future use",
        _ => "Reserved for private use"
    };

    /// <summary>Parses the message payload bytes</summary>
    /// <param name="data">Message payload as bytes</param>
    /// <returns>An instance of FileTransferFragment representing the message payload</returns>
    public static FileTransferFragment ParseData(ReadOnlySpan<byte> data)
    {
        return new FileTransferFragment(data[0], MessageDataFragment.ParseData(data.Slice(1), MessageDataFragmentFieldSize.FourBytes));
    }
}