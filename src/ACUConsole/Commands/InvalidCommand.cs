using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.CommandData;

namespace ACUConsole.Commands
{
    /// <summary>
    /// 
    /// </summary>
    public class InvalidCommand : CommandData
    {
        /// <inheritdoc />
        public override byte Code => 0x59;
        
        /// <inheritdoc />
        public override CommandType CommandType => CommandType.Poll;

        /// <inheritdoc />
        public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithNoDataSecurity;

        /// <inheritdoc />
        public override byte[] BuildData()
        {
            return Array.Empty<byte>();
        }
    }
}