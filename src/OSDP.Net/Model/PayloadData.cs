using System;

namespace OSDP.Net.Model
{
    /// <summary>
    /// Base class representing a payload an OSDP message
    /// </summary>
    public abstract class PayloadData
    {
        /// <summary>
        /// Converts the payload into a byte array to be sent over the wire. The design
        /// decision to put the burden of adding padding on every deriving class is intentional
        /// as this method is where byte[] array originates and creating a payload that is
        /// ready to be encoded, helps us avoid a few downstream heap operations.
        /// </summary>
        /// <returns>Packed reply as array of raw bytes. Note that some types of replies,
        /// like osdp_ACK do not have additional data, in which case it is perfectly
        /// acceptable for this array to be 0 length</returns>
        public abstract byte[] BuildData();
        
        /// <summary>
        /// Gets the command or reply code byte value that represent the type of data
        /// </summary>
        public abstract byte Code { get; }
        
        /// <summary>
        /// Gets a value indicating whether the security initialization is performed by this payload.
        /// </summary>
        /// <value>
        /// <c>true</c> if the security initialization is performed; otherwise, <c>false</c>.
        /// </value>
        public virtual bool IsSecurityInitialization => false;

        /// <summary>
        /// Retrieves the security control block for a specific command data type.
        /// </summary>
        /// <returns>The security control block for the command data type.</returns>
        /// <remarks>
        /// This method is used to retrieve the security control block for a specific command data type. Each command data type
        /// has a specific security control block associated with it. The security control block is a collection of bytes that
        /// contains security-related information for the command data type.
        /// </remarks>
        public abstract ReadOnlySpan<byte> SecurityControlBlock();

        /// <summary>
        /// Updates the message data before sending in the specified byte buffer.
        /// </summary>
        /// <param name="messageBuffer">The byte buffer containing the custom message.</param>
        public virtual void CustomMessageUpdate(Span<byte> messageBuffer)
        {
        }

    }
}
