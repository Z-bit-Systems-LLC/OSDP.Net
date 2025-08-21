using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ACUConsole.Configuration;
using ACUConsole.Model;
using OSDP.Net.Connections;
using OSDP.Net.Model.CommandData;

namespace ACUConsole
{
    /// <summary>
    /// Interface for ACU Console presenter to enable testing and separation of concerns
    /// </summary>
    public interface IACUConsolePresenter : IDisposable
    {
        // Events
        event EventHandler<ACUEvent> MessageReceived;
        event EventHandler<string> StatusChanged;
        event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;
        event EventHandler<Exception> ErrorOccurred;

        // Properties
        bool IsConnected { get; }
        Guid ConnectionId { get; }
        IReadOnlyList<ACUEvent> MessageHistory { get; }
        Settings Settings { get; }

        // Connection Methods
        Task StartSerialConnection(string portName, int baudRate, int replyTimeout);
        Task StartTcpServerConnection(int portNumber, int baudRate, int replyTimeout);
        Task StartTcpClientConnection(string host, int portNumber, int baudRate, int replyTimeout);
        Task StopConnection();

        // Device Management Methods
        void AddDevice(string name, byte address, bool useCrc, bool useSecureChannel, byte[] secureChannelKey);
        void RemoveDevice(byte address);
        Task<string> DiscoverDevice(string portName, int pingTimeout, int reconnectDelay, CancellationToken cancellationToken = default);

        // Command Methods
        Task SendDeviceCapabilities(byte address);
        Task SendIdReport(byte address);
        Task SendInputStatus(byte address);
        Task SendLocalStatus(byte address);
        Task SendOutputStatus(byte address);
        Task SendReaderStatus(byte address);
        Task SendCommunicationConfiguration(byte address, byte newAddress, int newBaudRate);
        Task SendOutputControl(byte address, byte outputNumber, bool activate);
        Task SendReaderLedControl(byte address, byte ledNumber, LedColor color);
        Task SendReaderBuzzerControl(byte address, byte readerNumber, byte repeatTimes);
        Task SendReaderTextOutput(byte address, byte readerNumber, string text);
        Task SendManufacturerSpecific(byte address, byte[] vendorCode, byte[] data);
        Task SendEncryptionKeySet(byte address, byte[] key);
        Task SendBiometricRead(byte address, byte readerNumber, byte type, byte format, byte quality);
        Task SendBiometricMatch(byte address, byte readerNumber, byte type, byte format, byte qualityThreshold, byte[] templateData);
        Task<int> SendFileTransfer(byte address, byte type, byte[] data, byte messageSize);

        // Custom Commands
        Task SendCustomCommand(byte address, CommandData commandData);

        // Configuration Methods
        void UpdateConnectionSettings(int pollingInterval, bool isTracing);
        void SaveConfiguration();
        void LoadConfiguration();
        void ParseOSDPCapFile(string filePath, byte? filterAddress, bool ignorePollsAndAcks, byte[] key);

        // Utility Methods
        void ClearHistory();
        void AddLogMessage(string message);
        bool CanSendCommand();
        string[] GetDeviceList();
        
        // View Management
        void SetView(IACUConsoleView view);
    }

    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public byte Address { get; init; }
        public bool IsConnected { get; init; }
        public bool IsSecureChannelEstablished { get; init; }
        public string DeviceName { get; init; }
    }
}