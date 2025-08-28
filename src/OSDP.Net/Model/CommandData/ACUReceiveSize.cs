using System;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Command data to set the maximum message size the ACU can receive.
/// </summary>
public class ACUReceiveSize : CommandData
{
    /// <inheritdoc />
    public ACUReceiveSize(ushort maximumReceiveSize)
    {
        MaximumReceiveSize = maximumReceiveSize;
    }

    /// <summary>
    /// Gets the maximum message size the ACU can receive.
    /// </summary>
    public ushort MaximumReceiveSize { get; }
    
    /// <inheritdoc />
    public override CommandType CommandType => CommandType.MaxReplySize;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;
        
    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        return Message.ConvertShortToBytes(MaximumReceiveSize);
    }

    /// <summary>Parses the message payload bytes</summary>
    /// <param name="data">Message payload as bytes</param>
    /// <returns>An instance of ACUReceivedSize representing the message payload</returns>
    public static ACUReceiveSize ParseData(ReadOnlySpan<byte> data)
    {
        return new ACUReceiveSize(Message.ConvertBytesToUnsignedShort(data));
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Max Receive Size: {MaximumReceiveSize} bytes");
        return sb.ToString();
    }
}