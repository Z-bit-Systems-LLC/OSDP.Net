﻿using System;
using System.Collections.Concurrent;
using System.Linq;
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
    private IOsdpServer _osdpServer;
    private DateTime _lastValidReceivedCommand = DateTime.MinValue;

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
    public bool IsConnected => _osdpServer?.ConnectionCount > 0 && (
        _lastValidReceivedCommand + TimeSpan.FromSeconds(8) >= DateTime.UtcNow);

    /// <summary>
    /// Gets raised whenever osdp_ComSet command is successfully processed and there is 
    /// a change in either device address or baud rate. Because baud rate is configured on
    /// the OSDP connection/server that is passed down into the Device class, it is up to
    /// the consumer of the Device class (i.e. whatever code that creates that class in the
    /// first place) to properly handle this event, and re-initialize the Device with the
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
    /// Starts listening for commands from the OSDP device through the specified connection.
    /// </summary>
    /// <param name="server">The I/O server used for communication with the OSDP client.</param>
    public async void StartListening(IOsdpServer server)
    {
        _osdpServer = server ?? throw new ArgumentNullException(nameof(server));
        await _osdpServer.Start(ClientListenLoop);
    }

    private async Task ClientListenLoop(IOsdpConnection incomingConnection)
    {
        try
        {
            var currentContextCount = _connectionContextCounter;
            var channel = new PdMessageSecureChannel(
                incomingConnection, _deviceConfiguration.SecurityKey, loggerFactory: _loggerFactory)
            { 
                Address = _deviceConfiguration.Address,
                SecurityMode = !_deviceConfiguration.RequireSecurity
                    ? SecurityMode.Unsecured
                    : (_deviceConfiguration.SecurityKey == null ||
                       _deviceConfiguration.SecurityKey.SequenceEqual(SecurityContext.DefaultKey))
                    ? SecurityMode.InstallMode
                    : SecurityMode.FullSecurity,
                AllowUnsecured = _deviceConfiguration.AllowUnsecured ?? [],
            };

            while (incomingConnection.IsOpen)
            {
                var command = await channel.ReadNextCommand();

                if (command == null) continue;

                var reply = HandleCommand(command);
                await channel.SendReply(reply);

                if (currentContextCount != _connectionContextCounter)
                {
                    _logger?.LogInformation("Interruping existing connection due to 'force disconnect' flag");
                    break;
                }
            }
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

    /// <summary>
    /// Stops listening for OSDP messages on the device.
    /// </summary>
    public async Task StopListening()
    {
        await (_osdpServer?.Stop() ?? Task.CompletedTask);
        _osdpServer = null;
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

        return new OutgoingReply(command, (CommandType)command.Type switch
        {
            CommandType.Poll => HandlePoll(),
            CommandType.IdReport => HandleIdReport(),
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
            CommandType.Abort => HandleAbortRequest(),
            CommandType.PivData => HandlePivData(GetPIVData.ParseData(command.Payload)),
            CommandType.KeepActive => HandleKeepActive(KeepReaderActive.ParseData(command.Payload)),
            _ => HandleUnknownCommand(command)
        });
    }

    private PayloadData HandlePoll()
    {
        return _pendingPollReplies.TryDequeue(out var reply) ? reply : new Ack();
    }

    /// <summary>
    /// Handles the ID Report Request command received from the OSDP device.
    /// </summary>
    /// <returns></returns>
    protected virtual PayloadData HandleIdReport()
    {
        return HandleUnknownCommand(CommandType.IdReport);
    }

    /// <summary>
    /// Handles the text output command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming reader text output command payload.</param>
    /// <returns></returns>
    protected virtual PayloadData HandleTextOutput(ReaderTextOutput commandPayload)
    {
        return HandleUnknownCommand(CommandType.TextOutput);
    }

    /// <summary>
    /// Handles the reader buzzer control command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming reader buzzer control command payload.</param>
    /// <returns></returns>
    protected virtual PayloadData HandleBuzzerControl(ReaderBuzzerControl commandPayload)
    {
        return HandleUnknownCommand(CommandType.BuzzerControl);
    }

    /// <summary>
    /// Handles the output controls command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming output controls command payload.</param>
    /// <returns></returns>
    protected virtual PayloadData HandleOutputControl(OutputControls commandPayload)
    {
        return HandleUnknownCommand(CommandType.OutputControl);
    }

    /// <summary>
    /// Handles the output control command received from the OSDP device.
    /// </summary>
    /// <returns></returns>
    protected virtual PayloadData HandleDeviceCapabilities()
    {
        return HandleUnknownCommand(CommandType.DeviceCapabilities);
    }

    /// <summary>
    /// Handles the get PIV data command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming get PIV data command payload.</param>
    /// <returns></returns>
    protected virtual PayloadData HandlePivData(GetPIVData commandPayload)
    {
        return HandleUnknownCommand(CommandType.PivData);
    }

    /// <summary>
    /// Handles the manufacture command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming manufacture command payload.</param>
    /// <returns></returns>
    protected virtual PayloadData HandleManufacturerCommand(ManufacturerSpecific commandPayload)
    {
        return HandleUnknownCommand(CommandType.ManufacturerSpecific);
    }

    /// <summary>
    /// Handles the keep active command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The incoming keep active command payload.</param>
    /// <returns></returns>
    protected virtual PayloadData HandleKeepActive(KeepReaderActive commandPayload)
    {
        return HandleUnknownCommand(CommandType.KeepActive);
    }

    /// <summary>
    /// Handles the abort request command received from the OSDP device.
    /// </summary>
    /// <returns></returns>
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
    /// Handles the maximum ACU maximum receive size command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The ACU maximum receive size command payload.</param>
    /// <returns></returns>
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
    /// this method in order to provide its own means of persisting a newly set security key which
    /// which was sent by the ACU. The base `Device` class will automatically pick up the new key
    /// for future connections if this function returns successful Ack response.
    /// NOTE: Any existing connections will continue to use the previous key. It is up to the
    /// ACU to drop connection and reconnect if it wishes to do so
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
    /// <returns></returns>
    protected virtual PayloadData HandleBiometricMatch(BiometricTemplateData commandPayload)
    {
        return HandleUnknownCommand(CommandType.BioMatch);
    }

    /// <summary>
    /// Handles the biometric match command received from the OSDP device.
    /// </summary>
    /// <param name="commandPayload">The biometric match command payload.</param>
    /// <returns></returns>
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
            var previousBaudRate = _osdpServer.BaudRate;

            if (previousAddress != config.Address)
            {
                UpdateDeviceConfig(c => c.Address = config.Address);
            }
            
            if (previousBaudRate != config.BaudRate || previousAddress != config.Address)
            {
                var updatedEvent = DeviceComSetUpdated;
                if (updatedEvent != null) 
                {
                    // Decouple current call stack from the event invocation which could result
                    // in the event subscriber resetting the entire connection so that the current
                    // command has a chance to run to completion and we don't have any deadlock
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
    /// this method in order to provide its own means of persisting a new baud rate and address which
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
    /// <param name="commandPayload">The reader LED controls command payload.</param>
    /// <returns></returns>
    protected virtual PayloadData HandleReaderLEDControl(ReaderLedControls commandPayload)
    {
        return HandleUnknownCommand(CommandType.LEDControl);
    }

    /// <summary>
    /// Handles the reader status command received from the OSDP device.
    /// </summary>
    /// <returns></returns>
    protected virtual PayloadData HandleReaderStatusReport()
    {
        return HandleUnknownCommand(CommandType.ReaderStatus);
    }

    /// <summary>
    /// Handles the output status command received from the OSDP device.
    /// </summary>
    /// <returns></returns>
    protected virtual PayloadData HandleOutputStatusReport()
    {
        return HandleUnknownCommand(CommandType.OutputStatus);
    }

    /// <summary>
    /// Handles the input status command received from the OSDP device.
    /// </summary>
    /// <returns></returns>
    protected virtual PayloadData HandleInputStatusReport()
    {
        return HandleUnknownCommand(CommandType.InputStatus);
    }

    /// <summary>
    /// Handles the reader local status command received from the OSDP device.
    /// </summary>
    /// <returns></returns>
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
}


/// <summary>
/// Represents a set of configuration options to be used when initializating 
/// a new instance of the Device class
/// </summary>
public class DeviceConfiguration : ICloneable
{
    /// <summary>
    /// Address the device is assigned 
    /// </summary>
    public byte Address { get; set; }

    /// <summary>
    /// Indicates whether or not device will require establishment of a secure
    /// channel. When this value is 'true', PD will be initialized with SCBK (non-default 
    /// SecurityKey) in full-security mode; or with SCBK_D in "installation
    /// mode" if SecurityKey is not set to non-default installation value.
    /// </summary>
    public bool RequireSecurity { get; set; } = true;

    /// <summary>
    /// Security Key if one was previously set via osdp_KeySet command or some
    /// other out-of-band means
    /// </summary>
    public byte[] SecurityKey { get; set; } = SecurityContext.DefaultKey;

    /// <summary>
    /// List of commands the PD will allow to be sent unsecured when device is operating
    /// in "Full Security" mode as defined by the OSDP spec. NOTE: per OSDP committee's
    /// decision by default this list will include IdReport, DeviceCapabilities and CommSet
    /// commands, but PD manufacturer can use this property to override that default
    /// </summary>
    public CommandType[] AllowUnsecured { get; set; } = [
        CommandType.IdReport, CommandType.DeviceCapabilities, CommandType.CommunicationSet];

    /// <summary>
    /// Creates a new object that is a copy of the current instance
    /// </summary>
    public DeviceConfiguration Clone() => (DeviceConfiguration)this.MemberwiseClone();

    /// <inheritdoc/>
    object ICloneable.Clone() => this.Clone();
}

/// <summary>
/// Event arguments for DevicecomSetUpdated event which is raised whenever ACU
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
