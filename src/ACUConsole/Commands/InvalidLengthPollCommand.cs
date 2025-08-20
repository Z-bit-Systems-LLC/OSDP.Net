using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace ACUConsole.Commands
{
    /// <summary>
    /// Change the length on a poll command
    /// </summary>
    public class InvalidLengthPollCommand : CommandData
    {
        /// <inheritdoc />
        public override byte Code => (byte)CommandType;
        
        /// <inheritdoc />
        public override CommandType CommandType => CommandType.Poll;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithNoDataSecurity;

        /// <inheritdoc />
        public override void CustomMessageUpdate(Span<byte> commandBuffer)
        {
            commandBuffer[2] = (byte)(commandBuffer[2] + 1);
        }

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            return Array.Empty<byte>();
        }
    }
}