using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Logging;
using OSDP.Net;
using OSDP.Net.Connections;
using OSDP.Net.Model;
using PDConsole.Configuration;
using PDConsole.Tracing;

namespace PDConsole
{
    /// <summary>
    /// Presenter class that manages the PDConsole business logic and device interactions
    /// </summary>
    public class PDConsolePresenter(Settings settings) : IPDConsolePresenter
    {
        private Settings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        private readonly List<CommandEvent> _commandHistory = new();

        private PDDevice _device;
        private IOsdpConnectionListener _connectionListener;
        private CancellationTokenSource _cancellationTokenSource;
        private string _currentSettingsFilePath;
        private ILoggerFactory _loggerFactory;
        private PDPacketCaptureTracer _packetCaptureTracer;

        // Events
        public event EventHandler<CommandEvent> CommandReceived;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<Exception> ErrorOccurred;

        // Properties
        public bool IsDeviceRunning => _device != null && _connectionListener != null;
        public IReadOnlyList<CommandEvent> CommandHistory => _commandHistory.AsReadOnly();
        public Settings Settings => _settings;
        public string CurrentSettingsFilePath => _currentSettingsFilePath;

        // Device Control Methods
        public async Task StartDevice()
        {
            if (IsDeviceRunning)
            {
                throw new InvalidOperationException("Device is already running");
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();

                // Create device configuration
                var vendorCode = ConvertHexStringToBytes(_settings.Device.VendorCode, 3);
                var serialNumber = ParseSerialNumber(_settings.Device.SerialNumber);
                var (requireSecurity, securityKey) = ResolveSecurity(_settings.Security);
                var deviceConfig = new DeviceConfiguration(new ClientIdentification(vendorCode, serialNumber))
                {
                    Address = _settings.Device.Address,
                    RequireSecurity = requireSecurity,
                    SecurityKey = securityKey
                };

                // Wire up logging if enabled
                ILoggerFactory loggerFactory = null;
                if (_settings.EnableLogging)
                {
                    EnsureLoggerFactory();
                    loggerFactory = _loggerFactory;
                }

                // Create the device
                _device = new PDDevice(deviceConfig, _settings.Device, loggerFactory);
                _device.CommandReceived += OnDeviceCommandReceived;
                _device.EncryptionKeyChanged += OnEncryptionKeyChanged;

                // Create a connection listener based on type, optionally wrapped with packet capture
                var listener = CreateConnectionListener();
                if (_settings.EnableTracing)
                {
                    _packetCaptureTracer = new PDPacketCaptureTracer();
                    listener = new TracingConnectionListener(listener, _packetCaptureTracer);
                }
                _connectionListener = listener;

                // Start listening
                await _device.StartListening(_connectionListener);

                var connectionString = GetConnectionString();
                ConnectionStatusChanged?.Invoke(this, $"Listening on {connectionString}");
                StatusChanged?.Invoke(this, "Device started successfully");
            }
            catch (Exception ex)
            {
                await StopDevice();
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        public async Task StopDevice()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _connectionListener?.Dispose();

                if (_device != null)
                {
                    _device.CommandReceived -= OnDeviceCommandReceived;
                    _device.EncryptionKeyChanged -= OnEncryptionKeyChanged;
                    await _device.StopListening();
                }

                ConnectionStatusChanged?.Invoke(this, "Not Started");
                StatusChanged?.Invoke(this, "Device stopped");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            finally
            {
                _packetCaptureTracer?.Dispose();
                _packetCaptureTracer = null;
                _device = null;
                _connectionListener = null;
                _cancellationTokenSource = null;
            }
        }

        public void SendSimulatedCardRead(string cardData)
        {
            if (!IsDeviceRunning)
            {
                throw new InvalidOperationException("Device is not running");
            }

            if (string.IsNullOrEmpty(cardData))
            {
                throw new ArgumentException("Card data cannot be empty", nameof(cardData));
            }

            _device.SendSimulatedCardRead(cardData);
        }

        public void SimulateKeypadEntry(string keys)
        {
            if (!IsDeviceRunning)
            {
                throw new InvalidOperationException("Device is not running");
            }

            if (string.IsNullOrEmpty(keys))
            {
                throw new ArgumentException("Keypad data cannot be empty", nameof(keys));
            }

            _device.SimulateKeypadEntry(keys);
        }

        public void ClearHistory()
        {
            _commandHistory.Clear();
            StatusChanged?.Invoke(this, "Command history cleared");
        }

        public string GetDeviceStatusText()
        {
            return $"Address: {_settings.Device.Address} | Security: {_settings.Security.SecureChannelMode}";
        }

        // Settings Management Methods
        public void LoadSettings(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    throw new ArgumentException("File path cannot be empty", nameof(filePath));
                }

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Settings file not found: {filePath}");
                }

                if (IsDeviceRunning)
                {
                    throw new InvalidOperationException("Cannot load settings while device is running. Stop the device first.");
                }

                var json = File.ReadAllText(filePath);
                _settings = JsonSerializer.Deserialize<Settings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                }) ?? new Settings();

                _currentSettingsFilePath = filePath;
                StatusChanged?.Invoke(this, $"Settings loaded from {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        public void SaveSettings(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    throw new ArgumentException("File path cannot be empty", nameof(filePath));
                }

                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                });

                File.WriteAllText(filePath, json);
                _currentSettingsFilePath = filePath;
                StatusChanged?.Invoke(this, $"Settings saved to {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        public void SetCurrentSettingsFilePath(string filePath)
        {
            _currentSettingsFilePath = filePath;
        }

        public void UpdateSerialConnection(string portName, int baudRate)
        {
            if (IsDeviceRunning)
            {
                throw new InvalidOperationException("Cannot update connection settings while device is running. Stop the device first.");
            }

            _settings.Connection.SerialPortName = portName;
            _settings.Connection.SerialBaudRate = baudRate;
            StatusChanged?.Invoke(this, "Serial connection settings updated");
        }

        public void UpdateSimulationSettings(string cardNumber, string pinNumber)
        {
            _settings.Simulation.CardNumber = cardNumber ?? _settings.Simulation.CardNumber;
            _settings.Simulation.PinNumber = pinNumber ?? _settings.Simulation.PinNumber;
        }

        // Private Methods
        private IOsdpConnectionListener CreateConnectionListener()
        {
            switch (_settings.Connection.Type)
            {
                case ConnectionType.Serial:
                    return new SerialPortConnectionListener(
                        _settings.Connection.SerialPortName,
                        _settings.Connection.SerialBaudRate);

                case ConnectionType.TcpServer:
                    return new TcpConnectionListener(
                        _settings.Connection.TcpServerPort,
                        9600);

                default:
                    throw new NotSupportedException($"Connection type {_settings.Connection.Type} not supported");
            }
        }

        private string GetConnectionString()
        {
            return _settings.Connection.Type switch
            {
                ConnectionType.Serial => $"{_settings.Connection.SerialPortName} @ {_settings.Connection.SerialBaudRate}",
                ConnectionType.TcpServer => $"{_settings.Connection.TcpServerAddress}:{_settings.Connection.TcpServerPort}",
                _ => "Unknown"
            };
        }

        private void OnDeviceCommandReceived(object sender, CommandEvent e)
        {
            _commandHistory.Add(e);

            // Keep only the last 100 commands
            if (_commandHistory.Count > 100)
            {
                _commandHistory.RemoveAt(0);
            }

            CommandReceived?.Invoke(this, e);
        }

        /// <summary>
        /// Persists a new secure channel key set by the ACU via osdp_KEYSET and switches the
        /// stored mode to Secure so subsequent runs use the new key.
        /// </summary>
        private void OnEncryptionKeyChanged(object sender, byte[] newKey)
        {
            // A KEYSET only completes over an established secure channel (install or secure mode).
            if (_settings.Security.SecureChannelMode == SecureChannelMode.ClearText)
            {
                return;
            }

            _settings.Security.SecureChannelKey = Convert.ToHexString(newKey);
            _settings.Security.SecureChannelMode = SecureChannelMode.Secure;

            StatusChanged?.Invoke(this, "Secure channel key updated by ACU");

            try
            {
                if (!string.IsNullOrWhiteSpace(_currentSettingsFilePath))
                {
                    SaveSettings(_currentSettingsFilePath);
                }
            }
            catch
            {
                // SaveSettings already surfaced the failure via ErrorOccurred; swallow here so the
                // KEYSET reply is still sent to the ACU.
            }
        }

        /// <summary>
        /// Maps the configured <see cref="SecureChannelMode"/> to the library's
        /// RequireSecurity/SecurityKey pair. Install mode keys with the well-known default key,
        /// Secure mode uses the configured key, and ClearText disables the secure channel.
        /// </summary>
        private static (bool RequireSecurity, byte[] SecurityKey) ResolveSecurity(SecuritySettings security)
        {
            switch (security.SecureChannelMode)
            {
                case SecureChannelMode.Install:
                    return (true, SecuritySettings.DefaultKey);

                case SecureChannelMode.Secure:
                    return (true, ParseSecureChannelKey(security.SecureChannelKey));

                case SecureChannelMode.ClearText:
                default:
                    return (false, SecuritySettings.DefaultKey);
            }
        }

        private static byte[] ParseSecureChannelKey(string hexKey)
        {
            var cleaned = (hexKey ?? string.Empty).Replace(" ", "").Replace("-", "");

            byte[] key;
            try
            {
                key = Convert.FromHexString(cleaned);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Secure channel key must be a valid hex string.");
            }

            if (key.Length != 16)
            {
                throw new InvalidOperationException(
                    $"Secure channel key must be 16 bytes (32 hex characters), but was {key.Length} bytes.");
            }

            return key;
        }

        private static byte[] ConvertHexStringToBytes(string hex, int expectedLength)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            var bytes = new byte[expectedLength];

            for (int i = 0; i < Math.Min(hex.Length / 2, expectedLength); i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static uint ParseSerialNumber(string serialNumber)
        {
            if (string.IsNullOrEmpty(serialNumber))
            {
                return 0;
            }

            // Try to parse as a number first
            if (uint.TryParse(serialNumber, out var result))
            {
                return result;
            }

            // If not a number, hash the string to get a consistent serial number
            uint hash = 0;
            foreach (char c in serialNumber)
            {
                hash = (hash * 31) + c;
            }
            return hash;
        }

        public void Dispose()
        {
            _ = StopDevice();
            _cancellationTokenSource?.Dispose();
            _loggerFactory?.Dispose();
        }

        /// <summary>
        /// Lazily creates the log4net-backed logger factory used by the device, configuring
        /// log4net from log4net.config the first time it is needed.
        /// </summary>
        private void EnsureLoggerFactory()
        {
            if (_loggerFactory != null) return;

            ConfigureLog4Net();
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddLog4Net();
        }

        private static void ConfigureLog4Net()
        {
            var repository = LogManager.GetRepository(
                Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly());

            var configFile = new FileInfo("log4net.config");
            if (configFile.Exists)
            {
                XmlConfigurator.Configure(repository, configFile);
            }
        }
    }
}