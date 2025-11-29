using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Represents a command data type for generating an ID report request.
/// </summary>
public class IdReport : CommandData
{
    /// <summary>
    /// Creates a new instance of IdReport requesting the standard PD ID block.
    /// </summary>
    public IdReport() : this(false)
    {
    }

    /// <summary>
    /// Creates a new instance of IdReport.
    /// </summary>
    /// <param name="requestExtended">
    /// If true, requests the extended PD ID block (osdp_EXT_PDID reply).
    /// If false, requests the standard PD ID block (osdp_PDID reply).
    /// </param>
    public IdReport(bool requestExtended)
    {
        RequestExtended = requestExtended;
    }

    /// <summary>
    /// Gets a value indicating whether this request is for the extended ID response.
    /// </summary>
    public bool RequestExtended { get; }

    /// <inheritdoc />
    public override CommandType CommandType => CommandType.IdReport;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;

    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        return [RequestExtended ? (byte)0x01 : (byte)0x00];
    }
}