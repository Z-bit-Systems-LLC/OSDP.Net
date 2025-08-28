using System;
using System.Collections.Generic;
using System.Threading;
using OSDP.Net;
using OSDP.Net.Connections;
using PDConsole.Configuration;

namespace PDConsole
{
    /// <summary>
    /// Controller class that manages the PDConsole business logic and device interactions
    /// </summary>
    public class PDConsoleController(Settings settings) : IPDConsoleController
    {
        private readonly Settings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        private readonly List<CommandEvent> _commandHistory = new();
        
        private PDDevice _device;
        private IOsdpConnectionListener _connectionListener;
        private CancellationTokenSource _cancellationTokenSource;

        // Events
        public event EventHandler<CommandEvent> CommandReceived;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<Exception> ErrorOccurred;

        // Properties
        public bool IsDeviceRunning => _device != null && _connectionListener != null;
        public IReadOnlyList<CommandEvent> CommandHistory => _commandHistory.AsReadOnly();
        public Settings Settings => _settings;

        // Device Control Methods
        public void StartDevice()
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
                _device.StartListening(_connectionListener);

                var connectionString = GetConnectionString();
                ConnectionStatusChanged?.Invoke(this, $"Listening on {connectionString}");
                StatusChanged?.Invoke(this, "Device started successfully");
            }
            catch (Exception ex)
            {
                StopDevice();
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        public void StopDevice()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _connectionListener?.Dispose();

                if (_device != null)
                {
                    _device.CommandReceived -= OnDeviceCommandReceived;
                    _ = _device.StopListening();
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
            StopDevice();
            _cancellationTokenSource?.Dispose();
        }
    }
}