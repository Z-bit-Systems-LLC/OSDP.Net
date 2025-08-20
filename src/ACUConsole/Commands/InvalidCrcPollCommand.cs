using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace ACUConsole.Commands
{
    /// <summary>
    /// Change the CRC on a poll command
    /// </summary>
    public class InvalidCrcPollCommand : CommandData
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
            commandBuffer[^1] = (byte)(commandBuffer[^1] + 1);
        }

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            return Array.Empty<byte>();
        }
    }
}