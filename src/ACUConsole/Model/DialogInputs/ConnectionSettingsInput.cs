namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for connection settings dialog input
    /// </summary>
    public class ConnectionSettingsInput
    {
        public int PollingInterval { get; set; }
        public bool IsTracing { get; set; }
        public bool WasCancelled { get; set; }
    }
}