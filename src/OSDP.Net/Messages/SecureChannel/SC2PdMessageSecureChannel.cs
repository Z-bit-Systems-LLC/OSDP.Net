using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OSDP.Net.Connections;
using OSDP.Net.Model;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Messages.SecureChannel;

/// <summary>
/// SC2 base secure channel for PD side (encrypt replies, decrypt commands).
/// </summary>
internal class SC2PdMessageSecureChannelBase : SC2MessageSecureChannel
{
    /// <summary>
    /// Initializes a new SC2 PD base message secure channel.
    /// </summary>
    /// <param name="context">The SC2 security context.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public SC2PdMessageSecureChannelBase(SC2SecurityContext context, ILoggerFactory loggerFactory = null)
        : base(context, loggerFactory)
    {
    }
}

/// <summary>
/// SC2 PD secure channel with full handshake handling.
/// </summary>
internal class SC2PdMessageSecureChannel : SC2PdMessageSecureChannelBase
{
    private readonly IOsdpConnection _connection;
    private readonly byte[] _clientUID;
    // ReSharper disable once NotAccessedField.Local — written by KeySet handler, read on next session reset
    private byte[] _securityKey;
    private byte[] _expectedServerCryptogram;

    /// <summary>
    /// Initializes a new SC2 PD message secure channel.
    /// </summary>
    /// <param name="connection">The OSDP connection.</param>
    /// <param name="securityKey">The 32-byte SCBK.</param>
    /// <param name="clientUID">The 8-byte client unique identifier.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public SC2PdMessageSecureChannel(
        IOsdpConnection connection,
        byte[] securityKey,
        byte[] clientUID,
        ILoggerFactory loggerFactory = null)
        : base(new SC2SecurityContext(securityKey), loggerFactory)
    {
        _connection = connection;
        _securityKey = securityKey ?? throw new ArgumentNullException(nameof(securityKey));

        if (clientUID == null)
        {
            throw new ArgumentNullException(nameof(clientUID));
        }

        if (clientUID.Length != 8)
        {
            throw new ArgumentException("Client UID must be exactly 8 bytes", nameof(clientUID));
        }

        _clientUID = clientUID;
    }

    /// <summary>
    /// The device address.
    /// </summary>
    public byte Address { get; set; }

    /// <summary>
    /// The security mode for this channel.
    /// </summary>
    public SecurityMode SecurityMode { get; set; } = SecurityMode.Unsecured;

    /// <summary>
    /// Commands allowed without secure channel.
    /// </summary>
    public CommandType[] AllowUnsecured { get; set; } = Array.Empty<CommandType>();

    /// <summary>
    /// Reads the next command from the ACU, handling SC2 handshake messages internally.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next user command, or null on timeout.</returns>
    public async Task<IncomingMessage> ReadNextCommand(CancellationToken cancellationToken = default)
    {
        var commandBuffer = new Collection<byte>();

        if (!await Bus.WaitForStartOfMessage(_connection, commandBuffer, TimeSpan.FromSeconds(8),
                cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (!await Bus.WaitForMessageLength(_connection, commandBuffer, cancellationToken).ConfigureAwait(false))
        {
            throw new TimeoutException("Timeout waiting for command message length");
        }

        if (!await Bus.WaitForRestOfMessage(_connection, commandBuffer, Bus.ExtractMessageLength(commandBuffer),
                cancellationToken).ConfigureAwait(false))
        {
            throw new TimeoutException("Timeout waiting for command of reply message");
        }

        var command = new IncomingMessage(commandBuffer.ToArray().AsSpan(), this);

        if (command.Type != (byte)CommandType.Poll)
        {
            Logger?.LogInformation("Received Command: {CommandType}",
                Enum.GetName(typeof(CommandType), command.Type));
            Logger?.LogDebug("Incoming: {Data}", BitConverter.ToString(commandBuffer.ToArray()));
        }

        var commandHandled = await HandleCommand(command);
        return commandHandled ? await ReadNextCommand(cancellationToken) : command;
    }

    /// <summary>
    /// Sends a reply back to the ACU.
    /// </summary>
    /// <param name="reply">The reply to send.</param>
    /// <param name="sendUnsecured">Whether to send without security.</param>
    internal async Task SendReply(OutgoingReply reply, bool sendUnsecured = false)
    {
        if (reply.Command.Type == (byte)CommandType.KeySet && reply.Code == (byte)ReplyType.Ack)
        {
            HandleKeySetUpdate(reply);
        }

        var replyBuffer = reply.BuildMessage(sendUnsecured ? null : this);

        if (reply.Command.Type != (byte)CommandType.Poll)
        {
            Logger?.LogInformation("Sending Reply: {Reply}",
                Enum.GetName(typeof(ReplyType), reply.PayloadData.Code));
            Logger?.LogDebug("Outgoing: {Data}", BitConverter.ToString(replyBuffer));
        }

        await _connection.WriteAsync(replyBuffer);
    }

    private async Task<bool> HandleCommand(IncomingMessage command)
    {
        if (command.Address != Address && command.Address != ControlPanel.ConfigurationAddress) return true;

        var reply = (command.IsValidMac, (CommandType)command.Type) switch
        {
            (false, _) => HandleInvalidMac(),
            (true, CommandType.SessionChallenge) => HandleSessionChallenge(command),
            (true, CommandType.ServerCryptogram) => HandleSCrypt(command),
            _ => ValidateCommandSecurity(command)
        };

        if (reply == null) return false;

        await SendReply(new OutgoingReply(command, reply), reply.Code == (byte)ReplyType.Nak);

        if (command.Type == (byte)CommandType.ServerCryptogram)
        {
            SC2Context.IsSecurityEstablished = true;
        }

        return true;
    }

    private static PayloadData HandleInvalidMac()
    {
        return new Nak(ErrorCode.CommunicationSecurityNotMet);
    }

    /// <summary>
    /// Handles the SC2 session challenge (osdp_CHLNG) from the ACU.
    /// </summary>
    /// <param name="command">The incoming session challenge command.</param>
    /// <returns>A ChallengeResponse reply.</returns>
    private PayloadData HandleSessionChallenge(IncomingMessage command)
    {
        SC2Context.Reset();

        byte[] rndA = command.Payload;
        byte[] rndB = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(rndB);
        }

        SC2Context.DeriveSessionKeys(rndA, rndB);
        SC2Context.ClientUID = (byte[])_clientUID.Clone();

        var clientCryptogram = SC2Context.ComputeCryptogram(rndA, rndB);
        _expectedServerCryptogram = SC2Context.ComputeCryptogram(rndB, rndA);

        return new ChallengeResponse(_clientUID, rndB, clientCryptogram, false);
    }

    /// <summary>
    /// Handles the SC2 server cryptogram (osdp_SCRYPT) from the ACU.
    /// </summary>
    /// <param name="command">The incoming server cryptogram command.</param>
    /// <returns>An InitialRMac reply (empty payload for SC2).</returns>
    private PayloadData HandleSCrypt(IncomingMessage command)
    {
        var serverCryptogram = command.Payload;

        if (command.SecurityBlockType != (byte)SecurityBlockType.SecureConnectionSequenceStep3)
        {
            Logger?.LogWarning("Received unexpected security block type: {SecurityBlockType}",
                command.SecurityBlockType);
        }
        else if (!serverCryptogram.SequenceEqual(_expectedServerCryptogram))
        {
            Logger?.LogWarning("Received unexpected server cryptogram!");
        }
        else if (IsSecurityEstablished)
        {
            Logger?.LogWarning("Secure channel already established. Why did we get another SCrypt??");
        }
        else
        {
            // SC2: no RMAC computation. Return empty RMAC and establish via counter/nonce.
            return new InitialRMac(Array.Empty<byte>(), true);
        }

        return new Nak(ErrorCode.DoesNotSupportSecurityBlock);
    }

    private PayloadData ValidateCommandSecurity(IncomingMessage command)
    {
        if (IsSecurityEstablished)
        {
            return command.IsSecureMessage ? null : new Nak(ErrorCode.CommunicationSecurityNotMet);
        }

        if (SecurityMode != SecurityMode.FullSecurity)
        {
            return null;
        }

        return AllowUnsecured.Contains((CommandType)command.Type)
            ? null
            : new Nak(ErrorCode.CommunicationSecurityNotMet);
    }

    private void HandleKeySetUpdate(OutgoingReply reply)
    {
        var keySetPayload = EncryptionKeyConfiguration.ParseData(reply.Command.Payload);
        _securityKey = keySetPayload.KeyData;
    }
}
