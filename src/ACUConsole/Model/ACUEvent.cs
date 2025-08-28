using System;

namespace ACUConsole.Model
{
    /// <summary>
    /// Represents an event or message in the ACU Console
    /// </summary>
    public class ACUEvent
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public ACUEventType Type { get; init; } = ACUEventType.Information;
        public byte? DeviceAddress { get; init; }

        public override string ToString()
        {
            var deviceInfo = DeviceAddress.HasValue ? $" [Device {DeviceAddress}]" : string.Empty;
            return $"{Timestamp:HH:mm:ss.fff}{deviceInfo} - {Title}: {Message}";
        }
    }

    public enum ACUEventType
    {
        Information,
        Warning,
        Error,
        DeviceReply,
        ConnectionStatus,
        CommandSent
    }
}