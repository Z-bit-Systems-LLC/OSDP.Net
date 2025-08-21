namespace ACUConsole.Dialogs
{
    /// <summary>
    /// Data transfer object for TCP client connection dialog input
    /// </summary>
    public class TcpClientConnectionInput
    {
        public string Host { get; set; } = string.Empty;
        public int PortNumber { get; set; }
        public int BaudRate { get; set; }
        public int ReplyTimeout { get; set; }
        public bool WasCancelled { get; set; }
    }
}