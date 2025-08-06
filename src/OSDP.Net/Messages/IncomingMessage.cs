
using System;
using System.Collections.Generic;
using System.Linq;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Messages
{
    /// <summary>
    /// Represents a message that was received from the wire. It extends base Message
    /// class with extra properties/methods that specifically indicate the parsing and
    /// validation of incoming raw bytes.
    /// </summary>
    internal class IncomingMessage : Message
    {
        private const ushort MessageHeaderSize = 6;
        private readonly byte[] _originalMessage;

        /// <summary>
        /// Creates a new instance of IncomingMessage class
        /// </summary>
        /// <param name="data">Raw byte data received from the wire</param>
        /// <param name="channel">Message channel context</param>
        internal IncomingMessage(ReadOnlySpan<byte> data, IMessageSecureChannel channel)
        {
            IsUsingDefaultKey = channel.IsUsingDefaultKey;
            
            // TODO: way too much copying in this code, simplify it.
            _originalMessage = data.ToArray();

            Address = (byte)(data[1] & AddressMask);
            MessageType = data[1] < 0x80 ? MessageType.Command : MessageType.Reply;
            Sequence = (byte)(data[4] & 0x03);
            IsUsingCrc = Convert.ToBoolean(data[4] & 0x04);
            ushort replyMessageFooterSize = (ushort)(IsUsingCrc ? 2 : 1);
            bool isSecureControlBlockPresent = Convert.ToBoolean(data[4] & 0x08);
            byte secureBlockSize = (byte)(isSecureControlBlockPresent ? data[5] : 0);
            SecurityBlockType = (byte)(isSecureControlBlockPresent ? data[6] : 0);
            int messageLength = data.Length - (IsUsingCrc ? 6 : 5);
            if (isSecureControlBlockPresent)
            {
                SecureBlockData = data.Slice(MessageHeaderSize + 1, secureBlockSize - 2).ToArray();
                Mac = data.Slice(messageLength, MacSize).ToArray();
            }

            Type = data[MsgTypeIndex + secureBlockSize];

            Payload = data.Slice(MessageHeaderSize + secureBlockSize, data.Length -
                MessageHeaderSize - secureBlockSize - replyMessageFooterSize -
                (IsSecureMessage ? MacSize : 0)).ToArray();
            if (Payload.Length > 0 && HasSecureData)
            {
                var paddedPayload = channel.DecodePayload(Payload);
                var lastByteIdx = Payload.Length;
                while (lastByteIdx > 0 && paddedPayload[--lastByteIdx] != FirstPaddingByte)
                {
                }
                
                if (lastByteIdx == 0) throw new Exception("The encrypted payload is missing a padding byte");

                Payload = paddedPayload.AsSpan().Slice(0, lastByteIdx).ToArray();
            }

            IsDataCorrect = IsUsingCrc
                ? CalculateCrc(data.Slice(0, data.Length - 2)) ==
                  ConvertBytesToUnsignedShort(data.Slice(data.Length - 2, 2))
                : CalculateChecksum(data.Slice(0, data.Length - 1).ToArray()) == data[data.Length - 1];

            if (IsSecureMessage)
            {
                if (channel.IsSecurityEstablished)
                {
                    var mac = channel.GenerateMac(data.Slice(0, messageLength).ToArray(), true);
                    IsValidMac = !IsSecureMessage || mac.Slice(0, MacSize).SequenceEqual(Mac?.ToArray());
                }
                else
                {
                    IsValidMac = false;
                }
            }
            else
            {
                IsValidMac = true;
            }
        }

        /// <summary>
        /// If true, the message has a CRC suffix; otherwise it has a checksum
        /// </summary>
        public bool IsUsingCrc { get; }

        /// <summary>
        /// Command/reply code of the message
        /// </summary>
        public byte Type { get; }

        /// <summary>
        /// Message sequence number
        /// </summary>
        public byte Sequence { get; }

        internal Control ControlBlock => new(Sequence, IsUsingCrc, IsSecureMessage);

        /// <summary>
        /// Indicates if the message was sent via an established secure channel
        /// </summary>
        public bool IsSecureMessage => SecureSessionMessages.Contains(SecurityBlockType) && IsDataSecure;

        /// <summary>
        /// Indicates if the message has a valid MAC signature which was validated via
        /// local message channel context
        /// </summary>
        public bool IsValidMac { get; }

        /// <summary>
        /// Gets whether the incoming message is using the default key.
        /// </summary>
        /// <remarks>
        /// The IsUsingDefaultKey property is a boolean value that indicates whether the incoming message is using the default key.
        /// This property is true if the incoming message is using the default key; otherwise, false.
        /// The default key is a pre-configured key that is used for cryptographic operations.
        /// </remarks>
        public bool IsUsingDefaultKey { get; }

        /// <summary>
        /// Determines whether the incoming message has secure data.
        /// </summary>
        /// <remarks>
        /// The HasSecureData property is used to check if the incoming message has secure data.
        /// </remarks>
        /// <value>
        /// <c>true</c> if the incoming message has secure data; otherwise, <c>false</c>.
        /// </value>
        public bool HasSecureData =>
            SecurityBlockType == (byte)SecureChannel.SecurityBlockType.CommandMessageWithDataSecurity ||
            SecurityBlockType == (byte)SecureChannel.SecurityBlockType.ReplyMessageWithDataSecurity;

        /// <summary>
        /// Returns the raw message payload
        /// </summary>
        public byte[] Payload { get; }

        /// <summary>
        /// Original byte data which includes the header, security control block, payload, 
        /// MAC and CRC/Checksum suffix
        /// </summary>
        public ReadOnlySpan<byte> OriginalMessageData => _originalMessage;

        /// <summary>
        /// Type of the security block, if there is one
        /// </summary>
        public byte SecurityBlockType { get; }

        /// <summary>
        /// Raw security block bytes
        /// </summary>
        public byte[] SecureBlockData { get; }

        /// <inheritdoc/>
        protected override ReadOnlySpan<byte> Data() => Payload.ToArray();

        private bool IsDataSecure => Payload == null || Payload.Length == 0 || 
            SecurityBlockType == (byte)SecureChannel.SecurityBlockType.ReplyMessageWithDataSecurity || 
            SecurityBlockType == (byte)SecureChannel.SecurityBlockType.CommandMessageWithDataSecurity;
        
        private IEnumerable<byte> Mac { get; }

        /// <summary>
        /// Gets a value indicating whether the data in the incoming message is correct.
        /// </summary>
        /// <returns>true if the data is correct; otherwise, false.</returns>
        public bool IsDataCorrect { get; }

        private static IEnumerable<byte> SecureSessionMessages => new[]
        {
            (byte)SecureChannel.SecurityBlockType.CommandMessageWithNoDataSecurity,
            (byte)SecureChannel.SecurityBlockType.ReplyMessageWithNoDataSecurity,
            (byte)SecureChannel.SecurityBlockType.CommandMessageWithDataSecurity,
            (byte)SecureChannel.SecurityBlockType.ReplyMessageWithDataSecurity,
        };

        /// <summary>
        /// Checks whether the secure cryptogram has been accepted.
        /// </summary>
        /// <returns>Returns true if the secure cryptogram has been accepted; otherwise, false.</returns>
        public bool SecureCryptogramHasBeenAccepted() => SecureBlockData[0] == 0x01;
    }
}
