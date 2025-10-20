using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OSDP.Net;
using OSDP.Net.Connections;
using PDConsole.Configuration;

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
                var deviceConfig = new DeviceConfiguration
                {
                    Address = _settings.Device.Address,
                    RequireSecurity = _settings.Security.RequireSecureChannel,
                    SecurityKey = _settings.Security.SecureChannelKey
                };

                // Create the device
                _device = new PDDevice(deviceConfig, _settings.Device);
                _device.CommandReceived += OnDeviceCommandReceived;

                // Create a connection listener based on type
                _connectionListener = CreateConnectionListener();

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
            return $"Address: {_settings.Device.Address} | Security: {(_settings.Security.RequireSecureChannel ? "Enabled" : "Disabled")}";
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
                    PropertyNameCaseInsensitive = true
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
                    WriteIndented = true
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

        public void Dispose()
        {
            _ = StopDevice();
            _cancellationTokenSource?.Dispose();
        }
    }
}