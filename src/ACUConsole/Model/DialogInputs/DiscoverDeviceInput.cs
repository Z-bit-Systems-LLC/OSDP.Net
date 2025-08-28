namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Input data for Discover Device dialog
    /// </summary>
    public class DiscoverDeviceInput
    {
        public string PortName { get; set; } = string.Empty;
        public int PingTimeout { get; set; }
        public int ReconnectDelay { get; set; }
        public bool WasCancelled { get; set; }
    }
}