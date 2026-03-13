using Microsoft.Extensions.Logging;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// SC2 secure channel implementation for the Access Control Unit (ACU) side.
/// Commands are encrypted and sent; replies are received and decrypted.
/// </summary>
internal class SC2ACUMessageSecureChannel : SC2MessageSecureChannel
{
    /// <summary>
    /// Initializes a new SC2 ACU message secure channel.
    /// </summary>
    /// <param name="context">The SC2 security context.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public SC2ACUMessageSecureChannel(SC2SecurityContext context, ILoggerFactory loggerFactory = null)
        : base(context, loggerFactory)
    {
    }
}
