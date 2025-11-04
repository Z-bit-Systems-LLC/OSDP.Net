using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Represents the capabilities of a device within the context of OSDP (Open Supervised Device Protocol).
/// </summary>
public class DeviceCapabilities : CommandData
{
    /// <inheritdoc />
    public override CommandType CommandType => CommandType.DeviceCapabilities;

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