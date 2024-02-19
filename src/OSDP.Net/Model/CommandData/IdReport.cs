﻿using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

internal class IdReport : CommandData
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
        return new byte[] {0x00};
    }
}