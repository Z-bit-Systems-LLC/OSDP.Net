using System.Linq;
using OSDP.Net.Connections;

namespace ACUConsole
{
    /// <summary>
    /// Shared constants used across the ACUConsole application
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Standard baud rates supported by OSDP devices as strings for UI display
        /// </summary>
        public static readonly string[] StandardBaudRates =
            SerialPortOsdpConnection.StandardBaudRates.Select(r => r.ToString()).ToArray();
    }
}
