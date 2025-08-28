namespace ACUConsole.Model.DialogInputs
{
    /// <summary>
    /// Data transfer object for TCP server connection dialog input
    /// </summary>
    public class TcpServerConnectionInput
    {
        public int PortNumber { get; set; }
        public int BaudRate { get; set; }
        public int ReplyTimeout { get; set; }
        public bool WasCancelled { get; set; }
    }
}