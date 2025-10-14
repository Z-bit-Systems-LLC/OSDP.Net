using System;
using System.Collections.Generic;
using PDConsole.Configuration;

namespace PDConsole
{
    /// <summary>
    /// Interface for PDConsole presenter to enable testing and alternative implementations
    /// </summary>
    public interface IPDConsolePresenter : IDisposable
    {
        // Events
        event EventHandler<CommandEvent> CommandReceived;
        event EventHandler<string> StatusChanged;
        event EventHandler<string> ConnectionStatusChanged;
        event EventHandler<Exception> ErrorOccurred;

        // Properties
        bool IsDeviceRunning { get; }
        IReadOnlyList<CommandEvent> CommandHistory { get; }
        Settings Settings { get; }
        string CurrentSettingsFilePath { get; }

        // Methods
        void StartDevice();
        void StopDevice();
        void SendSimulatedCardRead(string cardData);
        void SimulateKeypadEntry(string keys);
        void ClearHistory();
        string GetDeviceStatusText();

        // Settings Methods
        void LoadSettings(string filePath);
        void SaveSettings(string filePath);
        void SetCurrentSettingsFilePath(string filePath);
    }
}