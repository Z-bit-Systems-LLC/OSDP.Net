using System;

namespace OSDP.Net.Messages;

internal class ReplyTracker
{
    private readonly OutgoingMessage _issuingCommand;
    private readonly DeviceProxy _device;

    public ReplyTracker(Guid connectionId, IncomingMessage replyMessage, OutgoingMessage issuingCommand,
        DeviceProxy device)
    {
        ConnectionId = connectionId;
        ReplyMessage = replyMessage;
        _issuingCommand = issuingCommand;
        _device = device;
    }

    public IncomingMessage ReplyMessage { get; }

    public Guid ConnectionId { get; }

    /// <summary>
    /// Indicates whether the reply received is valid based on the originating command's address
    /// and the correctness of the reply's data.
    /// </summary>
    /// <returns>
    /// True if the reply is valid; otherwise, false.
    /// </returns>
    public bool IsValidReply => ReplyMessage.Address == _issuingCommand.Address && ReplyMessage.IsDataCorrect;

    /// <summary>
    /// Determines if the given address and command code match the issuing command.
    /// </summary>
    /// <param name="address">The device address to check against the issuing command's address.</param>
    /// <param name="commandCode">The command code to check against the issuing command's command code.</param>
    /// <returns>True if the provided address and command code match the issuing command; otherwise false.</returns>
    public bool MatchIssuingCommand(byte address, byte commandCode) =>
        address.Equals(_issuingCommand.Address) && commandCode.Equals(_issuingCommand.Code);

    /// <summary>
    /// Indicates whether the issuing command was a step of the secure channel handshake (osdp_CHLNG or osdp_SCRYPT).
    /// </summary>
    public bool IsReplyToSecureChannelHandshake =>
        _issuingCommand.Code is (byte)CommandType.SessionChallenge or (byte)CommandType.ServerCryptogram;

    internal bool ValidateSecureChannelEstablishment()
    {
        if (!ReplyMessage.SecureCryptogramHasBeenAccepted())
        {
            return false;
        }

        _device.MessageSecureChannel.Establish(ReplyMessage.Payload);

        return true;
    }
}