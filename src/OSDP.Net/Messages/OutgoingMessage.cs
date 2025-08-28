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

        if (securityEstablished && payload.Length > 0)
        {
            payload = secureChannel.PadTheData(payload).ToArray();
        }

        bool isSecurityBlockPresent = securityEstablished || PayloadData.IsSecurityInitialization;
        var securityBlock = isSecurityBlockPresent ? PayloadData.SecurityControlBlock() : Array.Empty<byte>();

        int headerLength = StartOfMessageLength + securityBlock.Length + sizeof(ReplyType);
        int totalLength = headerLength + payload.Length +
                          (ControlBlock.UseCrc ? 2 : 1) +
                          (securityEstablished ? MacSize : 0);
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

        buffer[currentLength++] = PayloadData.Code;

        if (securityEstablished)
        {
            secureChannel.EncodePayload(payload, buffer.AsSpan(currentLength));
            currentLength += payload.Length;
            secureChannel.GenerateMac(buffer.AsSpan(0, currentLength), false)
                .Slice(0, MacSize)
                .CopyTo(buffer.AsSpan(currentLength));
            currentLength += MacSize;
        }
        else
        {
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