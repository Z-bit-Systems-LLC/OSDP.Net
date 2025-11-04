using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Represents a command data type for generating an ID report request.
/// </summary>
public class IdReport : CommandData
{
    /// <inheritdoc />
    public override CommandType CommandType => CommandType.IdReport;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;
    
    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        return [0x00];
    }
}