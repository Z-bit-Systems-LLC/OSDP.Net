using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.ReplyData
{
    /// <summary>
    /// A negative reply.
    /// </summary>
    public class Nak : PayloadData
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="Nak"/> class from being created.
        /// </summary>
        private Nak()
        {
        }

        /// <summary>
        /// Creates a new instance of Nak class
        /// </summary>
        /// <param name="errorCode">Error code associated with NAK response</param>
        public Nak(ErrorCode errorCode)
        {
            // TODO: make this guy accept extra data, if/when we need to return that

            ErrorCode = errorCode;
            ExtraData = Array.Empty<byte>();
        }

        /// <summary>
        /// Gets the error code.
        /// </summary>
        public ErrorCode ErrorCode { get; private set; }

        /// <summary>
        /// Gets the extra data.
        /// </summary>
        public IEnumerable<byte> ExtraData { get; private set;  }
        
        /// <inheritdoc/>
        public override byte Code => (byte)ReplyType.Nak;
        
        /// <inheritdoc/>
        public override ReadOnlySpan<byte> SecurityControlBlock()
        {
            return SecurityBlock.ReplyMessageWithDataSecurity;
        }

        /// <summary>
        /// Parses the data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>A negative reply.</returns>
        /// <exception cref="System.Exception">Invalid size for the data</exception>
        internal static Nak ParseData(ReadOnlySpan<byte> data)
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < 1)
            {
                throw new Exception("Invalid size for the data");
            }

            var nak = new Nak
            {
                ErrorCode = (ErrorCode)dataArray[0],
                ExtraData = dataArray.Skip(1).Take(dataArray.Length - 1)
            };

            return nak;
        }

        /// <inheritdoc/>
        public override byte[] BuildData()
        {
            var buffer = new byte[1];
            buffer[0] = (byte)ErrorCode;
            return buffer;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var build = new StringBuilder();
            build.AppendLine($"Error: {Helpers.SplitCamelCase(ErrorCode.ToString())}");
            var extraDataArray = ExtraData.ToArray();
            build.AppendLine($" Data: {(extraDataArray.Length > 0 ? BitConverter.ToString(extraDataArray) : "(none)")}");
            return build.ToString();
        }
    }

    /// <summary>
    /// Error code
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// No error
        /// </summary>
        NoError = 0x0,
        /// <summary>
        /// Message check character(s) error (bad checksum/crc)
        /// </summary>
        BadChecksumOrCrc = 0x1,
        /// <summary>
        /// Unknown Command Code � Command not implemented by PD
        /// </summary>
        InvalidCommandLength = 0x2,
        /// <summary>
        /// Unknown command code
        /// </summary>
        UnknownCommandCode = 0x3,
        /// <summary>
        /// Unexpected sequence number detected in the header
        /// </summary>
        UnexpectedSequenceNumber = 0x4,
        /// <summary>
        /// This PD does not support the security block that was received
        /// </summary>
        DoesNotSupportSecurityBlock = 0x5,
        /// <summary>
        /// Encrypted communication is required to process this command
        /// </summary>
        CommunicationSecurityNotMet = 0x6,
        /// <summary>
        /// The Bio type is not supported
        /// </summary>
        BioTypeNotSupported = 0x7,
        /// <summary>
        /// The Bio format is not supported
        /// </summary>
        BioFormatNotSupported = 0x8,
        /// <summary>
        /// Unable to process command record
        /// </summary>
        UnableToProcessCommand = 0x9,
        /// <summary>
        /// The generic error
        /// </summary>
        GenericError = 0xFF
    }
}