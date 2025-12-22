using OSDP.Net.Messages;
using OSDP.Net.Model.ReplyData;
using System;

namespace OSDP.Net
{
    /// <summary>
    /// Represents a custom exception defined in OSDP.Net library
    /// </summary>
    public class OSDPNetException : Exception
    {
        /// <summary>
        /// Initializes a new instance of OSDP.Net.OSDPNetException with a specified
        /// error message
        /// </summary>
        /// <param name="message">Message that describes the error</param>
        public OSDPNetException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of OSDP.Net.OSDPNetException
        /// </summary>
        public OSDPNetException()
        {
        }
    }

    /// <summary>
    /// Represent a failure condition where PD indicates that it didn't accept the 
    /// command by replying with osdp_NAK packet
    /// </summary>
    public sealed class NackReplyException : OSDPNetException
    {
        /// <summary>
        /// Parsed osdp_NAK packet data
        /// </summary>
        public Nak Reply { get; }

        /// <summary>
        /// Initializes a new instance of OSDP.Net.NackReplyException class
        /// </summary>
        /// <param name="replyData">osdp_NAK packet data returned from PD</param>
        /// <param name="message">Optional message to be included with the exception</param>
        public NackReplyException(Nak replyData, string message = null) : base(message)
        {
            Message =
                $"Received NAK error '{Helpers.SplitCamelCase(replyData.ErrorCode.ToString())}'.{(string.IsNullOrEmpty(message) ? string.Empty : $" {message}")}";
            Reply = replyData;
        }

        /// <inheritdoc />
        public override string Message { get; }
    }

    /// <summary>
    /// Gets thrown whenever OSDP.NET is unable to parse payload bytes
    /// </summary>
    public class InvalidPayloadException : OSDPNetException
    {
        /// <summary>
        /// Initializes a new instance of OSDP.Net.InvalidPayloadException with a specified
        /// error message
        /// </summary>
        /// <param name="message">Optional message to be included with the exception</param>
        public InvalidPayloadException(string message) : base(message) { }
    }

    /// <summary>
    /// Exception that gets thrown if the attempted operation expects channel security
    /// to be established
    /// </summary>
    public class SecureChannelRequired : OSDPNetException
    {
        /// <summary>
        /// Initializes a new instance of OSDP.Net.SecureChannelRequired
        /// </summary>
        public SecureChannelRequired()
        {
        }

        /// <summary>
        /// Initializes a new instance of OSDP.Net.SecureChannelRequired with a specified
        /// error message
        /// </summary>
        /// <param name="message">Message that describes the error</param>
        public SecureChannelRequired(string message) : base(message)
        {
        }
    }
}
