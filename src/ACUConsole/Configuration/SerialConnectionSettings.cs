namespace ACUConsole.Configuration
{
    public class SerialConnectionSettings
    {
        public string PortName { get; set; } = "/dev/tty.SLAB_USBtoUART";

        public int BaudRate { get; set; } = 9600;

        public int ReplyTimeout { get; set; } = 200;
    }
}