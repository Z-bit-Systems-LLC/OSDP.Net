using System;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;

namespace OSDP.Net.Messages;

internal class OutgoingMessage : Message
{
    private const int StartOfMessageLength = 5;

    internal OutgoingMessage(byte address, Control controlBlock, PayloadData data)
    {
        Address = address;
        ControlBlock = controlBlock;
        PayloadData = data;
    }

    internal Control ControlBlock { get; }

    internal byte Code => PayloadData.Code;
    
    internal PayloadData PayloadData { get; }

    internal byte[] BuildMessage(IMessageSecureChannel secureChannel)
    {
        var payload = PayloadData.BuildData();

        var securityEstablished = secureChannel is { IsSecurityEstablished: true };
        var isSC2 = securityEstablished && secureChannel.IsSecureChannelV2;

        if (securityEstablished && payload.Length > 0)
        {
            payload = secureChannel.PadTheData(payload).ToArray();
        }

        bool isSecurityBlockPresent = securityEstablished || PayloadData.IsSecurityInitialization;
        ReadOnlySpan<byte> securityBlock;
        if (isSC2)
        {
            // SC2: always use WithDataSecurity because command byte is encrypted
            bool isReply = (Address & 0x80) != 0;
            securityBlock = isReply
                ? SecurityBlock.ReplyMessageWithDataSecurity
                : SecurityBlock.CommandMessageWithDataSecurity;
        }
        else
        {
            securityBlock = isSecurityBlockPresent
                ? PayloadData.SecurityControlBlock()
                : Array.Empty<byte>();
        }

        int authTagSize = securityEstablished ? secureChannel.AuthenticationTagSize : 0;

        // SC2: command byte is inside ciphertext, not in clear header
        int ciphertextLength = isSC2 ? 1 + payload.Length : payload.Length;
        int commandByteInHeader = isSC2 ? 0 : sizeof(ReplyType);
        int headerLength = StartOfMessageLength + securityBlock.Length + commandByteInHeader;
        int totalLength = headerLength + ciphertextLength +
                          (ControlBlock.UseCrc ? 2 : 1) +
                          authTagSize;
        var buffer = new byte[totalLength];
        int currentLength = 0;

        buffer[currentLength++] = StartOfMessage;
        buffer[currentLength++] = Address;
        buffer[currentLength++] = (byte)(totalLength & 0xff);
        buffer[currentLength++] = (byte)((totalLength >> 8) & 0xff);
        buffer[currentLength++] = (byte)((ControlBlock.ControlByte & 0x07) | (isSecurityBlockPresent ? 0x08 : 0x00));

        if (isSecurityBlockPresent)
        {
            securityBlock.CopyTo(buffer.AsSpan(currentLength));
            currentLength += securityBlock.Length;
        }

        if (securityEstablished)
        {
            if (isSC2)
            {
                // SC2: prepend command byte to payload and encrypt together
                // Pass header as AAD (Associated Authenticated Data) for GCM
                var plaintext = new byte[1 + payload.Length];
                plaintext[0] = PayloadData.Code;
                payload.CopyTo(plaintext, 1);
                var aad = buffer.AsSpan(0, currentLength);
                ((SC2MessageSecureChannel)secureChannel).EncodePayload(plaintext, buffer.AsSpan(currentLength), aad);
                currentLength += plaintext.Length;
            }
            else
            {
                // SC1: command byte in clear header, encrypt payload separately
                buffer[currentLength++] = PayloadData.Code;
                secureChannel.EncodePayload(payload, buffer.AsSpan(currentLength));
                currentLength += payload.Length;
            }

            secureChannel.GenerateMac(buffer.AsSpan(0, currentLength), false)
                .Slice(0, authTagSize)
                .CopyTo(buffer.AsSpan(currentLength));
            currentLength += authTagSize;
        }
        else
        {
            buffer[currentLength++] = PayloadData.Code;
            payload.CopyTo(buffer, currentLength);
            currentLength += payload.Length;
        }

        if (ControlBlock.UseCrc)
        {
            AddCrc(buffer);
            currentLength += 2;
        }
        else
        {
            AddChecksum(buffer);
            currentLength++;
        }

        if (currentLength != buffer.Length)
        {
            throw new Exception(
                $"Invalid processing of reply data, expected length {currentLength}, actual length {buffer.Length}");
        }

        PayloadData.CustomMessageUpdate(buffer);

        // Section 5.7 states that transmitting device shall guarantee an idle time between packets. This is
        // accomplished by sending a character with all bits set to 1. The driver byte is required by
        // converters and multiplexers to sense when line is idle.
        var messageBuffer = new byte[buffer.Length + 1];
        messageBuffer[0] = Bus.DriverByte;
        Buffer.BlockCopy(buffer, 0, messageBuffer, 1, buffer.Length);

        return messageBuffer;
    }

    protected override ReadOnlySpan<byte> Data()
    {
        return PayloadData.BuildData();
    }
}


internal class OutgoingReply : OutgoingMessage
{
    internal OutgoingReply(IncomingMessage command, PayloadData replyPayload) :
        base((byte)(command.Address | 0x80), command.ControlBlock, replyPayload)
    {
        Command = command;
    }
    internal IncomingMessage Command { get; }
}