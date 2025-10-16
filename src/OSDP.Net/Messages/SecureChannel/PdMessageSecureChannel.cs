using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OSDP.Net.Connections;
using OSDP.Net.Model;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Messages.SecureChannel
{
    /// <summary>
    /// Message channel which represents the Periphery Device (PD) side of the OSDP 
    /// communications (i.e. OSDP commands are received and replies are sent out)
    /// </summary>
    internal class PdMessageSecureChannelBase : MessageSecureChannel
    {
        /// <summary>
        /// Initializes a new instance of the PDMessageChannel
        /// </summary>
        /// <param name="context">Optional security context state to be used by the channel. If one 
        /// is not provided, new default instance of the context will be created internally. This is
        /// useful when more than one channel have a need to share the same security state (i.e. in
        /// cases of implementing a spy that analyzes traffic flow through the two inbound and outbound
        /// channels</param>
        /// <param name="loggerFactory">Optional logger factory from which a logger object for the
        /// message channel will be acquired</param>
        public PdMessageSecureChannelBase(SecurityContext context = null, ILoggerFactory loggerFactory = null)
            : base(context, loggerFactory) { }

        /// <inheritdoc />
        public override void EncodePayload(byte[] payload, Span<byte> destination) =>
            EncodePayload(payload, Context.CMac, destination);

        /// <inheritdoc />
        public override byte[] DecodePayload(byte[] payload) => DecodePayload(payload, Context.RMac);

        /// <inheritdoc />
        public override ReadOnlySpan<byte> GenerateMac(ReadOnlySpan<byte> message, bool isIncoming) =>
            isIncoming ? GenerateCommandMac(message) : GenerateReplyMac(message);
    }

    internal class PdMessageSecureChannel : PdMessageSecureChannelBase
    {
        private readonly IOsdpConnection _connection;
        private byte[] _expectedServerCryptogram;
        private byte[] _securityKey;

        public PdMessageSecureChannel(IOsdpConnection connection, SecurityContext context = null, ILoggerFactory loggerFactory = null)
            : base(context, loggerFactory) 
        {
            _connection = connection;
        }

        public PdMessageSecureChannel(IOsdpConnection connection, byte[] securityKey, ILoggerFactory loggerFactory = null)
            : this(connection, context: null, loggerFactory)
        {
            _securityKey = securityKey;
        }

        public byte Address { get; set; }

        public SecurityMode SecurityMode { get; set; } = SecurityMode.Unsecured;

        public CommandType[] AllowUnsecured { get; set; } = [];

        public async Task<IncomingMessage> ReadNextCommand(CancellationToken cancellationToken = default)
        {
            var commandBuffer = new Collection<byte>();

            if (!await Bus.WaitForStartOfMessage(_connection, commandBuffer, TimeSpan.FromSeconds(8), cancellationToken)
                    .ConfigureAwait(false))
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
                Logger?.LogInformation("Received Command: {CommandType}", Enum.GetName(typeof(CommandType), command.Type));
                Logger?.LogDebug("Incoming: {Data}", BitConverter.ToString(commandBuffer.ToArray()));
            }

            var commandHandled = await HandleCommand(command);
            return commandHandled ? await ReadNextCommand(cancellationToken) : command;
        }

        internal async Task SendReply(OutgoingReply reply, bool sendUnsecured = false)
        {
            if (reply.Command.Type == (byte)CommandType.KeySet && reply.Code == (byte)ReplyType.Ack)
            {
                HandleKeySetUpdate(reply);
            }

            var replyBuffer = reply.BuildMessage(sendUnsecured ? null : this);

            if (reply.Command.Type != (byte)CommandType.Poll)
            {
                Logger?.LogInformation("Sending Reply: {Reply}", Enum.GetName(typeof(ReplyType), reply.PayloadData.Code));
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

            // If we return NAK from here, it is generally because command didn't pass secure channel validation
            // in this case, we can only send the reply unsecured
            await SendReply(new OutgoingReply(command, reply), reply.Code == (byte)ReplyType.Nak);

            if (command.Type == (byte)CommandType.ServerCryptogram)
            {
                Context.IsSecurityEstablished = true;
            }

            return true;
        }
        private PayloadData HandleInvalidMac()
        {
            return new Nak(ErrorCode.CommunicationSecurityNotMet);
        }

        /// <summary>
        /// Default handler for the SessionChallenge message received on the channel
        /// </summary>
        /// <param name="command">Incoming command of type SessionChallenge</param>
        /// <returns>A message representing a reply to the SessionChallenge</returns>
        protected PayloadData HandleSessionChallenge(IncomingMessage command)
        {
            // Per Section D.1.3
            bool useDefaultKey = command.SecureBlockData[0] == 0;

            if (useDefaultKey && 
                !_securityKey.SequenceEqual(SecurityContext.DefaultKey))
            {
                // We want to fail only when device has already been configured with a
                // non-default key AND the use of the default key isn't allowed.
                return new Nak(ErrorCode.DoesNotSupportSecurityBlock);
            }

            Context.Reset(useDefaultKey ? SecurityContext.DefaultKey : _securityKey);

            // generate a set of session keys: S-ENC, S-MAC1, S-MAC2 using command.Payload (which is RND.A)
            using var crypto = Context.CreateCypher(true);
            byte[] rndA = command.Payload;

            // TODO: we should validate payload and SCB type

            Context.Enc = SecurityContext.GenerateKey(crypto, new byte[] { 0x01, 0x82, rndA[0], rndA[1], rndA[2], rndA[3], rndA[4], rndA[5] });
            Context.SMac1 = SecurityContext.GenerateKey(crypto, new byte[] { 0x01, 0x01, rndA[0], rndA[1], rndA[2], rndA[3], rndA[4], rndA[5] });
            Context.SMac2 = SecurityContext.GenerateKey(crypto, new byte[] { 0x01, 0x02, rndA[0], rndA[1], rndA[2], rndA[3], rndA[4], rndA[5] });

            // TODO: this should be some kind of unique identifier, but a) not sure how to generate it and b) seems
            // the other side presently simply ignores these bytes. So for time being simply leaving this uninitialized
            byte[] cUID = new byte[8];
            byte[] rndB = new byte[8];

            new Random().NextBytes(rndB);
            crypto.Key = Context.Enc;
            var clientCryptogram = SecurityContext.GenerateKey(crypto, rndA, rndB);
            _expectedServerCryptogram = SecurityContext.GenerateKey(crypto, rndB, rndA);

            // reply with osdp_CCRYPT, returning PD's Id (cUID), its random number and the client cryptogram
            return new ChallengeResponse(cUID, rndB, clientCryptogram, useDefaultKey);
        }
        
        /// <summary>
        /// Default handler to the SCrypt command received on the channel
        /// </summary>
        /// <param name="command">An incoming message representing SCrypt command</param>
        /// <returns>Reply to the SCrypt command</returns>
        protected PayloadData HandleSCrypt(IncomingMessage command)
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
                var crypto = Context.CreateCypher(true, Context.SMac1);
                Context.RMac = SecurityContext.GenerateKey(crypto, serverCryptogram);
                crypto.Key = Context.SMac2;
                Context.RMac = SecurityContext.GenerateKey(crypto, Context.RMac);

                return new InitialRMac(Context.RMac, true);
            }

            return new Nak(ErrorCode.DoesNotSupportSecurityBlock);
        }

        private PayloadData ValidateCommandSecurity(IncomingMessage command)
        {
            if (IsSecurityEstablished)
            {
                return command.IsSecureMessage ? null : new Nak(ErrorCode.CommunicationSecurityNotMet);
            }
            else if (SecurityMode != SecurityMode.FullSecurity)
            {
                return null;
            }
            else 
            {
                return AllowUnsecured.Contains((CommandType)command.Type) ?
                    null : new Nak(ErrorCode.CommunicationSecurityNotMet);
            }
        }

        private void HandleKeySetUpdate(OutgoingReply reply)
        {
            var keySetPayload = EncryptionKeyConfiguration.ParseData(reply.Command.Payload);

            _securityKey = keySetPayload.KeyData;
        }
    }

    internal enum SecurityMode
    {
        Unsecured,
        InstallMode,
        FullSecurity
    }
}
