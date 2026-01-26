namespace ACUConsole
{
    /// <summary>
    /// Shared constants used across the ACUConsole application
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Standard baud rates supported by OSDP devices
        /// </summary>
        public static readonly string[] StandardBaudRates =
        [
            "9600",
            "19200",
            "38400",
            "57600",
            "115200",
            "230400",
            "460800"
        ];
    }
}
