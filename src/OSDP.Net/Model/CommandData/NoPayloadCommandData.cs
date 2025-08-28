using System;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Represents a command data object that doesn't have any payload data.
/// </summary>
internal class NoPayloadCommandData : CommandData
{
    private readonly CommandType[] _validCommandTypes =
    {
        CommandType.Abort,
        CommandType.InputStatus,
        CommandType.LocalStatus,
        CommandType.OutputStatus,
        CommandType.Poll,
        CommandType.ReaderStatus
    };

    public NoPayloadCommandData(CommandType commandType)
    {
        if (!_validCommandTypes.Contains(commandType))
        {
            throw new ArgumentException("Invalid command type for a sending a payload with no data.");
        }

        CommandType = commandType;
    }

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;

    /// <inheritdoc />
    public override CommandType CommandType { get; }

    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithNoDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        return Array.Empty<byte>();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Command Type: {CommandType}");
        sb.AppendLine($"     Payload: (none)");
        return sb.ToString();
    }
}