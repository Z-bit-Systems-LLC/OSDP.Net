using System;
using System.Collections.Generic;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Command data to send a data fragment of a file to a PD.
/// </summary>
internal class FileTransferFragment : CommandData
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

    /// <summary>Parses the message payload bytes</summary>
    /// <param name="data">Message payload as bytes</param>
    /// <returns>An instance of FileTransferFragment representing the message payload</returns>
    public static FileTransferFragment ParseData(ReadOnlySpan<byte> data)
    {
        return new FileTransferFragment(data[0], MessageDataFragment.ParseData(data.Slice(1)));
    }
}