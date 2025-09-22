
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ACUConsole.Configuration;
using ACUConsole.Model;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Logging;
using OSDP.Net;
using OSDP.Net.Connections;
using OSDP.Net.Model.CommandData;
using OSDP.Net.PanelCommands.DeviceDiscover;
using OSDP.Net.Tracing;
using CommunicationConfiguration = OSDP.Net.Model.CommandData.CommunicationConfiguration;
using ManufacturerSpecific = OSDP.Net.Model.CommandData.ManufacturerSpecific;

namespace ACUConsole
{
    /// <summary>
    /// Presenter class that manages the ACU Console business logic and device interactions
    /// </summary>
    public class ACUConsolePresenter : IACUConsolePresenter
    {
        private ControlPanel _controlPanel;
        private ILoggerFactory _loggerFactory;
        private readonly List<ACUEvent> _messageHistory = new();
        private readonly object _messageLock = new();
        private readonly ConcurrentDictionary<byte, ControlPanel.NakReplyEventArgs> _lastNak = new();
        
        private Guid _connectionId = Guid.Empty;
        private Settings _settings;
        private string _lastConfigFilePath;
        private string _lastOsdpConfigFilePath;

        // Events
        public event EventHandler<ACUEvent> MessageReceived;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;
        public event EventHandler<Exception> ErrorOccurred;

        // Properties
        public bool IsConnected => _connectionId != Guid.Empty;
        public Guid ConnectionId => _connectionId;
        public IReadOnlyList<ACUEvent> MessageHistory 
        {
            get
            {
                lock (_messageLock)
                {
                    return _messageHistory.ToList().AsReadOnly();
                }
            }
        }
        public Settings Settings => _settings;

        public ACUConsolePresenter()
        {
            InitializeLogging();
            InitializePaths();
            InitializeControlPanel();
            LoadSettings();
        }


        private void InitializeLogging()
        {
            // Check if we're running in a single-file deployment
            var entryAssembly = Assembly.GetEntryAssembly();
            var isSingleFile = string.IsNullOrEmpty(entryAssembly?.Location);

            if (!isSingleFile)
            {
                // Only configure log4net for regular deployment
                ConfigureLog4Net();
                _loggerFactory = new LoggerFactory();
                _loggerFactory.AddLog4Net();
            }
            else
            {
                // For single-file deployment, skip complex logging to avoid CodeBase issues
                _loggerFactory = new LoggerFactory();
                Console.WriteLine("Running in single-file mode - using basic logging");
            }
        }

        private void ConfigureLog4Net()
        {
            // Check if we're running in a single-file deployment
            var entryAssembly = Assembly.GetEntryAssembly();
            var isSingleFile = string.IsNullOrEmpty(entryAssembly?.Location);

            if (isSingleFile)
            {
                // For single-file deployment, use minimal log4net configuration to avoid CodeBase issues
                ConfigureLog4NetForSingleFile();
            }
            else
            {
                // For regular deployment, use standard configuration
                var repository = LogManager.GetRepository(Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly());

                var configFile = new FileInfo("log4net.config");
                if (configFile.Exists)
                {
                    try
                    {
                        XmlConfigurator.Configure(repository, configFile);
                    }
                    catch
                    {
                        // Fallback to programmatic configuration if file loading fails
                        ConfigureLog4NetProgrammatically(repository);
                    }
                }
                else
                {
                    // Fallback to programmatic configuration
                    ConfigureLog4NetProgrammatically(repository);
                }
            }
        }

        private void ConfigureLog4NetForSingleFile()
        {
            // For single-file deployment, use a very basic configuration that avoids Assembly.CodeBase
            try
            {
                // Use default configuration without specifying a repository to avoid Assembly.CodeBase issues
                var repository = LogManager.CreateRepository(Assembly.GetEntryAssembly()!, typeof(log4net.Repository.Hierarchy.Hierarchy));
                ConfigureLog4NetProgrammatically(repository);
            }
            catch
            {
                // If even this fails, log4net might not work in single-file mode
                // In this case, logging will simply not work, but app won't crash
                Console.WriteLine("Warning: Could not initialize logging in single-file mode");
            }
        }

        private void ConfigureLog4NetProgrammatically(log4net.Repository.ILoggerRepository repository)
        {
            // Create an appender programmatically to match log4net.config
            var appender = new CustomAppender
            {
                Layout = new log4net.Layout.PatternLayout("%date | %thread | %-5level | %logger | %message%newline")
            };
            appender.ActivateOptions();

            // Configure root logger
            var rootLogger = ((log4net.Repository.Hierarchy.Hierarchy)repository).Root;
            rootLogger.Level = log4net.Core.Level.All;
            rootLogger.AddAppender(appender);

            // Mark the repository as configured
            ((log4net.Repository.Hierarchy.Hierarchy)repository).Configured = true;
            
            // Set up custom appender to redirect log messages
            CustomAppender.MessageHandler = AddLogMessage;
        }

        private void InitializePaths()
        {
            // Use AppContext.BaseDirectory for single-file deployment compatibility
            var baseDirectory = GetBaseDirectory();
            _lastConfigFilePath = Path.Combine(baseDirectory, "appsettings.config");
            _lastOsdpConfigFilePath = baseDirectory;
        }

        private string GetBaseDirectory()
        {
            // Check if we're running in a single-file deployment
            var entryAssembly = Assembly.GetEntryAssembly();
            var isSingleFile = string.IsNullOrEmpty(entryAssembly?.Location);

            if (isSingleFile)
            {
                // For single-file deployment, use AppContext.BaseDirectory
                return AppContext.BaseDirectory;
            }
            else
            {
                // For regular deployment, use current directory
                return Environment.CurrentDirectory;
            }
        }

        private void InitializeControlPanel()
        {
            _controlPanel = new ControlPanel(_loggerFactory);
            RegisterControlPanelEvents();
        }

        private void LoadSettings()
        {
            try
            {
                var baseDirectory = GetBaseDirectory();
                var configPath = Path.Combine(baseDirectory, "appsettings.config");

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    _settings = JsonSerializer.Deserialize<Settings>(json) ?? GetDefaultSettings();
                }
                else
                {
                    // Use default settings for single-file deployment or missing config
                    _settings = GetDefaultSettings();
                }
            }
            catch
            {
                _settings = GetDefaultSettings();
            }
        }

        private static Settings GetDefaultSettings()
        {
            return new Settings
            {
                SerialConnectionSettings = new SerialConnectionSettings
                {
                    PortName = "COM4",
                    BaudRate = 9600,
                    ReplyTimeout = 200
                },
                TcpServerConnectionSettings = new TcpServerConnectionSettings
                {
                    PortNumber = 5000,
                    BaudRate = 9600,
                    ReplyTimeout = 200
                },
                TcpClientConnectionSettings = new TcpClientConnectionSettings
                {
                    Host = "",
                    PortNumber = 5000,
                    BaudRate = 9600,
                    ReplyTimeout = 200
                },
                Devices =
                [
                    new DeviceSetting
                    {
                        Name = "Secure",
                        Address = 0,
                        UseSecureChannel = true,
                        UseCrc = true,
                        SecureChannelKey = DeviceSetting.DefaultKey
                    }
                ],
                PollingInterval = 200,
                LastFileTransferDirectory = null,
                IsTracing = false
            };
        }

        private void RegisterControlPanelEvents()
        {
            _controlPanel.ConnectionStatusChanged += OnConnectionStatusChanged;
            _controlPanel.NakReplyReceived += OnNakReplyReceived;
            _controlPanel.LocalStatusReportReplyReceived += OnLocalStatusReportReplyReceived;
            _controlPanel.InputStatusReportReplyReceived += OnInputStatusReportReplyReceived;
            _controlPanel.OutputStatusReportReplyReceived += OnOutputStatusReportReplyReceived;
            _controlPanel.ReaderStatusReportReplyReceived += OnReaderStatusReportReplyReceived;
            _controlPanel.RawCardDataReplyReceived += OnRawCardDataReplyReceived;
            _controlPanel.KeypadReplyReceived += OnKeypadReplyReceived;
        }

        // Connection Methods
        public async Task StartSerialConnection(string portName, int baudRate, int replyTimeout)
        {
            var connection = new SerialPortOsdpConnection(portName, baudRate)
            {
                ReplyTimeout = TimeSpan.FromMilliseconds(replyTimeout)
            };
            
            await StartConnection(connection);
            
            _settings.SerialConnectionSettings.PortName = portName;
            _settings.SerialConnectionSettings.BaudRate = baudRate;
            _settings.SerialConnectionSettings.ReplyTimeout = replyTimeout;
        }

        public async Task StartTcpServerConnection(int portNumber, int baudRate, int replyTimeout)
        {
            var connection = new TcpServerOsdpConnection(portNumber, baudRate, _loggerFactory)
            {
                ReplyTimeout = TimeSpan.FromMilliseconds(replyTimeout)
            };
            
            await StartConnection(connection);
            
            _settings.TcpServerConnectionSettings.PortNumber = portNumber;
            _settings.TcpServerConnectionSettings.BaudRate = baudRate;
            _settings.TcpServerConnectionSettings.ReplyTimeout = replyTimeout;
        }

        public async Task StartTcpClientConnection(string host, int portNumber, int baudRate, int replyTimeout)
        {
            var connection = new TcpClientOsdpConnection(host, portNumber, baudRate);
            
            await StartConnection(connection);
            
            _settings.TcpClientConnectionSettings.Host = host;
            _settings.TcpClientConnectionSettings.PortNumber = portNumber;
            _settings.TcpClientConnectionSettings.BaudRate = baudRate;
            _settings.TcpClientConnectionSettings.ReplyTimeout = replyTimeout;
        }

        public async Task StopConnection()
        {
            _connectionId = Guid.Empty;
            await _controlPanel.Shutdown();
            AddLogMessage("Connection stopped");
        }

        private async Task StartConnection(IOsdpConnection osdpConnection)
        {
            _lastNak.Clear();

            if (_connectionId != Guid.Empty)
            {
                await _controlPanel.Shutdown();
            }

            _connectionId = _controlPanel.StartConnection(osdpConnection, 
                TimeSpan.FromMilliseconds(_settings.PollingInterval), 
                _settings.IsTracing);

            foreach (var device in _settings.Devices)
            {
                _controlPanel.AddDevice(_connectionId, device.Address, device.UseCrc, 
                    device.UseSecureChannel, device.SecureChannelKey);
            }
            
            AddLogMessage($"Connection started with ID: {_connectionId}");
        }

        // Device Management Methods
        public void AddDevice(string name, byte address, bool useCrc, bool useSecureChannel, byte[] secureChannelKey)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Start a connection before adding devices.");
            }

            _lastNak.TryRemove(address, out _);
            _controlPanel.AddDevice(_connectionId, address, useCrc, useSecureChannel, secureChannelKey);

            var foundDevice = _settings.Devices.FirstOrDefault(device => device.Address == address);
            if (foundDevice != null)
            {
                _settings.Devices.Remove(foundDevice);
            }

            _settings.Devices.Add(new DeviceSetting
            {
                Address = address,
                Name = name,
                UseSecureChannel = useSecureChannel,
                UseCrc = useCrc,
                SecureChannelKey = secureChannelKey
            });

            AddLogMessage($"Device '{name}' added at address {address}");
        }

        public void RemoveDevice(byte address)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Start a connection before removing devices.");
            }

            var removedDevice = _settings.Devices.FirstOrDefault(d => d.Address == address);
            if (removedDevice != null)
            {
                _controlPanel.RemoveDevice(_connectionId, address);
                _lastNak.TryRemove(address, out _);
                _settings.Devices.Remove(removedDevice);
                AddLogMessage($"Device '{removedDevice.Name}' removed from address {address}");
            }
        }

        public async Task<string> DiscoverDevice(string portName, int pingTimeout, int reconnectDelay, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _controlPanel.DiscoverDevice(
                    SerialPortOsdpConnection.EnumBaudRates(portName),
                    new DiscoveryOptions
                    {
                        ProgressCallback = OnDiscoveryProgress,
                        ResponseTimeout = TimeSpan.FromMilliseconds(pingTimeout),
                        CancellationToken = cancellationToken,
                        ReconnectDelay = TimeSpan.FromMilliseconds(reconnectDelay),
                    }.WithDefaultTracer(_settings.IsTracing));

                var resultMessage = result != null
                    ? $"Device discovered successfully:\n{result}"
                    : "Device was not found";
                
                AddLogMessage(resultMessage);
                return resultMessage;
            }
            catch (OperationCanceledException)
            {
                AddLogMessage("Device discovery cancelled");
                return "Device discovery cancelled";
            }
            catch (Exception ex)
            {
                AddLogMessage($"Device Discovery Error:\n{ex}");
                throw;
            }
        }

        // Command Methods - Individual command implementations
        public async Task SendDeviceCapabilities(byte address)
        {
            await ExecuteCommand("Device capabilities", address, 
                () => _controlPanel.DeviceCapabilities(_connectionId, address));
        }

        public async Task SendIdReport(byte address)
        {
            await ExecuteCommand("ID report", address, 
                () => _controlPanel.IdReport(_connectionId, address));
        }

        public async Task SendInputStatus(byte address)
        {
            await ExecuteCommand("Input status", address, 
                () => _controlPanel.InputStatus(_connectionId, address));
        }

        public async Task SendLocalStatus(byte address)
        {
            await ExecuteCommand("Local Status", address, 
                () => _controlPanel.LocalStatus(_connectionId, address));
        }

        public async Task SendOutputStatus(byte address)
        {
            await ExecuteCommand("Output status", address, 
                () => _controlPanel.OutputStatus(_connectionId, address));
        }

        public async Task SendReaderStatus(byte address)
        {
            await ExecuteCommand("Reader status", address, 
                () => _controlPanel.ReaderStatus(_connectionId, address));
        }

        public async Task SendCommunicationConfiguration(byte address, byte newAddress, int newBaudRate)
        {
            var config = new CommunicationConfiguration(newAddress, newBaudRate);
            await ExecuteCommand("Communication Configuration", address, 
                () => _controlPanel.CommunicationConfiguration(_connectionId, address, config));
            
            // Handle device address change
            var device = _settings.Devices.FirstOrDefault(d => d.Address == address);
            if (device != null)
            {
                _controlPanel.RemoveDevice(_connectionId, address);
                _lastNak.TryRemove(address, out _);
                
                device.Address = newAddress;
                _controlPanel.AddDevice(_connectionId, device.Address, device.UseCrc, 
                    device.UseSecureChannel, device.SecureChannelKey);
            }
        }

        public async Task SendOutputControl(byte address, byte outputNumber, bool activate)
        {
            var outputControls = new OutputControls([
                new OutputControl(outputNumber, activate
                    ? OutputControlCode.PermanentStateOnAbortTimedOperation
                    : OutputControlCode.PermanentStateOffAbortTimedOperation, 0)
            ]);
            
            await ExecuteCommand("Output Control Command", address, 
                () => _controlPanel.OutputControl(_connectionId, address, outputControls));
        }

        public async Task SendReaderLedControl(byte address, byte ledNumber, LedColor color)
        {
            var ledControls = new ReaderLedControls([
                new ReaderLedControl(0, ledNumber,
                    TemporaryReaderControlCode.CancelAnyTemporaryAndDisplayPermanent, 1, 0,
                    LedColor.Red, LedColor.Green, 0,
                    PermanentReaderControlCode.SetPermanentState, 1, 0, color, color)
            ]);
            
            await ExecuteCommand("Reader LED Control Command", address, 
                () => _controlPanel.ReaderLedControl(_connectionId, address, ledControls));
        }

        public async Task SendReaderBuzzerControl(byte address, byte readerNumber, byte repeatTimes)
        {
            var buzzerControl = new ReaderBuzzerControl(readerNumber, ToneCode.Default, 2, 2, repeatTimes);
            await ExecuteCommand("Reader Buzzer Control Command", address, 
                () => _controlPanel.ReaderBuzzerControl(_connectionId, address, buzzerControl));
        }

        public async Task SendReaderTextOutput(byte address, byte readerNumber, string text)
        {
            var textOutput = new ReaderTextOutput(readerNumber, TextCommand.PermanentTextNoWrap, 0, 1, 1, text);
            await ExecuteCommand("Reader Text Output Command", address, 
                () => _controlPanel.ReaderTextOutput(_connectionId, address, textOutput));
        }

        public async Task SendManufacturerSpecific(byte address, byte[] vendorCode, byte[] data)
        {
            var manufacturerSpecific = new ManufacturerSpecific(vendorCode, data);
            await ExecuteCommand("Manufacturer Specific Command", address, 
                () => _controlPanel.ManufacturerSpecificCommand(_connectionId, address, manufacturerSpecific));
        }

        public async Task SendEncryptionKeySet(byte address, byte[] key)
        {
            var keyConfig = new EncryptionKeyConfiguration(KeyType.SecureChannelBaseKey, key);
            var result = await ExecuteCommand("Encryption Key Configuration", address, 
                () => _controlPanel.EncryptionKeySet(_connectionId, address, keyConfig));
            
            if (result)
            {
                _lastNak.TryRemove(address, out _);
                var device = _settings.Devices.FirstOrDefault(d => d.Address == address);
                if (device != null)
                {
                    device.UseSecureChannel = true;
                    device.SecureChannelKey = key;
                    _controlPanel.AddDevice(_connectionId, device.Address, device.UseCrc, 
                        device.UseSecureChannel, device.SecureChannelKey);
                }
            }
        }

        public async Task SendBiometricRead(byte address, byte readerNumber, byte type, byte format, byte quality)
        {
            var biometricData = new BiometricReadData(readerNumber, (BiometricType)type, (BiometricFormat)format, quality);
            var result = await ExecuteCommandWithTimeout("Biometric Read Command", address, 
                () => _controlPanel.ScanAndSendBiometricData(_connectionId, address, biometricData, 
                    TimeSpan.FromSeconds(30), CancellationToken.None));
            
            if (result.TemplateData.Length > 0)
            {
                await File.WriteAllBytesAsync("BioReadTemplate", result.TemplateData);
            }
        }

        public async Task SendBiometricMatch(byte address, byte readerNumber, byte type, byte format, byte qualityThreshold, byte[] templateData)
        {
            var biometricTemplate = new BiometricTemplateData(readerNumber, (BiometricType)type, (BiometricFormat)format,
                qualityThreshold, templateData);
            await ExecuteCommandWithTimeout("Biometric Match Command", address, 
                () => _controlPanel.ScanAndMatchBiometricTemplate(_connectionId, address, biometricTemplate, 
                    TimeSpan.FromSeconds(30), CancellationToken.None));
        }

        public async Task<FileTransferResult> SendFileTransfer(byte address, byte type, byte[] data, byte messageSize,
            Action<ControlPanel.FileTransferStatus> progressCallback, CancellationToken cancellationToken)
        {
            var fragmentCount = 0;

            // Wrap the progress callback to count fragments
            Action<ControlPanel.FileTransferStatus> wrappedCallback = status =>
            {
                if (status.CurrentOffset > 0)
                {
                    // Calculate fragment count based on message size
                    fragmentCount = (status.CurrentOffset + messageSize - 1) / messageSize;
                }
                progressCallback?.Invoke(status);
            };

            var result = await _controlPanel.FileTransfer(_connectionId, address, type, data, messageSize,
                wrappedCallback, cancellationToken);

            // Final fragment count calculation
            if (data.Length > 0)
            {
                fragmentCount = (data.Length + messageSize - 1) / messageSize;
            }

            return new FileTransferResult { FragmentCount = fragmentCount, Status = result };
        }

        public async Task SendCustomCommand(byte address, CommandData commandData)
        {
            await _controlPanel.SendCustomCommand(_connectionId, address, commandData);
        }

        // Configuration Methods
        public void UpdateConnectionSettings(int pollingInterval, bool isTracing)
        {
            _settings.PollingInterval = pollingInterval;
            _settings.IsTracing = isTracing;
            StatusChanged?.Invoke(this, "Connection settings updated");
        }

        public void SaveConfiguration()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_lastConfigFilePath, json);
                AddLogMessage("Configuration saved successfully");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        public void LoadConfiguration()
        {
            try
            {
                LoadSettings();
                AddLogMessage("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        public void ParseOSDPCapFile(string filePath, byte? filterAddress, bool ignorePollsAndAcks, byte[] key)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var entries = PacketDecoding.OSDPCapParser(json, key)
                    .Where(entry => FilterAddress(entry, filterAddress) && FilterPollsAndAcks(entry, ignorePollsAndAcks));
                
                var textBuilder = BuildTextFromEntries(entries);
                var outputPath = Path.ChangeExtension(filePath, ".txt");
                File.WriteAllText(outputPath, textBuilder.ToString());
                
                // Update the last used directory for next time
                _lastOsdpConfigFilePath = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
                
                AddLogMessage($"OSDP Cap file parsed successfully. Output saved to: {outputPath}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        // Utility Methods
        public void ClearHistory()
        {
            lock (_messageLock)
            {
                _messageHistory.Clear();
            }
            StatusChanged?.Invoke(this, "Message history cleared");
        }

        public void AddLogMessage(string message)
        {
            lock (_messageLock)
            {
                var acuEvent = new ACUEvent
                {
                    Timestamp = DateTime.Now,
                    Title = "System",
                    Message = message,
                    Type = ACUEventType.Information
                };
                
                _messageHistory.Add(acuEvent);
                
                // Keep only the last 100 messages
                if (_messageHistory.Count > 100)
                {
                    _messageHistory.RemoveAt(0);
                }
                
                MessageReceived?.Invoke(this, acuEvent);
            }
        }

        public bool CanSendCommand()
        {
            return IsConnected && _settings.Devices.Count > 0;
        }

        public string[] GetDeviceList()
        {
            return _settings.Devices
                .OrderBy(device => device.Address)
                .Select(device => $"{device.Address} : {device.Name}")
                .ToArray();
        }

        public string GetLastOsdpConfigDirectory()
        {
            return _lastOsdpConfigFilePath;
        }

        // Private helper methods
        private async Task<T> ExecuteCommand<T>(string commandName, byte address, Func<Task<T>> commandFunction)
        {
            try
            {
                var result = await commandFunction();
                AddLogMessage($"{commandName} for address {address}\n{result}\n{new string('*', 30)}");
                return result;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        private async Task<T> ExecuteCommandWithTimeout<T>(string commandName, byte address, Func<Task<T>> commandFunction)
        {
            try
            {
                var result = await commandFunction();
                AddLogMessage($"{commandName} for address {address}\n{result}\n{new string('*', 30)}");
                return result;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        private void OnDiscoveryProgress(DiscoveryResult current)
        {
            string additionalInfo = current.Status switch
            {
                DiscoveryStatus.Started => string.Empty,
                DiscoveryStatus.LookingForDeviceOnConnection => $"\n    Connection baud rate {current.Connection.BaudRate}...",
                DiscoveryStatus.ConnectionWithDeviceFound => $"\n    Connection baud rate {current.Connection.BaudRate}",
                DiscoveryStatus.LookingForDeviceAtAddress => $"\n    Address {current.Address}...",
                _ => string.Empty
            };

            AddLogMessage($"Device Discovery Progress: {current.Status}{additionalInfo}");
        }

        private bool FilterAddress(OSDPCapEntry entry, byte? address)
        {
            return !address.HasValue || entry.Packet.Address == address.Value;
        }

        private bool FilterPollsAndAcks(OSDPCapEntry entry, bool ignorePollsAndAcks)
        {
            if (!ignorePollsAndAcks) return true;
            
            return (entry.Packet.CommandType != null && entry.Packet.CommandType != OSDP.Net.Messages.CommandType.Poll) ||
                   (entry.Packet.ReplyType != null && entry.Packet.ReplyType != OSDP.Net.Messages.ReplyType.Ack);
        }

        private StringBuilder BuildTextFromEntries(IEnumerable<OSDPCapEntry> entries)
        {
            var textBuilder = new StringBuilder();
            DateTime lastEntryTimeStamp = DateTime.MinValue;
            
            foreach (var entry in entries)
            {
                TimeSpan difference = lastEntryTimeStamp > DateTime.MinValue
                    ? entry.TimeStamp - lastEntryTimeStamp
                    : TimeSpan.Zero;
                lastEntryTimeStamp = entry.TimeStamp;
                
                string direction = "Unknown";
                string type = "Unknown";
                
                if (entry.Packet.CommandType != null)
                {
                    direction = "ACU -> PD";
                    type = entry.Packet.CommandType.ToString();
                }
                else if (entry.Packet.ReplyType != null)
                {
                    direction = "PD -> ACU";
                    type = entry.Packet.ReplyType.ToString();
                }

                var payloadData = entry.Packet.ParsePayloadData();
                
                var payloadDataString = payloadData switch
                {
                    null => string.Empty,
                    byte[] data => $"    {BitConverter.ToString(data)}\n",
                    string data => $"    {data}\n",
                    _ => payloadData.ToString()
                };

                textBuilder.AppendLine($"{entry.TimeStamp:yy-MM-dd HH:mm:ss.fff} [ {difference:g} ] {direction}: {type}");
                textBuilder.AppendLine($"    Address: {entry.Packet.Address} Sequence: {entry.Packet.Sequence}");
                textBuilder.AppendLine(payloadDataString);
            }

            return textBuilder;
        }

        // Event handlers for ControlPanel events
        private void OnConnectionStatusChanged(object sender, ControlPanel.ConnectionStatusEventArgs args)
        {
            var deviceName = _settings.Devices.SingleOrDefault(device => device.Address == args.Address)?.Name ?? "[Unknown]";
            var eventArgs = new ConnectionStatusChangedEventArgs
            {
                Address = args.Address,
                IsConnected = args.IsConnected,
                IsSecureChannelEstablished = args.IsSecureChannelEstablished,
                DeviceName = deviceName
            };
            
            ConnectionStatusChanged?.Invoke(this, eventArgs);
            
            var statusMessage = $"Device '{deviceName}' at address {args.Address} is now " +
                               $"{(args.IsConnected ? (args.IsSecureChannelEstablished ? "connected with secure channel" : "connected with clear text") : "disconnected")}";
            
            AddLogMessage(statusMessage);
        }

        private void OnNakReplyReceived(object sender, ControlPanel.NakReplyEventArgs args)
        {
            _lastNak.TryRemove(args.Address, out var lastNak);
            _lastNak.TryAdd(args.Address, args);
            
            if (lastNak != null && lastNak.Address == args.Address &&
                lastNak.Nak.ErrorCode == args.Nak.ErrorCode)
            {
                return;
            }

            AddLogMessage($"!!! Received NAK reply for address {args.Address} !!!\n{args.Nak}");
        }

        private void OnLocalStatusReportReplyReceived(object sender, ControlPanel.LocalStatusReportReplyEventArgs args)
        {
            AddLogMessage($"Local status updated for address {args.Address}\n{args.LocalStatus}");
        }

        private void OnInputStatusReportReplyReceived(object sender, ControlPanel.InputStatusReportReplyEventArgs args)
        {
            AddLogMessage($"Input status updated for address {args.Address}\n{args.InputStatus}");
        }

        private void OnOutputStatusReportReplyReceived(object sender, ControlPanel.OutputStatusReportReplyEventArgs args)
        {
            AddLogMessage($"Output status updated for address {args.Address}\n{args.OutputStatus}");
        }

        private void OnReaderStatusReportReplyReceived(object sender, ControlPanel.ReaderStatusReportReplyEventArgs args)
        {
            AddLogMessage($"Reader tamper status updated for address {args.Address}\n{args.ReaderStatus}");
        }

        private void OnRawCardDataReplyReceived(object sender, ControlPanel.RawCardDataReplyEventArgs args)
        {
            AddLogMessage($"Received raw card data reply for address {args.Address}\n{args.RawCardData}");
        }

        private void OnKeypadReplyReceived(object sender, ControlPanel.KeypadReplyEventArgs args)
        {
            AddLogMessage($"Received keypad data reply for address {args.Address}\n{args.KeypadData}");
        }

        public void Dispose()
        {
            try
            {
                if (_controlPanel != null)
                {
                    var shutdownTask = _controlPanel.Shutdown();
                    shutdownTask?.Wait();
                }
                _loggerFactory?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disposal: {ex.Message}");
                // Don't re-throw to allow graceful shutdown
            }
        }
    }

    /// <summary>
    /// Result of a file transfer operation containing both fragment count and status
    /// </summary>
    public class FileTransferResult
    {
        /// <summary>
        /// Number of fragments sent during the file transfer
        /// </summary>
        public int FragmentCount { get; set; }

        /// <summary>
        /// Final status returned from the device
        /// </summary>
        public OSDP.Net.Model.ReplyData.FileTransferStatus.StatusDetail Status { get; set; }
    }
}