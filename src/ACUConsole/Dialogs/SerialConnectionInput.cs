namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Data transfer object for serial connection dialog input
    /// </summary>
    public class SerialConnectionInput
    {
        public string PortName { get; set; } = string.Empty;
        public int BaudRate { get; set; }
        public int ReplyTimeout { get; set; }
        public bool WasCancelled { get; set; }
    }
}