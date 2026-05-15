using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Command data to set the keep alive time.
/// </summary>
public class KeepReaderActive : CommandData
{
    /// <inheritdoc />
    public KeepReaderActive(ushort keepAliveTimeInMilliseconds)
    {
        KeepAliveTimeInMilliseconds = keepAliveTimeInMilliseconds;
    }

    /// <summary>
    /// Gets the keep alive time in milliseconds.
    /// </summary>
    /// <remarks>
    /// The keep alive time is used to determine how long a connection can remain idle before it is closed.
    /// Once the keep alive time elapses without any activity, the connection will be closed.
    /// </remarks>
    public ushort KeepAliveTimeInMilliseconds { get; }
    
    /// <inheritdoc />
    public override CommandType CommandType => CommandType.KeepActive;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;
        
    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        return Message.ConvertShortToBytes(KeepAliveTimeInMilliseconds);
    }

    /// <summary>Parses the message payload bytes</summary>
    /// <param name="data">Message payload as bytes</param>
    /// <returns>An instance of KeepReaderActive representing the message payload</returns>
    public static KeepReaderActive ParseData(ReadOnlySpan<byte> data)
    {
        return new KeepReaderActive(Message.ConvertBytesToUnsignedShort(data));
    }
}