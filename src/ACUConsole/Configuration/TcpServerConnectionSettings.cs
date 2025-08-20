namespace ACUConsole.Configuration
{
    public class TcpServerConnectionSettings
    {
        public int PortNumber { get; set; } = 5000;

        public int BaudRate { get; set; } = 9600;

        public int ReplyTimeout { get; set; } = 200;
    }
}