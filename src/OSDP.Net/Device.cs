using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using CommunicationConfiguration = OSDP.Net.Model.CommandData.CommunicationConfiguration;
using ManufacturerSpecific = OSDP.Net.Model.CommandData.ManufacturerSpecific;

namespace OSDP.Net;

/// <summary>
/// Represents a Peripheral Device (PD) that communicates over the OSDP protocol.
/// </summary>
public class Device : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<PayloadData> _pendingPollReplies = new();

    private volatile int _connectionContextCounter;
    private DeviceConfiguration _deviceConfiguration;
    private IOsdpConnectionListener _connectionListener;
    private CancellationTokenSource _cancellationTokenSource;
    private DateTime _lastValidReceivedCommand = DateTime.MinValue;
    private PairingTransport _pairingTransport;
    private byte[] _pendingPairedKey;

    private const int PairingReplyFragmentSize = 128;
    private static readonly TimeSpan PairingSessionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Represents a Peripheral Device (PD) that communicates over the OSDP protocol.
    /// </summary>
    public Device(DeviceConfiguration config, ILoggerFactory loggerFactory = null)
    {
        _deviceConfiguration = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<Device>();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets a value indicating whether the device is currently connected.
    /// </summary>
    /// <value><c>true</c> if the device is connected; otherwise, <c>false</c>.</value>
    public bool IsConnected => _connectionListener?.ConnectionCount > 0 && (
        _lastValidReceivedCommand + ConnectionTiming.OfflineThreshold >= DateTime.UtcNow);

    /// <summary>
    /// Gets raised whenever osdp_ComSet command is successfully processed, and there is 
    /// a change in either device address or baud rate. Because baud rate is configured on
    /// the OSDP connection/server that is passed down into the Device class, it is up to
    /// the consumer of the Device class (i.e., whatever code that creates that class in the
    /// first place) to properly handle this event and re-initialize the Device with the
    /// correct connection settings.
    /// 
    /// NOTE: In addition to this event, there's also `HandleCommunicationSet` which the
    /// deriving class MUST override if it is to support osdp_ComSet properly. The overriding
    /// allows the device to validate and accept/reject the command parameters which occurs
    /// prior to this event
    /// </summary>
    public event EventHandler<DeviceComSetUpdatedEventArgs> DeviceComSetUpdated;

    /// <summary>
    /// Disposes the Device instance.
    /// </summary>
    /// <remarks>
    /// This method is responsible for releasing any resources used by the Device instance. 
    /// </remarks>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            var _ = StopListening();
        }
    }

    /// <summary>
    /// Starts listening for commands from the ACU through the specified connection listener.
    /// </summary>
    /// <param name="connectionListener">The connection listener used to accept incoming connections from ACUs.</param>
    public async Task StartListening(IOsdpConnectionListener connectionListener)
    {
        _connectionListener = connectionListener ?? throw new ArgumentNullException(nameof(connectionListener));
        _cancellationTokenSource = new CancellationTokenSource();
        await _connectionListener.Start(ClientListenLoop);
    }

    private async Task ClientListenLoop(IOsdpConnection incomingConnection)
    {
        try
        {
            var currentContextCount = _connectionContextCounter;
            if (_deviceConfiguration.SecureChannelVersion == SecureChannelVersion.V2)
            {
                var channel = new SC2PdMessageSecureChannel(
                    incomingConnection, _deviceConfiguration.SecurityKey,
                    _deviceConfiguration.Identification.ToBytes(), _loggerFactory)
                {
                    Address = _deviceConfiguration.Address,
                    SecurityMode = !_deviceConfiguration.RequireSecurity
                        ? SecurityMode.Unsecured
                        : SecurityMode.FullSecurity,
                    AllowUnsecured = EffectiveAllowUnsecured(),
                };

                await RunClientLoop(channel, incomingConnection, currentContextCount);
            }
            else
            {
                var channel = new PdMessageSecureChannel(
                    incomingConnection, _deviceConfiguration.SecurityKey,
                    _deviceConfiguration.Identification.ToBytes(), _loggerFactory)
                {
                    Address = _deviceConfiguration.Address,
                    SecurityMode = !_deviceConfiguration.RequireSecurity
                        ? SecurityMode.Unsecured
                        : (_deviceConfiguration.SecurityKey == null ||
                           _deviceConfiguration.SecurityKey.SequenceEqual(SecurityContext.DefaultKey))
                        ? SecurityMode.InstallMode
                        : SecurityMode.FullSecurity,
                    AllowUnsecured = EffectiveAllowUnsecured(),
                };

                await RunClientLoop(channel, incomingConnection, currentContextCount);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Connection loop cancelled");
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, $"Unexpected exception in polling loop");
        }
        finally
        {
            await incomingConnection.Close();
        }
    }

    private async Task RunClientLoop(PdMessageSecureChannel channel, IOsdpConnection connection, int contextCount)
    {
        while (connection.IsOpen && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            var command = await channel.ReadNextCommand(_cancellationTokenSource.Token);
            if (command == null) continue;

            var reply = HandleCommand(command);
            await channel.SendReply(reply);

            if (contextCount != _connectionContextCounter)
            {
                _logger?.LogInformation("Interrupting existing connection due to 'force disconnect' flag");
                break;
            }
        }
    }

    private async Task RunClientLoop(SC2PdMessageSecureChannel channel, IOsdpConnection connection, int contextCount)
    {
        while (connection.IsOpen && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            var command = await channel.ReadNextCommand(_cancellationTokenSource.Token);
            if (command == null) continue;

            var reply = HandleCommand(command);
            await channel.SendReply(reply);

            // Pairing just completed and its Result reply has now been sent. Activate the derived key
            // on this running channel and switch it to full security so the ACU's next SC2 handshake
            // uses the paired key — no reconnect required. Also update the configuration so a future
            // reconnect uses the paired key. Order matters: this must run after the Result is sent.
            if (_pendingPairedKey != null)
            {
                var pairedKey = _pendingPairedKey;
                _pendingPairedKey = null;
                channel.ActivatePairedKey(pairedKey);
                UpdateDeviceConfig(c =>
                {
                    c.SecurityKey = pairedKey;
                    c.RequireSecurity = true;
                });
            }

            if (contextCount != _connectionContextCounter)
            {
                _logger?.LogInformation("Interrupting existing connection due to 'force disconnect' flag");
                break;
            }
        }
    }

    /// <summary>
    /// Stops listening for OSDP messages on the device.
    /// </summary>
    public async Task StopListening()
    {
        _cancellationTokenSource?.Cancel();
        await (_connectionListener?.Stop() ?? Task.CompletedTask);
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _connectionListener = null;
    }

    /// <summary>
    /// Enqueues a reply into the pending poll reply queue.
    /// </summary>
    /// <param name="reply">The reply to enqueue.</param>
    public void EnqueuePollReply(PayloadData reply) => _pendingPollReplies.Enqueue(reply);

    internal virtual OutgoingReply HandleCommand(IncomingMessage command)
    {
        if (command.IsDataCorrect && Enum.IsDefined(typeof(CommandType), command.Type))
            _lastValidReceivedCommand = DateTime.UtcNow;

        var commandType = (CommandType)command.Type;
        var reply = commandType switch
        {
            CommandType.Poll => HandlePoll(),
            CommandType.IdReport => DispatchIdReport(command.Payload),
            CommandType.DeviceCapabilities => HandleDeviceCapabilities(),
            CommandType.LocalStatus => HandleLocalStatusReport(),
            CommandType.InputStatus => HandleInputStatusReport(),
            CommandType.OutputStatus => HandleOutputStatusReport(),
            CommandType.ReaderStatus => HandleReaderStatusReport(),
            CommandType.OutputControl => HandleOutputControl(OutputControls.ParseData(command.Payload)),
            CommandType.LEDControl => HandleReaderLEDControl(ReaderLedControls.ParseData(command.Payload)),
            CommandType.BuzzerControl => HandleBuzzerControl(ReaderBuzzerControl.ParseData(command.Payload)),
            CommandType.TextOutput => HandleTextOutput(ReaderTextOutput.ParseData(command.Payload)),
            CommandType.CommunicationSet => _HandleCommunicationSet(CommunicationConfiguration.ParseData(command.Payload)),
            CommandType.BioRead => HandleBiometricRead(BiometricReadData.ParseData(command.Payload)),
            CommandType.BioMatch => HandleBiometricMatch(BiometricTemplateData.ParseData(command.Payload)),
            CommandType.KeySet => _HandleKeySettings(EncryptionKeyConfiguration.ParseData(command.Payload)),
            CommandType.MaxReplySize => HandleMaxReplySize(ACUReceiveSize.ParseData(command.Payload)),
            CommandType.FileTransfer => HandleFileTransfer(FileTransferFragment.ParseData(command.Payload)),
            CommandType.ManufacturerSpecific => HandleManufacturerCommand(ManufacturerSpecific.ParseData(command.Payload)),
            CommandType.ExtendedWrite => HandleExtendedWrite(ExtendedWrite.ParseData(command.Payload)),
            CommandType.Abort => HandleAbortRequest(),
            CommandType.PivData => HandlePivData(GetPIVData.ParseData(command.Payload)),
            CommandType.KeepActive => HandleKeepActive(KeepReaderActive.ParseData(command.Payload)),
            CommandType.Pair => HandlePairCommand(PairFragment.ParseData(command.Payload)),
            _ => HandleUnknownCommand(command)
        };

        return new OutgoingReply(command, EnsureValidReply(commandType, reply));
    }

    /// <summary>
    /// Commands that the OSDP spec requires to be answered with a specific report reply. A handler
    /// that returns something else (e.g. an Ack) is a protocol violation, so the reply is replaced
    /// with a NAK to avoid putting an invalid message on the wire.
    /// </summary>
    private static readonly Dictionary<CommandType, ReplyType[]> MandatoryReportReplies = new()
    {
        [CommandType.IdReport] = new[] { ReplyType.PdIdReport, ReplyType.ExtendedPdIdReport },
        [CommandType.DeviceCapabilities] = new[] { ReplyType.PdCapabilitiesReport },
        [CommandType.LocalStatus] = new[] { ReplyType.LocalStatusReport },
        [CommandType.InputStatus] = new[] { ReplyType.InputStatusReport },
        [CommandType.OutputStatus] = new[] { ReplyType.OutputStatusReport },
        [CommandType.ReaderStatus] = new[] { ReplyType.ReaderStatusReport }
    };

    /// <summary>
    /// Validates that a handler's reply is permitted for the given command. For commands that mandate
    /// a specific report, only that report (or a NAK/BUSY) is allowed; any other reply is replaced with
    /// a NAK and logged. Other commands are returned unchanged.
    /// </summary>
    internal PayloadData EnsureValidReply(CommandType commandType, PayloadData reply)
    {
        if (!MandatoryReportReplies.TryGetValue(commandType, out var validReplies))
        {
            return reply;
        }

        var replyType = (ReplyType)reply.Code;
        if (replyType == ReplyType.Nak || replyType == ReplyType.Busy ||
            Array.IndexOf(validReplies, replyType) >= 0)
        {
            return reply;
        }

        _logger?.LogError(
            "Handler for {CommandType} returned an invalid reply ({ReplyType}); substituting NAK. " +
            "A spec-compliant PD must answer with one of [{ValidReplies}], or NAK/BUSY.",
            commandType, replyType, string.Join(", ", validReplies));

        return new Nak(ErrorCode.UnknownCommandCode);
    }

    private PayloadData HandlePoll()
    {
        return _pendingPollReplies.TryDequeue(out var reply) ? reply : new Ack();
    }

    private PayloadData DispatchIdReport(ReadOnlySpan<byte> payload)
    {
        // Check if the request is for extended ID (data byte = 0x01)
        if (payload.Length > 0 && payload[0] == 0x01)
        {
            return HandleExtendedIdReport();
        }

        return HandleIdReport();
    }

    /// <summary>
    /// Handles the ID Report Request command received from the OSDP device.
    /// </summary>
    /// <returns>A payload data response to the ID report request. Override this method to provide device identification information.</returns>
    protected virtual PayloadData HandleIdReport()
    {
        return HandleUnknownCommand(CommandType.IdReport);
    }

    /// <summary>
    /// Handles the Extended ID Report Request command received from the OSDP device.
    /// </summary>
    /// <returns>A payload data response containing extended device identification. Override this method to provide extended identification information.</returns>
    /// <remarks>
    /// This method is called when an osdp_ID command is received with data byte 0x01.
    /// The response should be an ExtendedDeviceIdentification containing TLV-encoded device information.
    /// </remarks>
    protected virtual PayloadData HandleExtendedIdReport()
    {
        return HandleUnknownCommand(CommandType.IdReport);
    }

    /// <summary>
    /// Handles the text output command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming reader text output command payload.</param>
    /// <returns>A payload data response indicating the result of the text output operation.</returns>
    protected virtual PayloadData HandleTextOutput(ReaderTextOutput commandPayload)
    {
        return HandleUnknownCommand(CommandType.TextOutput);
    }

    /// <summary>
    /// Handles the reader buzzer control command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming reader buzzer control command payload.</param>
    /// <returns>A payload data response indicating the result of the buzzer control operation.</returns>
    protected virtual PayloadData HandleBuzzerControl(ReaderBuzzerControl commandPayload)
    {
        return HandleUnknownCommand(CommandType.BuzzerControl);
    }

    /// <summary>
    /// Handles the output controls command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming output controls a command payload.</param>
    /// <returns>A payload data response indicating the result of the output control operation.</returns>
    protected virtual PayloadData HandleOutputControl(OutputControls commandPayload)
    {
        return HandleUnknownCommand(CommandType.OutputControl);
    }

    /// <summary>
    /// Handles the device capabilities request command received from the OSDP device.
    /// </summary>
    /// <returns>A payload data response containing the device capabilities. Override this method to provide actual device capabilities.</returns>
    protected virtual PayloadData HandleDeviceCapabilities()
    {
        return HandleUnknownCommand(CommandType.DeviceCapabilities);
    }

    /// <summary>
    /// Handles the get PIV data command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming get PIV data command payload.</param>
    /// <returns>A payload data response containing the requested PIV data or appropriate error response.</returns>
    protected virtual PayloadData HandlePivData(GetPIVData commandPayload)
    {
        return HandleUnknownCommand(CommandType.PivData);
    }

    /// <summary>
    /// Handles the manufacturer-specific command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming manufacturer-specific command payload.</param>
    /// <returns>A payload data response for the manufacturer-specific command.</returns>
    protected virtual PayloadData HandleManufacturerCommand(ManufacturerSpecific commandPayload)
    {
        return HandleUnknownCommand(CommandType.ManufacturerSpecific);
    }

    /// <summary>
    /// Handles the extended write (transparent mode) command received from the ACU.
    /// </summary>
    /// <param name="commandPayload">The incoming extended write command payload.</param>
    /// <returns>
    /// A payload data response. Typically an <see cref="ExtendedRead"/> reply, an Ack, or a Nak.
    /// </returns>
    /// <remarks>
    /// Transparent mode tunnels ISO 7816-4 smart-card APDUs through a PD reader over the OSDP
    /// link. The default implementation NAKs with <c>UnknownCommandCode</c>; PDs that support
    /// transparent mode should override and return an <see cref="ExtendedRead"/> payload.
    /// To push an unsolicited XRD (e.g., card-present notification) call
    /// <see cref="EnqueuePollReply"/> with an <see cref="ExtendedRead"/> instance.
    /// </remarks>
    protected virtual PayloadData HandleExtendedWrite(ExtendedWrite commandPayload)
    {
        return HandleUnknownCommand(CommandType.ExtendedWrite);
    }

    /// <summary>
    /// Handles the keep active command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming keep active command payload.</param>
    /// <returns>A payload data response acknowledging the keep active command.</returns>
    protected virtual PayloadData HandleKeepActive(KeepReaderActive commandPayload)
    {
        return HandleUnknownCommand(CommandType.KeepActive);
    }

    /// <summary>
    /// Handles the abort request command received from the OSDP device.
    /// </summary>
    /// <returns>A payload data response acknowledging the abort request.</returns>
    protected virtual PayloadData HandleAbortRequest()
    {
        return HandleUnknownCommand(CommandType.Abort);
    }

    /// <summary>
    /// Handles the file transfer command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming file transfer fragment command message.</param>
    /// <returns></returns>
    private PayloadData HandleFileTransfer(FileTransferFragment commandPayload)
    {
        _logger.LogInformation("Received a file transfer command: {CommandPayload}", commandPayload.ToString());
        return HandleUnknownCommand(CommandType.FileTransfer);
    }

    /// <summary>
    /// Handles the maximum ACU receive size command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The ACU maximum receive size command payload.</param>
    /// <returns>A payload data response acknowledging the maximum received size setting.</returns>
    protected virtual PayloadData HandleMaxReplySize(ACUReceiveSize commandPayload)
    {
        return HandleUnknownCommand(CommandType.MaxReplySize);
    }

    private PayloadData _HandleKeySettings(EncryptionKeyConfiguration commandPayload)
    {
        var response = HandleKeySettings(commandPayload);

        if (response.Code == (byte)ReplyType.Ack)
        {
            UpdateDeviceConfig(c => c.SecurityKey = commandPayload.KeyData);
        }

        return response;
    }

    /// <summary>
    /// If deriving PD class is intending to support secure connections, it MUST override
    /// this method to provide its own means of persisting a newly set security key 
    /// which was sent by the ACU. The base `Device` class will automatically pick up the new key
    /// for future connections if this function returns a successful Ack response.
    /// NOTE: Any existing connections will continue to use the previous key. It is up to the
    /// ACU to drop a connection and reconnect if it wishes to do so
    /// </summary>
    /// <param name="commandPayload">The key settings command payload.</param>
    /// <returns>
    /// Ack - if the new key was successfully accepted
    /// Nak - if the new key was rejected
    /// </returns>
    protected virtual PayloadData HandleKeySettings(EncryptionKeyConfiguration commandPayload)
    {
        return HandleUnknownCommand(CommandType.KeySet);
    }

    /// <summary>
    /// Handles the biometric match command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The biometric match command payload.</param>
    /// <returns>A payload data response containing the biometric match result.</returns>
    protected virtual PayloadData HandleBiometricMatch(BiometricTemplateData commandPayload)
    {
        return HandleUnknownCommand(CommandType.BioMatch);
    }

    /// <summary>
    /// Handles the biometric read command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The biometric read command payload.</param>
    /// <returns>A payload data response containing the biometric read result.</returns>
    protected virtual PayloadData HandleBiometricRead(BiometricReadData commandPayload)
    {
        return HandleUnknownCommand(CommandType.BioRead);
    }

    private PayloadData _HandleCommunicationSet(CommunicationConfiguration commandPayload)
    {
        var response = HandleCommunicationSet(commandPayload);

        if (response.Code == (byte)ReplyType.PdCommunicationsConfigurationReport)
        {
            var config = (Model.ReplyData.CommunicationConfiguration)response;
            var previousAddress = _deviceConfiguration.Address;
            var previousBaudRate = _connectionListener.BaudRate;

            if (previousAddress != config.Address)
            {
                UpdateDeviceConfig(c => c.Address = config.Address);
            }
            
            if (previousBaudRate != config.BaudRate || previousAddress != config.Address)
            {
                var updatedEvent = DeviceComSetUpdated;
                if (updatedEvent != null) 
                {
                    // Decouple a current call stack from the event invocation, which could result
                    // in the event subscriber resetting the entire connection so that the current
                    // command has a chance to run to completion, and we don't have any deadlock
                    // situations.
                    Task.Run(() =>
                    {
                        updatedEvent.Invoke(this, new DeviceComSetUpdatedEventArgs()
                        {
                            OldAddress = previousAddress,
                            OldBaudRate = previousBaudRate,
                            NewAddress = config.Address,
                            NewBaudRate = config.BaudRate,
                        });
                    });
                }
            }
        }

        return response;
    }

    /// <summary>
    /// If deriving PD class is intending to support updating the communication settings, it MUST override
    /// this method to provide its own means of persisting a new baud rate and address 
    /// which was sent by the ACU.
    /// 
    /// NOTE: The consumer will need to listen to the DeviceComSetUpdated event. It allows it to reinitialize the
    /// connection after successfully sending the reply.
    /// </summary>
    /// <param name="commandPayload">The requested communication settings command payload.</param>
    /// <returns>
    /// PdCommunicationsConfigurationReport - if updated communication settings are successfully accepted. Populate
    /// the data with the new values.
    /// Nak - if the communication settings are rejected
    /// </returns>
    protected virtual PayloadData HandleCommunicationSet(CommunicationConfiguration commandPayload)
    {
        return HandleUnknownCommand(CommandType.CommunicationSet);
    }

    /// <summary>
    /// Handles the reader LED controls command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The reader LED controls a command payload.</param>
    /// <returns>A payload data response indicating the result of the LED control operation.</returns>
    protected virtual PayloadData HandleReaderLEDControl(ReaderLedControls commandPayload)
    {
        return HandleUnknownCommand(CommandType.LEDControl);
    }

    /// <summary>
    /// Handles the reader status command received from the OSDP device.
    /// </summary>
    /// <returns>A payload data response containing the current reader status information.</returns>
    protected virtual PayloadData HandleReaderStatusReport()
    {
        return HandleUnknownCommand(CommandType.ReaderStatus);
    }

    /// <summary>
    /// Handles the output status command received from the OSDP device.
    /// </summary>
    /// <returns>A payload data response containing the current output status information.</returns>
    protected virtual PayloadData HandleOutputStatusReport()
    {
        return HandleUnknownCommand(CommandType.OutputStatus);
    }

    /// <summary>
    /// Handles the input status command received from the OSDP device.
    /// </summary>
    /// <returns>A payload data response containing the current input status information.</returns>
    protected virtual PayloadData HandleInputStatusReport()
    {
        return HandleUnknownCommand(CommandType.InputStatus);
    }

    /// <summary>
    /// Handles the reader local status command received from the OSDP device.
    /// </summary>
    /// <returns>A payload data response containing the current local status information.</returns>
    protected virtual PayloadData HandleLocalStatusReport()
    {
        return HandleUnknownCommand(CommandType.LocalStatus);
    }

    private PayloadData HandleUnknownCommand(IncomingMessage command)
    {
        _logger?.LogInformation("Unexpected Command: {CommandType}", (CommandType)command.Type);

        return new Nak(ErrorCode.UnknownCommandCode);
    }

   private PayloadData HandleUnknownCommand(CommandType commandType)
    {
        _logger?.LogInformation("Unexpected Command: {CommandType}", commandType);

        return new Nak(ErrorCode.UnknownCommandCode);
    }

    private CommandType[] EffectiveAllowUnsecured()
    {
        var configured = _deviceConfiguration.AllowUnsecured ?? Array.Empty<CommandType>();
        if (_deviceConfiguration.Pairing == null || Array.IndexOf(configured, CommandType.Pair) >= 0)
        {
            return configured;
        }

        // Pairing runs in cleartext before any secure channel, so the Pair command must be
        // accepted unsecured when a device is configured for asymmetric pairing.
        var extended = new CommandType[configured.Length + 1];
        Array.Copy(configured, extended, configured.Length);
        extended[configured.Length] = CommandType.Pair;
        return extended;
    }

    /// <summary>
    /// Tracks the in-progress asymmetric pairing exchange: the reassembly buffer for the current
    /// inbound message and the responder session that spans messages 1 through 3.
    /// </summary>
    private sealed class PairingTransport
    {
        internal Pairing.PdPairingSession Session { get; set; }
        internal List<byte> InboundBuffer { get; } = new();
        internal int InboundTotal { get; set; }
        internal DateTime LastActivity { get; set; }
    }

    /// <summary>
    /// Handles one fragment of an asymmetric pairing exchange (osdp_PAIR). Reassembles the inbound
    /// pairing message, runs the responder state machine on completion, and queues the fragmented
    /// response for delivery on subsequent polls. Returns a NAK when the device is not configured
    /// for pairing.
    /// </summary>
    private PayloadData HandlePairCommand(PairFragment command)
    {
        var configuration = _deviceConfiguration.Pairing;
        if (configuration == null)
        {
            return new Nak(ErrorCode.UnknownCommandCode);
        }

        var fragment = command.Fragment;

        // A message-1 first fragment always starts a fresh session (retry-friendly); a message-3
        // first fragment continues the existing session. The inner message type byte is the first
        // byte of the reassembled payload, so it is the first data byte at offset zero.
        if (fragment.Offset == 0)
        {
            if (_pairingTransport != null && DateTime.UtcNow - _pairingTransport.LastActivity > PairingSessionTimeout)
            {
                _pairingTransport = null;
            }

            var messageType = fragment.DataFragment.Length > 0 ? fragment.DataFragment[0] : (byte)0;
            if (messageType == Pairing.PairingMessages.TypeMessage1)
            {
                _pairingTransport = new PairingTransport { Session = new Pairing.PdPairingSession(configuration) };
            }
            else if (messageType != Pairing.PairingMessages.TypeMessage3 || _pairingTransport?.Session == null)
            {
                _pairingTransport = null;
                return new Nak(ErrorCode.UnableToProcessCommand);
            }

            _pairingTransport.InboundBuffer.Clear();
            _pairingTransport.InboundTotal = fragment.TotalSize;
        }

        if (_pairingTransport == null)
        {
            return new Nak(ErrorCode.UnableToProcessCommand);
        }

        _pairingTransport.LastActivity = DateTime.UtcNow;
        _pairingTransport.InboundBuffer.AddRange(fragment.DataFragment);

        if (_pairingTransport.InboundBuffer.Count < _pairingTransport.InboundTotal)
        {
            return new Ack();
        }

        var message = _pairingTransport.InboundBuffer.ToArray();
        var step = ProcessCompletePairingMessage(configuration, message);
        if (step.Response == null)
        {
            return new Ack();
        }

        if (step.IsResult)
        {
            // The Result is a single fragment. Return it directly (rather than via the poll queue) so
            // it is delivered before this channel switches to secure mode. On success, stage the paired
            // key so the loop activates it after this reply is sent — see RunClientLoop.
            if (step.PairedKey != null)
            {
                _pendingPairedKey = step.PairedKey;
            }

            return new PairData((ushort)step.Response.Length, 0, step.Response);
        }

        // Message 2 is multi-fragment; deliver it over subsequent polls.
        QueuePairingResponse(step.Response);
        return new Ack();
    }

    private readonly struct PairingStepResult
    {
        internal PairingStepResult(byte[] response, bool isResult, byte[] pairedKey)
        {
            Response = response;
            IsResult = isResult;
            PairedKey = pairedKey;
        }

        internal byte[] Response { get; }
        internal bool IsResult { get; }
        internal byte[] PairedKey { get; }
    }

    private PairingStepResult ProcessCompletePairingMessage(Pairing.PairingConfiguration configuration, byte[] message)
    {
        var session = _pairingTransport.Session;
        try
        {
            if (message.Length > 0 && message[0] == Pairing.PairingMessages.TypeMessage1)
            {
                return new PairingStepResult(session.ProcessMessage1(message), false, null);
            }

            var outcome = session.ProcessMessage3(message);
            if (!outcome.Success)
            {
                _pairingTransport = null;
                return new PairingStepResult(outcome.FailureResult, true, null);
            }

            var pairingResult = new Pairing.PairingResult(outcome.Scbk, session.PeerCertificate);
            var persisted = PersistPairedKey(configuration, pairingResult);
            var result = session.BuildResult(persisted ? Pairing.PairingStatus.Success
                : Pairing.PairingStatus.PersistenceFailed);
            _pairingTransport = null;
            return new PairingStepResult(result, true, persisted ? outcome.Scbk : null);
        }
        catch (Pairing.PairingException exception)
        {
            _logger?.LogInformation(exception, "Pairing rejected: {Status}", exception.Status);
            _pairingTransport = null;
            var status = exception.Status is Pairing.PairingStatus.PolicyRejected
                ? Pairing.PairingStatus.PolicyRejected
                : Pairing.PairingStatus.ProtocolError;
            return new PairingStepResult(Pairing.PairingMessages.EncodeResult(status, Array.Empty<byte>()), true, null);
        }
    }

    private bool PersistPairedKey(Pairing.PairingConfiguration configuration, Pairing.PairingResult result)
    {
        if (configuration.OnScbkEstablished == null)
        {
            return true;
        }

        try
        {
            var token = _cancellationTokenSource?.Token ?? CancellationToken.None;
            return configuration.OnScbkEstablished(result, token).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Pairing key persistence callback threw.");
            return false;
        }
    }

    private void QueuePairingResponse(byte[] responseMessage)
    {
        var total = (ushort)responseMessage.Length;
        var offset = 0;
        while (offset < responseMessage.Length)
        {
            var fragmentSize = Math.Min(PairingReplyFragmentSize, responseMessage.Length - offset);
            var fragment = new byte[fragmentSize];
            Array.Copy(responseMessage, offset, fragment, 0, fragmentSize);
            EnqueuePollReply(new PairData(total, (ushort)offset, fragment));
            offset += fragmentSize;
        }
    }

    private void UpdateDeviceConfig(Action<DeviceConfiguration> updateAction, bool resetConnection = false)
    {
        var configCopy = _deviceConfiguration.Clone();
        updateAction(configCopy);
        _deviceConfiguration = configCopy;

        if (resetConnection)
        {
            Interlocked.Add(ref _connectionContextCounter, 1);
        }
    }
    
    /// <summary>
    /// Event arguments for DeviceComSetUpdated event, which is raised whenever ACU
    /// requests the device to update its address and/or baud rate
    /// </summary>
    public class DeviceComSetUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// Old address value
        /// </summary>
        public byte OldAddress { get; set; }

        /// <summary>
        /// New address value
        /// </summary>
        public byte NewAddress { get; set; }

        /// <summary>
        /// Old baud rate 
        /// </summary>
        public int OldBaudRate {  get; set; }

        /// <summary>
        /// New baud rate
        /// </summary>
        public int NewBaudRate { get; set; }
    }
}


/// <summary>
/// Represents a set of configuration options to be used when initializing
/// a new instance of the Device class
/// </summary>
public class DeviceConfiguration : ICloneable
{
    /// <summary>
    /// Creates a new DeviceConfiguration with the required client identification.
    /// </summary>
    /// <param name="identification">Client identification used during secure channel establishment</param>
    public DeviceConfiguration(ClientIdentification identification)
    {
        Identification = identification;
    }

    /// <summary>
    /// Address the device is assigned
    /// </summary>
    public byte Address { get; set; }

    /// <summary>
    /// Indicates whether the device will require an establishment of a secure
    /// channel. When this value is 'true', PD will be initialized with SCBK (non-default
    /// SecurityKey) in full-security mode; or with SCBK_D in "installation
    /// mode" if SecurityKey is not set to a non-default installation value.
    /// </summary>
    public bool RequireSecurity { get; set; } = true;

    /// <summary>
    /// Security Key if one was previously set via osdp_KeySet command or some
    /// other out-of-band means
    /// </summary>
    public byte[] SecurityKey { get; set; } = SecurityContext.DefaultKey;

    /// <summary>
    /// The secure channel version to use (V1 or V2).
    /// SC2 requires a 32-byte key.
    /// </summary>
    public SecureChannelVersion SecureChannelVersion { get; set; } = SecureChannelVersion.V1;

    /// <summary>
    /// List of commands the PD will allow to be sent unsecured when a device is operating
    /// in "Full Security" mode as defined by the OSDP spec. NOTE: per the OSDP committee's
    /// decision, by default, this list will include IdReport, DeviceCapabilities and CommSet
    /// commands, but a PD manufacturer can use this property to override that default
    /// </summary>
    public CommandType[] AllowUnsecured { get; set; } =
    [
        CommandType.IdReport, CommandType.DeviceCapabilities, CommandType.CommunicationSet
    ];

    /// <summary>
    /// Optional asymmetric pairing configuration. When set, the device accepts the cleartext
    /// osdp_PAIR exchange and, on success, derives a 32-byte SCBK for SC2. When left null (the
    /// default), the device behaves as a symmetric-only device and rejects pairing commands, so
    /// pre-shared-key SC2 (and SC1) deployments are unaffected.
    /// </summary>
    public Pairing.PairingConfiguration Pairing { get; set; }

    /// <summary>
    /// Client identification (cUID) used during secure channel establishment.
    /// Composed of vendor code (3 bytes) and serial number (4 bytes).
    /// This is required for OSDP secure channel compliance.
    /// See OSDP specification for osdp_CCRYPT response format.
    /// </summary>
    public ClientIdentification Identification { get; }

    /// <summary>
    /// Creates a new object that is a copy of the current instance
    /// </summary>
    public DeviceConfiguration Clone() => (DeviceConfiguration)this.MemberwiseClone();

    /// <inheritdoc/>
    object ICloneable.Clone() => Clone();
}


