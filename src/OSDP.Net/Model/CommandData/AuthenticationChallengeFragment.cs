using System;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Model.CommandData;

/// <summary>
/// Command data to send an authentication challenge fragment to a PD.
/// </summary>
public class AuthenticationChallengeFragment(MessageDataFragment fragment) : CommandData
{
    /// <summary>
    /// Get the message data fragment
    /// </summary>
    public MessageDataFragment Fragment { get; } = fragment;

    /// <inheritdoc />
    public override CommandType CommandType => CommandType.AuthenticateChallenge;

    /// <inheritdoc />
    public override byte Code => (byte)CommandType;

    /// <inheritdoc />
    public override ReadOnlySpan<byte> SecurityControlBlock() => SecurityBlock.CommandMessageWithDataSecurity;

    /// <inheritdoc />
    public override byte[] BuildData()
    {
        return Fragment.BuildData().ToArray();
    }

    /// <summary>Parses the message payload bytes</summary>
    /// <param name="data">Message payload as bytes</param>
    /// <returns>An instance of AuthenticationChallengeFragment representing the message payload</returns>
    public static AuthenticationChallengeFragment ParseData(ReadOnlySpan<byte> data)
    {
        return new AuthenticationChallengeFragment(MessageDataFragment.ParseData(data));
    }
}