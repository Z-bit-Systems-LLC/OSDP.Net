using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OSDP.Net.Connections;
using OSDP.Net.Messages;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.PanelCommands.DeviceDiscover;
using OSDP.Net.Tracing;
using CommunicationConfiguration = OSDP.Net.Model.CommandData.CommunicationConfiguration;
using DeviceCapabilities = OSDP.Net.Model.ReplyData.DeviceCapabilities;
using ManufacturerSpecific = OSDP.Net.Model.ReplyData.ManufacturerSpecific;

namespace OSDP.Net
{
    /// <summary>
    /// The OSDP control panel used to communicate to Peripheral Devices (PDs) as an Access Control Unit (ACU).
    /// If multiple connections are needed, add them to the control panel. Avoid creating multiple control panel objects.
    /// </summary>
    public class ControlPanel
    {
        internal const byte ConfigurationAddress = 0x7f;

        private readonly object _lockBusCreation = new ();
        private readonly ConcurrentDictionary<Guid, Bus> _buses = new();
        private readonly ILogger<ControlPanel> _logger;
        private readonly BlockingCollection<ReplyTracker> _replies = new();
        private TimeSpan _replyResponseTimeout = TimeSpan.FromSeconds(8);
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _requestLocks = new();
        private readonly TimeSpan _timeToWaitToCheckOnData = TimeSpan.FromMilliseconds(10);
        private readonly IDeviceProxyFactory _deviceProxyFactory;

        /// <summary>Initializes a new instance of the <see cref="T:OSDP.Net.ControlPanel" /> class.</summary>
        /// <param name="logger">The logger definition used for logging.</param>
        [Obsolete("Sending a ILogger is deprecated, please send ILoggerFactory instead.")]
        public ControlPanel(ILogger<ControlPanel> logger = null) : this(null, logger) { }

        /// <summary>Initializes a new instance of the <see cref="T:OSDP.Net.ControlPanel" /> class.</summary>
        /// <param name="loggerFactory">The logger factory used to create logging facilities.</param>
        public ControlPanel(ILoggerFactory loggerFactory = null) : this(null, loggerFactory) { }

        internal ControlPanel(IDeviceProxyFactory deviceProxyFactory, ILogger<ControlPanel> logger = null)
        {
            _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<ControlPanel>();
            _deviceProxyFactory = deviceProxyFactory ?? new DeviceProxyFactory();

            Task.Factory.StartNew(() =>
            {
                foreach (var reply in _replies.GetConsumingEnumerable())
                {
                    OnReplyReceived(reply);
                }
            }, TaskCreationOptions.LongRunning);
        }

        internal ControlPanel(IDeviceProxyFactory deviceProxyFactory, ILoggerFactory loggerFactory = null)
        {
            _logger = loggerFactory != null
                ? loggerFactory.CreateLogger<ControlPanel>()
                : NullLoggerFactory.Instance.CreateLogger<ControlPanel>();
            _deviceProxyFactory = deviceProxyFactory ?? new DeviceProxyFactory();

            Task.Factory.StartNew(() =>
            {
                foreach (var reply in _replies.GetConsumingEnumerable())
                {
                    OnReplyReceived(reply);
                }
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Start polling on the defined connection.
        /// </summary>
        /// <param name="connection">This represents the type of connection used for communicating to PDs.</param>
        /// <returns>An identifier that represents the connection</returns>
        public Guid StartConnection(IOsdpConnection connection) => StartConnection(connection, Bus.DefaultPollInterval);

        /// <summary>
        /// Start polling on the defined connection.
        /// </summary>
        /// <param name="connection">This represents the type of connection used for communicating to PDs.</param>
        /// <param name="pollInterval">The interval at which the devices will be polled, zero or less indicates no polling</param>
        /// <param name="isTracing">Write packet data to {Bus ID}.osdpcap file</param>
        /// <returns>An identifier that represents the connection</returns>
        public Guid StartConnection(IOsdpConnection connection, TimeSpan pollInterval, bool isTracing) =>
            StartConnection(connection, pollInterval, isTracing ? OSDPFileCapTracer.Trace : _ => { });

        /// <summary>
        /// Start polling on the defined connection.
        /// </summary>
        /// <param name="connection">This represents the type of connection used for communicating to PDs.</param>
        /// <param name="pollInterval">The interval at which the devices will be polled, zero or less indicates no polling</param>
        /// <returns>An identifier that represents the connection</returns>
        public Guid StartConnection(IOsdpConnection connection, TimeSpan pollInterval) =>
            StartConnection(connection, pollInterval, _ => { });

        /// <summary>
        /// Start polling on the defined connection.
        /// </summary>
        /// <param name="connection">
        /// This represents the type of connection used for communicating to PDs.
        /// The connection instance may only be started once, otherwise an InvalidOperationException is thrown.
        /// </param>
        /// <param name="pollInterval">The interval at which the devices will be polled, zero or less indicates no polling</param>
        /// <param name="tracer">Delegate that will receive detailed trace information</param>
        /// <returns>An identifier that represents the connection</returns>
        /// <exception cref="InvalidOperationException">If the connection is already started.</exception>
        public Guid StartConnection(IOsdpConnection connection, TimeSpan pollInterval, Action<TraceEntry> tracer)
        {
            var newBus = CreateBus(connection, pollInterval, tracer);
            newBus.ConnectionStatusChanged += BusOnConnectionStatusChanged;
            newBus.StartPolling();
            return newBus.Id;
        }

        /// <summary>
        /// Create a bus for a connection.
        /// </summary>
        /// <remarks>
        /// This will serialize concurrent attempts to start simultaneous connections. Only the first will succeed.
        /// </remarks>
        private Bus CreateBus(IOsdpConnection connection, TimeSpan pollInterval, Action<TraceEntry> tracer)
        {
            // Lock while we check/create the bus. This is a very quick operation so the lock is not a performance problem.
            lock (_lockBusCreation)
            {
                var existingBusWithThisConnection = _buses.Values
                    .FirstOrDefault(bus => bus.Connection == connection);
                if (existingBusWithThisConnection != null)
                    throw new InvalidOperationException(
                        $"The IOsdpConnection is already active in connection {existingBusWithThisConnection.Id}. " +
                        "That connection must be stopped before starting a new one.");

                var newBus = new Bus(
                    connection,
                    _replies,
                    pollInterval,
                    tracer,
                    _deviceProxyFactory,
                    _logger);

                _buses[newBus.Id] = newBus;
                return newBus;
            }
        }

        /// <summary>
        /// Stop the bus for a specific connection.
        /// </summary>
        /// <param name="connectionId">The identifier that represents the connection.</param>
        public async Task StopConnection(Guid connectionId)
        {
            if (!_buses.TryGetValue(connectionId, out Bus bus))
            {
                return;
            }

            // At this point, there might be multiple threads running concurrently trying to stop the connection.
            // No worries, the method is reentrant. All threads will complete safely.

            bus.ConnectionStatusChanged -= BusOnConnectionStatusChanged;
            await bus.Close().ConfigureAwait(false);

            foreach (byte address in bus.ConfigureDeviceAddresses)
            {
                // This will fire twice if two threads call StopConnection for the same connection simultaneously.
                OnConnectionStatusChanged(bus.Id, address, false, false);
                _requestLocks.TryRemove(new { connectionId, address }.GetHashCode(), out _);
            }
            bus.Dispose();
            _buses.TryRemove(connectionId, out bus);
        }

        private void BusOnConnectionStatusChanged(object sender, Bus.ConnectionStatusEventArgs eventArgs)
        {
            if (sender is Bus bus)
                OnConnectionStatusChanged(bus.Id, eventArgs.Address, eventArgs.IsConnected,
                    eventArgs.IsSecureSessionEstablished);
        }

        /// <summary>
        /// Send a custom command for testing.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="command">The custom command to send.</param>
        public async Task SendCustomCommand(Guid connectionId, byte address, CommandData command)
        {
            await SendCommand(connectionId, address, command).ConfigureAwait(false);
        }

        /// <summary>Request to get an ID Report from the PD.</summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <returns>ID report reply data that was requested.</returns>
        public async Task<DeviceIdentification> IdReport(Guid connectionId, byte address) => 
            await IdReport(connectionId, address, default).ConfigureAwait(false);

        /// <summary>Request to get an ID Report from the PD.</summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="cancellationToken">The CancellationToken token to observe.</param>
        /// <returns>ID report reply data that was requested.</returns>
        public async Task<DeviceIdentification> IdReport(Guid connectionId, byte address, CancellationToken cancellationToken)
        {
            var message = await SendCommand(
                connectionId,
                address,
                new IdReport(),
                cancellationToken).ConfigureAwait(false);
            
            return DeviceIdentification.ParseData(message.Payload);
        }

        /// <summary>Request to get the capabilities of the PD.</summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <returns>Device capabilities reply data that was requested.</returns>
        public async Task<DeviceCapabilities> DeviceCapabilities(Guid connectionId, byte address)
        {
            return Model.ReplyData.DeviceCapabilities.ParseData((await SendCommand(
                connectionId,
                address,
                new Model.CommandData.DeviceCapabilities()).ConfigureAwait(false)).Payload);
        }

        /// <summary>
        /// Request to get the local status of a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <returns>Device local status reply data that was requested.</returns>
        public async Task<LocalStatus> LocalStatus(Guid connectionId, byte address)
        {
            return Model.ReplyData.LocalStatus.ParseData((await SendCommand(
                connectionId,
                address,
                new NoPayloadCommandData(CommandType.LocalStatus)).ConfigureAwait(false)).Payload);
        }

        /// <summary>
        /// Request to get the status all of the inputs of a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <returns>Device input status reply data that was requested.</returns>
        public async Task<InputStatus> InputStatus(Guid connectionId, byte address)
        {
            return Model.ReplyData.InputStatus.ParseData((await SendCommand(
                connectionId,
                address,
                new NoPayloadCommandData(CommandType.InputStatus)).ConfigureAwait(false)).Payload);
        }

        /// <summary>
        /// Request to get the status all of the outputs of a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <returns>Device output status reply data that was requested.</returns>
        public async Task<OutputStatus> OutputStatus(Guid connectionId, byte address)
        {
            return Model.ReplyData.OutputStatus.ParseData((await SendCommand(
                connectionId,
                address,
                new NoPayloadCommandData(CommandType.OutputStatus)).ConfigureAwait(false)).Payload);
        }

        /// <summary>
        /// Request to get PIV data from PD. Only one PIV request is processed at a time for each connection. 
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="getPIVData">Describe the PIV data to retrieve.</param>
        /// <param name="timeout">A TimeSpan that represents time to wait until waiting for the other requests to complete and it's own request, a TimeSpan that represents -1 milliseconds to wait indefinitely, or a TimeSpan that represents 0 milliseconds to return immediately when there is a request being processed.</param>
        /// <param name="cancellationToken">The CancellationToken token to observe.</param>
        /// <returns>A response with the PIV data requested.</returns>
        public async Task<byte[]> GetPIVData(Guid connectionId, byte address, GetPIVData getPIVData, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var requestLock = GetRequestLock(connectionId, address);
            
            if (!await requestLock.WaitAsync(timeout, cancellationToken))
            {
                throw new TimeoutException("Timeout waiting for another request to complete.");
            }
            
            try
            {
                return await WaitForPIVData(connectionId, address, getPIVData, timeout, cancellationToken);
            }
            finally
            {
                requestLock.Release();
            }
        }

        private SemaphoreSlim GetRequestLock(Guid connectionId, byte address)
        {
            int hash = new { connectionId, address }.GetHashCode();
            
            if (_requestLocks.TryGetValue(hash, out var requestLock))
            {
                return requestLock;
            }

            var newRequestLock = new SemaphoreSlim(1, 1);
            _requestLocks[hash] = newRequestLock;
            return newRequestLock;
        }

        private async Task<byte[]> WaitForPIVData(Guid connectionId, byte address, GetPIVData getPIVData, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            bool complete = false;
            byte[] data = null;

            void Handler(object sender, MultiPartMessageDataReplyEventArgs args)
            {
                // Only process matching replies
                if (args.ConnectionId != connectionId || args.Address != address) return;

                var pivData = args.DataFragmentResponse;
                data ??= new byte[pivData.WholeMessageLength];

                complete = Message.BuildMultiPartMessageData(pivData.WholeMessageLength, pivData.Offset,
                    pivData.LengthOfFragment, pivData.Data, data);
            }

            PIVDataReplyReceived += Handler;
            SetReceivingMultipartMessaging(connectionId, address, true);
            
            try
            {
                await SendCommand(connectionId, address, getPIVData, cancellationToken, throwOnNak: false)
                    .ConfigureAwait(false);

                DateTime endTime = DateTime.UtcNow + timeout;
                
                while (DateTime.UtcNow <= endTime)
                {
                    if (complete)
                    {
                        return data;
                    }

                    await Task.Delay(_timeToWaitToCheckOnData, cancellationToken);
                }

                throw new TimeoutException("Timeout waiting to receive PIV data.");
            }
            finally
            {
                PIVDataReplyReceived -= Handler;
                SetReceivingMultipartMessaging(connectionId, address, false);
            }
        }

        /// <summary>
        /// Request to get the status all of the readers of a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <returns>Device reader status reply data that was requested.</returns>
        public async Task<ReaderStatus> ReaderStatus(Guid connectionId, byte address)
        {
            return Model.ReplyData.ReaderStatus.ParseData((await SendCommand(connectionId, address,
                new NoPayloadCommandData(CommandType.ReaderStatus)).ConfigureAwait(false)).Payload);
        }

        /// <summary>
        /// Sends a manufacture specific command to a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="manufacturerSpecific">The manufacturer specific data.</param>
        /// <returns>Reply data that is returned after sending the command. There is the possibility of different replies can be returned from PD.</returns>
        public async Task<ReturnReplyData<ManufacturerSpecific>> ManufacturerSpecificCommand(Guid connectionId,
            byte address,
            Model.CommandData.ManufacturerSpecific manufacturerSpecific)
        {
            var reply = await SendCommand(connectionId, address, manufacturerSpecific).ConfigureAwait(false);

            return new ReturnReplyData<ManufacturerSpecific>
            {
                Ack = reply.Type == (byte)ReplyType.Ack,
                ReplyData = reply.Type == (byte)ReplyType.ManufactureSpecific
                    ? ManufacturerSpecific.ParseData(reply.Payload)
                    : null
            };
        }

        /// <summary>
        /// Sends a manufacturer specific multi-part command to a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="manufacturerSpecific">Manufacturer specific command data with a command code as the first byte.</param>
        /// <param name="maximumFragmentSize">The maximum size of the packet fragment.</param>
        /// <param name="timeout">A TimeSpan that represents time to wait until waiting for the other requests to complete and it's own request, a TimeSpan that represents -1 milliseconds to wait indefinitely, or a TimeSpan that represents 0 milliseconds to return immediately when there is a request being processed.</param>
        /// <param name="cancellationToken">The CancellationToken token to observe.</param>
        /// <returns>The last reply data that is returned after sending all the command fragments. There is the possibility of different replies can be returned from PD.</returns>
        public async Task<ReturnReplyData<ManufacturerSpecific>> ManufacturerSpecificCommand(Guid connectionId, byte address,
            Model.CommandData.ManufacturerSpecific manufacturerSpecific, ushort maximumFragmentSize, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var requestLock = GetRequestLock(connectionId, address);

            if (!await requestLock.WaitAsync(timeout, cancellationToken))
            {
                throw new TimeoutException("Timeout waiting for another request to complete.");
            }

            try
            {
                return await WaitForManufactureResponse(connectionId, address, manufacturerSpecific, maximumFragmentSize,
                    cancellationToken);
            }
            finally
            {
                requestLock.Release();
            }
        }

        private async Task<ReturnReplyData<ManufacturerSpecific>> WaitForManufactureResponse(Guid connectionId, byte address,
            Model.CommandData.ManufacturerSpecific manufacturerSpecific, ushort maximumFragmentSize, CancellationToken cancellationToken)
        {
            SetReceivingMultipartMessaging(connectionId, address, true);

            var manufactureCommandCode = manufacturerSpecific.Data.Skip(0).Take(1).ToArray();
            var manufactureCommandData = manufacturerSpecific.Data.Skip(1).ToArray();
            ushort totalSize = (ushort)manufactureCommandData.Length;
            ushort offset = 0;
            bool continueTransfer = true;
            IncomingMessage reply = null;

            try
            {
                while (!cancellationToken.IsCancellationRequested && continueTransfer)
                {
                    ushort fragmentSize = (ushort)Math.Min(maximumFragmentSize, totalSize - offset);
                    reply = await SendCommand(connectionId, address, new Model.CommandData.ManufacturerSpecific(
                            manufacturerSpecific.VendorCode, manufactureCommandCode.Concat(
                                new MessageDataFragment(totalSize, offset, fragmentSize,
                                        manufactureCommandData.Skip(offset).Take(fragmentSize).ToArray()).BuildData()
                                    .ToArray()).ToArray()), cancellationToken)
                        .ConfigureAwait(false);

                    if (reply.Type != (byte)ReplyType.Ack) break;

                    offset += maximumFragmentSize;

                    // Determine if we should continue on successful status
                    continueTransfer = offset < totalSize;
                }

                if (reply == null)
                {
                    throw new Exception("Command didn't return a valid reply");
                }
                
                return new ReturnReplyData<ManufacturerSpecific>
                {
                    Ack = reply.Type == (byte)ReplyType.Ack,
                    ReplyData = reply.Type == (byte)ReplyType.ManufactureSpecific ? ManufacturerSpecific.ParseData(reply.Payload) : null
                };
            }
            finally
            {
                SetReceivingMultipartMessaging(connectionId, address, false);
            }
        }

        /// <summary>
        /// Send a command to alter the state of one or more outputs on a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="outputControls">Data for one or more outputs to control.</param>
        /// <returns>Reply data that is returned after sending the command. There is the possibility of different replies can be returned from PD.</returns>
        public async Task<ReturnReplyData<OutputStatus>> OutputControl(Guid connectionId, byte address,
            OutputControls outputControls)
        {
            var reply = await SendCommand(connectionId, address, outputControls).ConfigureAwait(false);

            return new ReturnReplyData<OutputStatus>
            {
                Ack = reply.Type == (byte)ReplyType.Ack,
                ReplyData = reply.Type == (byte)ReplyType.OutputStatusReport
                    ? Model.ReplyData.OutputStatus.ParseData(reply.Payload)
                    : null
            };
        }

        /// <summary>
        /// Send a command to alter the LED color on a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="readerLedControls">Data to change color on one or more reader LEDs.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> ReaderLedControl(Guid connectionId, byte address, ReaderLedControls readerLedControls)
        {
            var reply = await SendCommand(connectionId, address, readerLedControls).ConfigureAwait(false);

            return reply.Type == (byte)ReplyType.Ack;
        }

        /// <summary>
        /// Send a command to control the buzzer on a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="readerBuzzerControl">Data for the reader buzzer control on the PD.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> ReaderBuzzerControl(Guid connectionId, byte address,
            ReaderBuzzerControl readerBuzzerControl)
        {
            var reply = await SendCommand(connectionId, address, readerBuzzerControl).ConfigureAwait(false);

            return reply.Type == (byte)ReplyType.Ack;
        }

        /// <summary>
        /// Send a command that sends text to be shown on a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="readerTextOutput">Data for the reader text output on the PD.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> ReaderTextOutput(Guid connectionId, byte address, ReaderTextOutput readerTextOutput)
        {
            var reply = await SendCommand(connectionId, address, readerTextOutput).ConfigureAwait(false);
            
            return reply.Type == (byte)ReplyType.Ack;
        }

        /// <summary>
        /// Send a command that sets the communication configuration on a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="communicationConfiguration">Data for the communication configuration on the PD.</param>
        /// <returns>Reply data of the actual communication configuration being set on the PD.</returns>
        public async Task<Model.ReplyData.CommunicationConfiguration> CommunicationConfiguration(Guid connectionId,
            byte address, CommunicationConfiguration communicationConfiguration)
        {
            if (_buses.Any(bus =>
                    bus.Key == connectionId &&
                    address != communicationConfiguration.Address &&
                    bus.Value.ConfigureDeviceAddresses.Any(configuredAddress =>
                        configuredAddress == communicationConfiguration.Address)))
            {
                throw new Exception("Address is already configured on the bus.");
            }

            return Model.ReplyData.CommunicationConfiguration.ParseData(
                (await SendCommand(connectionId, address, communicationConfiguration).ConfigureAwait(false))
                .Payload);
        }

        /// <summary>
        ///  Send a command that sets the encryption key configuration on a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="encryptionKeyConfiguration">Data for the encryption key configuration on the PD.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> EncryptionKeySet(Guid connectionId, byte address,
            EncryptionKeyConfiguration encryptionKeyConfiguration)
        {
            var reply = await SendCommand(connectionId, address, encryptionKeyConfiguration).ConfigureAwait(false);

            return reply.Type == (byte)ReplyType.Ack;
        }

        /// <summary>
        /// Send a file to a PD.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="fileType">File transfer type</param>
        /// <param name="fileData">The complete file data being sent to the PD</param>
        /// <param name="fragmentSize">Initial size of the fragment sent with each packet</param>
        /// <param name="callback">Track the status of the file transfer</param>
        /// <param name="cancellationToken">The CancellationToken token to observe.</param>
        /// <exception cref="FileTransferException">File transfer has failed because the PD has returned an error.</exception>
        /// <returns>The final successful (=positive) status code from the PD.</returns>
        public Task<Model.ReplyData.FileTransferStatus.StatusDetail> FileTransfer(Guid connectionId, byte address, byte fileType, byte[] fileData, ushort fragmentSize,
            Action<FileTransferStatus> callback, CancellationToken cancellationToken = default)
        {
            var bus = _buses[connectionId];
            return Task.Run(async () =>
            {
                bus.SetSendingMultipartMessage(address, true);
                try
                {
                    return await SendFileTransferCommands(connectionId, address, fileType, fileData, fragmentSize, callback,
                        cancellationToken);
                }
                finally
                {
                    bus.SetSendingMultipartMessage(address, false);
                    bus.SetSendingMultiMessageNoSecureChannel(address, false);
                }
            }, cancellationToken);
        }

        private async Task<Model.ReplyData.FileTransferStatus.StatusDetail> SendFileTransferCommands(Guid connectionId,
            byte address, byte fileType, byte[] fileData,
            ushort fragmentSize, Action<FileTransferStatus> callback, CancellationToken cancellationToken)
        {
            int totalSize = fileData.Length;
            int offset = 0;
            bool isFirstMessage = true;

            // Keep going until
            // * operation's cancelled
            // * an error occurs
            // * transfer's completed
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Get the fragment size if it doesn't exceed the total size
                var nextFragmentSize = (ushort)Math.Min(fragmentSize, totalSize - offset);

                var reply = await SendCommand(connectionId, address,
            new FileTransferFragment(fileType,
                new MessageDataFragment(totalSize, offset, nextFragmentSize,
                    fileData.Skip(offset).Take(nextFragmentSize).ToArray())),
                        cancellationToken, throwOnNak: false)
                    .ConfigureAwait(false);

                // On the first message, calculate the proper fragment size based on whether encryption is being used
                if (isFirstMessage)
                {
                    isFirstMessage = false;
                    fragmentSize = Message.CalculateMaximumMessageSize(fragmentSize, reply.IsSecureMessage);
                }

                // Update offset
                offset += nextFragmentSize;

                // Parse the fileTransfer status
                var fileTransferStatusReply = reply.Type == (byte)ReplyType.FileTransferStatus
                    ? Model.ReplyData.FileTransferStatus.ParseData(reply.Payload)
                    : null;

                // Check reply to see if PD has requested some change in com procedures.
                if (fileTransferStatusReply != null)
                {
                    // Leave secure channel if needed
                    if ((fileTransferStatusReply.Action &
                         Model.ReplyData.FileTransferStatus.ControlFlags.LeaveSecureChannel) ==
                        Model.ReplyData.FileTransferStatus.ControlFlags.LeaveSecureChannel)
                    {
                        _buses[connectionId].SetSendingMultiMessageNoSecureChannel(address, true);
                    }

                    // Set request delay if specified
                    if (fileTransferStatusReply is { RequestedDelay: > 0 })
                    {
                        _buses[connectionId].SetRequestDelay(address,
                            DateTime.UtcNow.AddMilliseconds(fileTransferStatusReply.RequestedDelay));
                    }

                    // Set fragment size if requested
                    if (fileTransferStatusReply is { UpdateMessageMaximum: > 0 })
                    {
                        fragmentSize = Message.CalculateMaximumMessageSize(fileTransferStatusReply.UpdateMessageMaximum,
                            reply.IsSecureMessage);
                    }
                }

                var status = new FileTransferStatus(
                    fileTransferStatusReply?.Detail ?? Model.ReplyData.FileTransferStatus.StatusDetail.UnknownError,
                    offset,
                    reply.Type == (byte)ReplyType.Nak ? Nak.ParseData(reply.Payload) : null);

                // Report status to progress listeners
                callback(status);

                switch (status.Status)
                {
                    // Abort transfer on error.
                    case < 0:
                        throw new FileTransferException("File transfer failed", status);
                    // File transfer is completed, but PD wants us to keep sending "idling" file transfer message (FtFragmentSize = 0) until we receive another status.
                    case Model.ReplyData.FileTransferStatus.StatusDetail.FinishingFileTransfer:
                        fragmentSize = 0;
                        break;
                    default:
                    {
                        if (offset >= totalSize)
                        {
                            // We're done. Return the last successful (=positive) status code.
                            return status.Status;
                        }

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Send a request to the PD to read from a biometric scan and send back the data template.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="biometricReadData">Command data to send a request to the PD to send template data from a biometric scan.</param>
        /// <param name="timeout">A TimeSpan that represents time to wait until waiting for the other requests to complete and it's own request, a TimeSpan that represents -1 milliseconds to wait indefinitely, or a TimeSpan that represents 0 milliseconds to return immediately when there is a request being processed.</param>
        /// <param name="cancellationToken">The CancellationToken token to observe.</param>
        /// <returns>Results from matching the biometric scan.</returns>
        public async Task<BiometricReadResult> ScanAndSendBiometricData(Guid connectionId, byte address,
            BiometricReadData biometricReadData, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var requestLock = GetRequestLock(connectionId, address);

            if (!await requestLock.WaitAsync(timeout, cancellationToken))
            {
                throw new TimeoutException("Timeout waiting for another request to complete.");
            }

            try
            {
                return await WaitForBiometricRead(connectionId, address, biometricReadData, timeout,
                    cancellationToken);
            }
            finally
            {
                requestLock.Release();
            }
        }

        private async Task<BiometricReadResult> WaitForBiometricRead(Guid connectionId, byte address,
            BiometricReadData biometricReadData, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            bool complete = false;
            BiometricReadResult result = null;

            void Handler(object sender, BiometricReadResultsReplyEventArgs args)
            {
                // Only process matching replies
                if (args.ConnectionId != connectionId || args.Address != address) return;

                complete = true;

                result = args.BiometricReadResult;
            }

            BiometricReadResultsReplyReceived += Handler;
            SetReceivingMultipartMessaging(connectionId, address, true);

            try
            {
                await SendCommand(connectionId, address, biometricReadData, cancellationToken, throwOnNak: false)
                    .ConfigureAwait(false);

                DateTime endTime = DateTime.UtcNow + timeout;

                while (DateTime.UtcNow <= endTime)
                {
                    if (complete)
                    {
                        return result;
                    }

                    await Task.Delay(_timeToWaitToCheckOnData, cancellationToken);
                }

                throw new TimeoutException("Timeout waiting to for biometric read data.");
            }
            finally
            {
                BiometricReadResultsReplyReceived -= Handler;
                SetReceivingMultipartMessaging(connectionId, address, false);
            }
        }


        /// <summary>
        /// Send a request to the PD to perform a biometric scan and match it to the provided template.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="biometricTemplateData">Command data to send a request to the PD to perform a biometric scan and match.</param>
        /// <param name="timeout">A TimeSpan that represents time to wait until waiting for the other requests to complete and it's own request, a TimeSpan that represents -1 milliseconds to wait indefinitely, or a TimeSpan that represents 0 milliseconds to return immediately when there is a request being processed.</param>
        /// <param name="cancellationToken">The CancellationToken token to observe.</param>
        /// <returns>Results from matching the biometric scan.</returns>
        public async Task<BiometricMatchResult> ScanAndMatchBiometricTemplate(Guid connectionId, byte address,
            BiometricTemplateData biometricTemplateData, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var requestLock = GetRequestLock(connectionId, address);
            
            if (!await requestLock.WaitAsync(timeout, cancellationToken))
            {
                throw new TimeoutException("Timeout waiting for another request to complete.");
            }
            
            try
            {
                return await WaitForBiometricMatch(connectionId, address, biometricTemplateData, timeout, cancellationToken);
            }
            finally
            {
                requestLock.Release();
            }
        }

        private async Task<BiometricMatchResult> WaitForBiometricMatch(Guid connectionId, byte address,
            BiometricTemplateData biometricTemplateData, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            bool complete = false;
            BiometricMatchResult result = null;

            void Handler(object sender, BiometricMatchReplyEventArgs args)
            {
                // Only process matching replies
                if (args.ConnectionId != connectionId || args.Address != address) return;

                complete = true;

                result = args.BiometricMatchResult;
            }

            BiometricMatchReplyReceived += Handler;
            SetReceivingMultipartMessaging(connectionId, address, true);

            try
            {
                await SendCommand(connectionId, address, biometricTemplateData, cancellationToken, throwOnNak: false)
                    .ConfigureAwait(false);

                DateTime endTime = DateTime.UtcNow + timeout;

                while (DateTime.UtcNow <= endTime)
                {
                    if (complete)
                    {
                        return result;
                    }

                    await Task.Delay(_timeToWaitToCheckOnData, cancellationToken);
                }

                throw new TimeoutException("Timeout waiting to for biometric match.");
            }
            finally
            {
                BiometricMatchReplyReceived -= Handler;
                SetReceivingMultipartMessaging(connectionId, address, false);
            }
        }

        /// <summary>
        /// Instruct the PD to perform a challenge/response sequence.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="algorithm"></param>
        /// <param name="key"></param>
        /// <param name="challenge"></param>
        /// <param name="maximumFragmentSize">The maximum size of the packet fragment.</param>
        /// <param name="timeout">A TimeSpan that represents time to wait until waiting for the other requests to complete and it's own request, a TimeSpan that represents -1 milliseconds to wait indefinitely, or a TimeSpan that represents 0 milliseconds to return immediately when there is a request being processed.</param>
        /// <param name="cancellationToken">The CancellationToken token to observe.</param>
        /// <returns></returns>
        public async Task<byte[]> AuthenticationChallenge(Guid connectionId, byte address,
            byte algorithm, byte key, byte[] challenge, ushort maximumFragmentSize, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var requestLock = GetRequestLock(connectionId, address);

            if (!await requestLock.WaitAsync(timeout, cancellationToken))
            {
                throw new TimeoutException("Timeout waiting for another request to complete.");
            }

            try
            {
                return await WaitForChallengeResponse(connectionId, address, algorithm, key, challenge, maximumFragmentSize,
                    timeout, cancellationToken);
            }
            finally
            {
                requestLock.Release();
            }
        }

        private async Task<byte[]> WaitForChallengeResponse(Guid connectionId, byte address, byte algorithm, byte key,
            byte[] challenge, ushort maximumFragmentSize, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            bool complete = false;
            var requestData = new List<byte> { algorithm, key };
            requestData.AddRange(challenge);
            byte[] responseData = null;

            void Handler(object sender, MultiPartMessageDataReplyEventArgs args)
            {
                // Only process matching replies
                if (args.ConnectionId != connectionId || args.Address != address) return;

                var dataFragmentResponse = args.DataFragmentResponse;
                responseData ??= new byte[dataFragmentResponse.WholeMessageLength];

                complete = Message.BuildMultiPartMessageData(dataFragmentResponse.WholeMessageLength, dataFragmentResponse.Offset,
                    dataFragmentResponse.LengthOfFragment, dataFragmentResponse.Data, responseData);
            }

            AuthenticationChallengeResponseReceived += Handler;
            SetReceivingMultipartMessaging(connectionId, address, true);    
            
            ushort totalSize = (ushort)requestData.Count;
            ushort offset = 0;
            bool continueTransfer = true;

            try
            {
                while (!cancellationToken.IsCancellationRequested && continueTransfer)
                {
                    ushort fragmentSize = (ushort)Math.Min(maximumFragmentSize, totalSize - offset);
                    await SendCommand(connectionId, address, new AuthenticationChallengeFragment(
                            new MessageDataFragment(totalSize, offset, fragmentSize,
                                requestData.Skip(offset).Take((ushort)Math.Min(fragmentSize, totalSize - offset))
                                    .ToArray())), cancellationToken)
                        .ConfigureAwait(false);

                    offset += fragmentSize;

                    // Determine if we should continue on successful status
                    continueTransfer = offset < totalSize;
                }

                DateTime endTime = DateTime.UtcNow + timeout;
                
                while (DateTime.UtcNow <= endTime)
                {
                    if (complete)
                    {
                        return responseData;
                    }
                    
                    await Task.Delay(_timeToWaitToCheckOnData, cancellationToken);
                }

                throw new TimeoutException("Timeout waiting to receive challenge response.");
            }
            finally
            {
                AuthenticationChallengeResponseReceived -= Handler;
                SetReceivingMultipartMessaging(connectionId, address, false);
            }
        }

        /// <summary>
        /// Inform the PD the maximum size that the ACU can receive.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="maximumReceiveSize">The maximum size that the ACU can receive.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> ACUReceiveSize(Guid connectionId, byte address, ushort maximumReceiveSize)
        {
            var reply = await SendCommand(connectionId, address, new ACUReceiveSize(maximumReceiveSize))
                .ConfigureAwait(false);

            return reply.Type == (byte)ReplyType.Ack;
        }

        /// <summary>
        /// Instructs the PD to maintain communication with the credential for a specified time.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="keepAliveTimeInMilliseconds">The length of time to maintain communication with credential.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> KeepReaderActive(Guid connectionId, byte address, ushort keepAliveTimeInMilliseconds)
        {
            var reply = await SendCommand(connectionId, address, new KeepReaderActive(keepAliveTimeInMilliseconds))
                .ConfigureAwait(false);

            return reply.Type == (byte)ReplyType.Ack;
        }

        /// <summary>
        /// Instructs the PD to abort the current operation.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> AbortCurrentOperation(Guid connectionId, byte address)
        {
            var reply = await SendCommand(connectionId, address, new NoPayloadCommandData(CommandType.Abort))
                .ConfigureAwait(false);

            return reply.Type == (byte)ReplyType.Ack;
        }

        /// <summary>
        /// Is the PD online
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device</param>
        /// <param name="address">Address assigned to the device</param>
        /// <returns>Returns true if the PD is online</returns>
        public bool IsOnline(Guid connectionId, byte address)
        {
            return _buses[connectionId].IsOnline(address);
        }

        private void SetReceivingMultipartMessaging(Guid connectionId, byte address, bool isReceivingMultipartMessaging)
        {
            if (!_buses.TryGetValue(connectionId, out var bus))
            {
                throw new ArgumentException("Connection could not be found", nameof(connectionId));
            }
            
            bus.SetReceivingMultipartMessage(address, isReceivingMultipartMessaging);
        }
        
        private async Task<IncomingMessage> SendCommand(Guid connectionId, byte address, CommandData command,
            CancellationToken cancellationToken = default, bool throwOnNak = true)
        {
            var source = new TaskCompletionSource<IncomingMessage>();

            void EventHandler(object sender, ReplyEventArgs replyEventArgs)
            {
                var reply = replyEventArgs.Reply;
                if (!reply.MatchIssuingCommand(address, command.Code)) return;

                if (throwOnNak && replyEventArgs.Reply.ReplyMessage.Type == (byte)ReplyType.Nak)
                {
                    source.SetException(new NackReplyException(Nak.ParseData(reply.ReplyMessage.Payload)));
                }
                else
                {
                    source.SetResult(reply.ReplyMessage);
                }
            }

            if (!_buses.TryGetValue(connectionId, out var bus))
            {
                throw new ArgumentException("Connection could not be found", nameof(connectionId));
            }

            ReplyReceived += EventHandler;

            try
            {
                bus.SendCommand(address, command);

                var task = await Task.WhenAny(
                    source.Task, Task.Delay(_replyResponseTimeout, cancellationToken)).ConfigureAwait(false);

                if (source.Task != task)
                {
                    throw task.IsCanceled 
                        ? new OperationCanceledException(cancellationToken)
                        : new TimeoutException();
                }

                return await source.Task;
            }
            finally
            {
                ReplyReceived -= EventHandler;
            }
        }

        /// <summary>
        /// Shutdown the control panel and stop all communication to PDs.
        /// </summary>
        public async Task Shutdown()
        {
            foreach (var bus in _buses.Values)
            {
                bus.ConnectionStatusChanged -= BusOnConnectionStatusChanged;
                await bus.Close().ConfigureAwait(false);
                
                foreach (byte address in bus.ConfigureDeviceAddresses)
                {
                    OnConnectionStatusChanged(bus.Id, address, false, false);
                }
                bus.Dispose();
            }
            _buses.Clear();

            foreach (var requestLock in _requestLocks.Values)
            {
                requestLock.Dispose();
            }
            _requestLocks.Clear();
        }

        /// <summary>
        /// Reset communication sequence with the PD specified.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        public void ResetDevice(Guid connectionId, int address)
        {
            if (_buses.TryGetValue(connectionId, out Bus bus))
            {
                bus.ResetDevice(address);
            }
        }

        /// <summary>
        /// Add a PD to the control panel. This will replace an existing PD that is configured at the same address.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        /// <param name="useCrc">Use CRC for error checking.</param>
        /// <param name="useSecureChannel">Require the device to communicate with a secure channel.</param>
        /// <param name="secureChannelKey">Set the secure channel key, default installation key is used if not specified.</param>
        public void AddDevice(Guid connectionId, byte address, bool useCrc, bool useSecureChannel, byte[] secureChannelKey = null)
        {
            if (!_buses.TryGetValue(connectionId, out Bus foundBus))
            {
                throw new ArgumentException("Connection could not be found", nameof(connectionId));
            }

            if (address > 127)
            {
                throw new ArgumentOutOfRangeException(nameof(address), "Address is out of range, it must between 0 and 127.");
            }

            foundBus.AddDevice(address, useCrc, useSecureChannel, useSecureChannel ? secureChannelKey : null);
        }

        /// <summary>
        /// Remove a PD from the control panel.
        /// </summary>
        /// <param name="connectionId">Identify the connection for communicating to the device.</param>
        /// <param name="address">Address assigned to the device.</param>
        public void RemoveDevice(Guid connectionId, byte address)
        {
            if (_buses.TryGetValue(connectionId, out Bus bus))
            {
                bus.RemoveDevice(address);
            }
        }

        /// <summary>
        /// Attempts a device discovery on a given connection.
        /// </summary>
        /// <param name="connections">
        /// Enumerable instance which returns a set of connections (e.g. each having a different baud rate)
        /// on which the method will attempt to find the device. This enumerable can be considered a 
        /// factory that returns a different instance (and possibly instantiates it only when) when the
        /// discovery process invokes <see cref="IEnumerator.MoveNext"/>
        /// </param>
        /// <param name="options">Can be passed in for additional options for the discovery</param>
        /// <returns>
        /// If successful, an instance of DiscoveryResult which identifies the device along with
        /// providing its capabilities. Otherwise, will return null.
        /// </returns>
        public async Task<DiscoveryResult> DiscoverDevice(IEnumerable<IOsdpConnection> connections, DiscoveryOptions options = null)
        {
            options ??= new DiscoveryOptions();
            DiscoveryResult result = new();
            Guid connectionId = Guid.Empty;

            // ReSharper disable AccessToModifiedClosure
            // Disable the warning which doesn't apply to us as we are accumulating/modifying results of the discovery
            // in the above variables which are closed over. We are good with with

            void UpdateStatus(DiscoveryStatus status)
            {
                result.Status = status;
                options.ProgressCallback?.Invoke(result);
            }

            async Task<DeviceIdentification> TryIdReport(byte address)
            {
                try
                {
                    return await IdReport(connectionId, address, options.CancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    return null;
                }
            }

            async Task<bool> FindConnectionWithActiveDevice()
            {
                bool isFirst = true;

                foreach (IOsdpConnection connection in connections)
                {
                    if (!isFirst)
                    {
                        await Task.Delay(options.ReconnectDelay, options.CancellationToken);
                    }
                    isFirst = false;

                    // WARNING: We are specifying zero timespan here so that Polling is
                    // DISABLED on this entire connection. If we don't do this, it seems
                    // non-existent devices will attempt to start polling and those commands
                    // ends up getting "stuck" so that even when we get to the valid device
                    // we'll end up timing out and not finding it :(
                    // Polling on "other" devices should be doing this, but there's something
                    // funky in there. If the device we are looking for happens to be at addr
                    // 0, everything will work but anything else doesn't unless we disable
                    // polling
                    connectionId = StartConnection(connection, TimeSpan.Zero, options.Tracer ?? (_ => {}));

                    AddDevice(connectionId, ConfigurationAddress, true, false);

                    result.Connection = connection;
                    UpdateStatus(DiscoveryStatus.LookingForDeviceOnConnection);

                    var deviceIdentification = await TryIdReport(ConfigurationAddress).ConfigureAwait(false);
                    if (deviceIdentification != null)
                    {
                        result.Id = deviceIdentification;
                        RemoveDevice(connectionId, ConfigurationAddress);
                        await StopConnection(connectionId).ConfigureAwait(false);
                        UpdateStatus(DiscoveryStatus.ConnectionWithDeviceFound);
                        return true;
                    }

                    await StopConnection(connectionId).ConfigureAwait(false);
                }

                result.Connection = null;
                return false;
            }

            async Task<bool> FindDeviceAddress()
            {
                connectionId = StartConnection(result.Connection, TimeSpan.Zero, options.Tracer ?? (_ => {}));
                for (byte address = 0; address < ConfigurationAddress; address++)
                {
                    AddDevice(connectionId, address, true, false);
                    
                    result.Address = address;
                    UpdateStatus(DiscoveryStatus.LookingForDeviceAtAddress);
                    var deviceIdentification = await TryIdReport(address).ConfigureAwait(false);

                    if (deviceIdentification != null)
                    {
                        result.Id = deviceIdentification;
                        UpdateStatus(DiscoveryStatus.DeviceIdentified);
                        return true;
                    }

                    RemoveDevice(connectionId, address);
                }
                await StopConnection(connectionId).ConfigureAwait(false);

                // Since we didn't find a valid device, for an unexpected reason
                // let's just leave the at the configuration address
                result.Address = ConfigurationAddress;
                return false;
            }

            async Task CheckForDefaultSecurityKey()
            {
                var waitForSecureConnection = new TaskCompletionSource<bool>();

                void HandleStatusChange(object sender, ConnectionStatusEventArgs args)
                {
                    if (args.Address == result.Address && args.IsConnected && args.IsSecureChannelEstablished)
                    {
                        waitForSecureConnection.SetResult(true);
                    }
                }

                ConnectionStatusChanged += HandleStatusChange;

                try
                {
                    // Right now control panel API doesn't indicate connection errors due invalid credentials, even
                    // though internally the bus knows it got an error response from the device. Until underlying APIs give
                    // us that data, the best we can do here is wait for a successful connection and timeout when one 
                    // isn't established
                    connectionId = StartConnection(result.Connection, Bus.DefaultPollInterval,
                        options.Tracer ?? (_ => { }));
                    AddDevice(connectionId, result.Address, true, true);

                    using var waitForTask = await Task.WhenAny(waitForSecureConnection.Task,
                        Task.Delay(TimeSpan.FromSeconds(8), options.CancellationToken));
                    result.UsesDefaultSecurityKey = waitForTask == waitForSecureConnection.Task;
                }
                finally
                {
                    ConnectionStatusChanged -= HandleStatusChange;
                }
            }

            UpdateStatus(DiscoveryStatus.Started);

            // While we are doing discovery, we want to override default behavior which allows
            // underlying wire protocols to retry things if something goes wrong. In the
            // interests for time while cycling through every possible address, we are going to
            // not be as gracious
            var origResponseTimeout = _replyResponseTimeout;
            _replyResponseTimeout = options.ResponseTimeout;

            try
            {
                if (!_buses.IsEmpty)
                {
                    throw new ControlPanelInUseException();
                }

                if (!await FindConnectionWithActiveDevice().ConfigureAwait(false))
                {
                    UpdateStatus(DiscoveryStatus.DeviceNotFound);
                    return null;
                }

                if (!await FindDeviceAddress().ConfigureAwait(false))
                {
                    throw new DeviceDiscoveryException(
                        $"Unable to determine address of device that responded to a configuration address on baud rate {result.Connection.BaudRate}");
                }

                result.Capabilities = await DeviceCapabilities(connectionId, result.Address).ConfigureAwait(false);
                UpdateStatus(DiscoveryStatus.CapabilitiesDiscovered);

                // Connection above was opened intentionally with no polling (see above comments). Now we
                // need to open a new connection with polling before doing the next step
                await StopConnection(connectionId).ConfigureAwait(false);
                connectionId = Guid.Empty;

                await CheckForDefaultSecurityKey().ConfigureAwait(false);

                UpdateStatus(DiscoveryStatus.Succeeded);
                return result;
            }
            catch (Exception exception)
            {
                result.Error = exception;
                UpdateStatus(exception is OperationCanceledException 
                    ? DiscoveryStatus.Cancelled : DiscoveryStatus.Error);
                throw;
            }
            finally
            {
                if (connectionId != Guid.Empty) await StopConnection(connectionId).ConfigureAwait(false);
                _replyResponseTimeout = origResponseTimeout;
            }

            // ReSharper enable AccessToModifiedClosure
        }

        private void OnConnectionStatusChanged(Guid connectionId, byte address, bool isConnected,
            bool isSecureChannelEstablished)
        {
            ConnectionStatusChanged?.Invoke(this,
                new ConnectionStatusEventArgs(connectionId, address, isConnected, isSecureChannelEstablished));
        }

        private void OnReplyReceived(ReplyTracker reply)
        {
            ReplyReceived?.Invoke(this, new ReplyEventArgs { Reply = reply });

            switch ((ReplyType)reply.ReplyMessage.Type)
            {
                case ReplyType.Nak:
                    NakReplyReceived?.Invoke(this,
                        new NakReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            Nak.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.LocalStatusReport:
                    LocalStatusReportReplyReceived?.Invoke(this,
                        new LocalStatusReportReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            Model.ReplyData.LocalStatus.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.InputStatusReport:
                    InputStatusReportReplyReceived?.Invoke(this,
                        new InputStatusReportReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            Model.ReplyData.InputStatus.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.OutputStatusReport:
                    OutputStatusReportReplyReceived?.Invoke(this,
                        new OutputStatusReportReplyEventArgs(reply.ConnectionId,reply.ReplyMessage.Address,
                            Model.ReplyData.OutputStatus.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.ReaderStatusReport:
                    ReaderStatusReportReplyReceived?.Invoke(this,
                        new ReaderStatusReportReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            Model.ReplyData.ReaderStatus.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.RawReaderData:
                    RawCardDataReplyReceived?.Invoke(this,
                        new RawCardDataReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            RawCardData.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.FormattedReaderData:
                    FormattedCardDataReplyReceived?.Invoke(this,
                        new FormattedCardDataReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            FormattedCardData.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.ManufactureSpecific:
                    ManufacturerSpecificReplyReceived?.Invoke(this,
                        new ManufacturerSpecificReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            ManufacturerSpecific.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.PIVData:
                    PIVDataReplyReceived?.Invoke(this,
                        new MultiPartMessageDataReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            DataFragmentResponse.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.ResponseToChallenge:
                    AuthenticationChallengeResponseReceived?.Invoke(this,
                        new MultiPartMessageDataReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            DataFragmentResponse.ParseData(reply.ReplyMessage.Payload)));   
                    break;
                case ReplyType.KeypadData:
                    KeypadReplyReceived?.Invoke(this,
                        new KeypadReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            KeypadData.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.BiometricData:
                    BiometricReadResultsReplyReceived?.Invoke(this,
                        new BiometricReadResultsReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            BiometricReadResult.ParseData(reply.ReplyMessage.Payload)));
                    break;
                case ReplyType.BiometricMatchResult:
                    BiometricMatchReplyReceived?.Invoke(this,
                        new BiometricMatchReplyEventArgs(reply.ConnectionId, reply.ReplyMessage.Address,
                            BiometricMatchResult.ParseData(reply.ReplyMessage.Payload)));
                    break;
            }
        }

        private event EventHandler<ReplyEventArgs> ReplyReceived;

        /// <summary>
        /// Occurs when connection status changed.
        /// </summary>
        public event EventHandler<ConnectionStatusEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// Occurs when a negative reply is received.
        /// </summary>
        public event EventHandler<NakReplyEventArgs> NakReplyReceived;

        /// <summary>
        /// Occurs when local status report reply is received.
        /// </summary>
        public event EventHandler<LocalStatusReportReplyEventArgs> LocalStatusReportReplyReceived;

        /// <summary>
        /// Occurs when input status report reply is received.
        /// </summary>
        public event EventHandler<InputStatusReportReplyEventArgs> InputStatusReportReplyReceived;

        /// <summary>
        /// Occurs when output status report reply is received.
        /// </summary>
        public event EventHandler<OutputStatusReportReplyEventArgs> OutputStatusReportReplyReceived;

        /// <summary>
        /// Occurs when reader status report reply is received.
        /// </summary>
        public event EventHandler<ReaderStatusReportReplyEventArgs> ReaderStatusReportReplyReceived;

        /// <summary>
        /// Occurs when raw card data reply is received.
        /// </summary>
        public event EventHandler<RawCardDataReplyEventArgs> RawCardDataReplyReceived;

        /// <summary>
        /// Occurs when formatted card data reply is received.
        /// </summary>
        [Obsolete("Use Raw Card Data for incoming card reads.")]
        public event EventHandler<FormattedCardDataReplyEventArgs> FormattedCardDataReplyReceived;

        /// <summary>
        /// Occurs when manufacturer specific reply is received.
        /// </summary>
        public event EventHandler<ManufacturerSpecificReplyEventArgs> ManufacturerSpecificReplyReceived;

        /// <summary>
        /// Occurs when key pad data reply is received.
        /// </summary>
        public event EventHandler<KeypadReplyEventArgs> KeypadReplyReceived;

        /// <summary>
        /// Occurs when biometric read results reply is received.
        /// </summary>
        private event EventHandler<BiometricReadResultsReplyEventArgs> BiometricReadResultsReplyReceived;

        /// <summary>
        /// Occurs when biometric match reply is received.
        /// </summary>
        private event EventHandler<BiometricMatchReplyEventArgs> BiometricMatchReplyReceived;

        /// <summary>
        /// Occurs when piv data reply is received.
        /// </summary>
        private event EventHandler<MultiPartMessageDataReplyEventArgs> PIVDataReplyReceived;

        /// <summary>
        /// Occurs when authentication challenge response is received.
        /// </summary>
        private event EventHandler<MultiPartMessageDataReplyEventArgs> AuthenticationChallengeResponseReceived;

        /// <summary>
        /// A negative reply that has been received.
        /// </summary>
        public class NakReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="NakReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="nak">A negative reply that has been received.</param>
            public NakReplyEventArgs(Guid connectionId, byte address, Nak nak)
            {
                ConnectionId = connectionId;
                Address = address;
                Nak = nak;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A negative reply that has been received.
            /// </summary>
            public Nak Nak { get; }
        }

        /// <summary>
        /// A connection status has been changed.
        /// </summary>
        public class ConnectionStatusEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ConnectionStatusEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="isConnected">Is the device currently connected.</param>
            /// <param name="isSecureChannelEstablished">Is the secure channel currently established.</param>
            public ConnectionStatusEventArgs(Guid connectionId, byte address, bool isConnected,
                bool isSecureChannelEstablished)
            {
                ConnectionId = connectionId;
                Address = address;
                IsConnected = isConnected;
                IsSecureChannelEstablished = isSecureChannelEstablished;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// Is the device currently connected.
            /// </summary>
            public bool IsConnected { get; }

            /// <summary>
            /// Is the secure channel currently established
            /// </summary>
            public bool IsSecureChannelEstablished { get; }

            /// <inheritdoc/>
            public override string ToString() =>
                $"{ConnectionId}:{Address} - Conn: {IsConnected}; Sec: {IsSecureChannelEstablished}";
        }

        /// <summary>
        /// The local status report reply has been received.
        /// </summary>
        public class LocalStatusReportReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="LocalStatusReportReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="localStatus">A local status report reply.</param>
            public LocalStatusReportReplyEventArgs(Guid connectionId, byte address, LocalStatus localStatus)
            {
                ConnectionId = connectionId;
                Address = address;
                LocalStatus = localStatus;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A local status report reply.
            /// </summary>
            public LocalStatus LocalStatus { get; }
        }

        /// <summary>
        /// The input status report reply has been received.
        /// </summary>
        public class InputStatusReportReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="LocalStatusReportReplyEventArgs" /> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="inputStatus">A input status report reply.</param>
            public InputStatusReportReplyEventArgs(Guid connectionId, byte address, InputStatus inputStatus)
            {
                ConnectionId = connectionId;
                Address = address;
                InputStatus = inputStatus;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A input status report reply.
            /// </summary>
            public InputStatus InputStatus { get; }
        }

        /// <summary>
        /// The output status report reply has been received.
        /// </summary>
        public class OutputStatusReportReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="OutputStatusReportReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="outputStatus">A output status report reply.</param>
            public OutputStatusReportReplyEventArgs(Guid connectionId, byte address, OutputStatus outputStatus)
            {
                ConnectionId = connectionId;
                Address = address;
                OutputStatus = outputStatus;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A output status report reply.
            /// </summary>
            public OutputStatus OutputStatus { get; }
        }

        /// <summary>
        /// The reader status report reply has been received.
        /// </summary>
        public class ReaderStatusReportReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ReaderStatusReportReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="readerStatus">A reader status report reply.</param>
            public ReaderStatusReportReplyEventArgs(Guid connectionId, byte address, ReaderStatus readerStatus)
            {
                ConnectionId = connectionId;
                Address = address;
                ReaderStatus = readerStatus;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A reader status report reply.
            /// </summary>
            public ReaderStatus ReaderStatus { get; }
        }

        /// <summary>
        /// The raw card data reply has been received.
        /// </summary>
        public class RawCardDataReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="RawCardDataReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="rawCardData">A raw card data reply.</param>
            public RawCardDataReplyEventArgs(Guid connectionId, byte address, RawCardData rawCardData)
            {
                ConnectionId = connectionId;
                Address = address;
                RawCardData = rawCardData;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A raw card data reply.
            /// </summary>
            public RawCardData RawCardData { get; }
        }

        /// <summary>
        /// The formatted card data reply has been received.
        /// </summary>
        public class FormattedCardDataReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FormattedCardDataReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="formattedCardData">A formatted card data reply.</param>
            public FormattedCardDataReplyEventArgs(Guid connectionId, byte address, FormattedCardData formattedCardData)
            {
                ConnectionId = connectionId;
                Address = address;
                FormattedCardData = formattedCardData;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A formatted card data reply.
            /// </summary>
            public FormattedCardData FormattedCardData { get; }
        }

        /// <summary>
        /// The manufacture specific reply has been received.
        /// </summary>
        public class ManufacturerSpecificReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ManufacturerSpecificReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="manufacturerSpecific">A manufacturer specific reply.</param>
            public ManufacturerSpecificReplyEventArgs(Guid connectionId, byte address, ManufacturerSpecific manufacturerSpecific)
            {
                ConnectionId = connectionId;
                Address = address;
                ManufacturerSpecific = manufacturerSpecific;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A manufacturer specific reply.
            /// </summary>
            public ManufacturerSpecific ManufacturerSpecific { get; }
        }

        /// <summary>
        /// The multi-part message reply has been received.
        /// </summary>
        private class MultiPartMessageDataReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MultiPartMessageDataReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="dataFragmentResponse">A PIV data reply.</param>
            public MultiPartMessageDataReplyEventArgs(Guid connectionId, byte address, DataFragmentResponse dataFragmentResponse)
            {
                ConnectionId = connectionId;
                Address = address;
                DataFragmentResponse = dataFragmentResponse;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A PIV data reply.
            /// </summary>
            public DataFragmentResponse DataFragmentResponse { get; }
        }

        /// <summary>
        /// The keypad reply has been received.
        /// </summary>
        public class KeypadReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="KeypadReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="keypadData">A keypad reply.</param>
            public KeypadReplyEventArgs(Guid connectionId, byte address, KeypadData keypadData)
            {
                ConnectionId = connectionId;
                Address = address;
                KeypadData = keypadData;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A keypad reply..
            /// </summary>
            public KeypadData KeypadData { get; }
        }

        /// <summary>
        /// A biometric match reply has been received.
        /// </summary>
        private class BiometricReadResultsReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="BiometricReadResultsReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="biometricReadResult">A biometric read results reply.</param>
            public BiometricReadResultsReplyEventArgs(Guid connectionId, byte address,
                BiometricReadResult biometricReadResult)
            {
                ConnectionId = connectionId;
                Address = address;
                BiometricReadResult = biometricReadResult;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A biometric read result reply.
            /// </summary>
            public BiometricReadResult BiometricReadResult { get; }
        }

        /// <summary>
        /// A biometric match reply has been received.
        /// </summary>
        private class BiometricMatchReplyEventArgs : EventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="BiometricMatchReplyEventArgs"/> class.
            /// </summary>
            /// <param name="connectionId">Identify the connection for communicating to the device.</param>
            /// <param name="address">Address assigned to the device.</param>
            /// <param name="biometricMatchResult">A biometric match reply.</param>
            public BiometricMatchReplyEventArgs(Guid connectionId, byte address,
                BiometricMatchResult biometricMatchResult)
            {
                ConnectionId = connectionId;
                Address = address;
                BiometricMatchResult = biometricMatchResult;
            }

            /// <summary>
            /// Identify the connection for communicating to the device.
            /// </summary>
            public Guid ConnectionId { get; }

            /// <summary>
            /// Address assigned to the device.
            /// </summary>
            public byte Address { get; }

            /// <summary>
            /// A biometric match result reply..
            /// </summary>
            public BiometricMatchResult BiometricMatchResult { get; }
        }

        /// <summary>
        /// Track the status of a file transfer to a PD
        /// </summary>
        public class FileTransferStatus
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FileTransferStatus"/> class.
            /// </summary>
            /// <param name="status">The last status returned from the PD</param>
            /// <param name="currentOffset">The current offset in the data of the file transfer</param>
            /// <param name="nak">Contains Nak reply data if returned</param>
            public FileTransferStatus(Model.ReplyData.FileTransferStatus.StatusDetail status, int currentOffset,
                Nak nak)
            {
                Status = status;
                CurrentOffset = currentOffset;
                Nak = nak;
            }

            /// <summary>
            /// The last status returned from the PD
            /// </summary>
            public Model.ReplyData.FileTransferStatus.StatusDetail  Status { get; }

            /// <summary>
            /// The current offset in the data of the file transfer
            /// </summary>
            public int CurrentOffset { get; }

            /// <summary>
            /// Contains Nak reply if returned
            /// </summary>
            public Nak Nak { get; }
        }

        /// <summary>
        /// An error has occurred during a file transfer operation.
        /// </summary>
        public class FileTransferException : Exception
        {
            internal FileTransferException(string msg, FileTransferStatus status) : base(msg)
            {
                Status = status;
            }

            /// <summary>
            /// The last received status from the PD that indicates what error has occurred.
            /// </summary>
            public FileTransferStatus Status { get; }
        }

        private class ReplyEventArgs : EventArgs
        {
            public ReplyTracker Reply { get; set; }
        }
    }
}