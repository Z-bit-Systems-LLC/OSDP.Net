using System;
using System.Collections.Generic;
using PDConsole.Configuration;

namespace PDConsole
{
    /// <summary>
    /// Interface for PDConsole controller to enable testing and alternative implementations
    /// </summary>
    public interface IPDConsoleController : IDisposable
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

        // Methods
        void StartDevice();
        void StopDevice();
        void SendSimulatedCardRead(string cardData);
        void SimulateKeypadEntry(string keys);
        void ClearHistory();
        string GetDeviceStatusText();
    }
}