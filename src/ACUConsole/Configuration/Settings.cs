using System.Collections.Generic;

namespace ACUConsole.Configuration
{
    public class Settings
    {
        public SerialConnectionSettings SerialConnectionSettings { get; set; } = new ();

        public TcpServerConnectionSettings TcpServerConnectionSettings { get; set; } = new ();

        public TcpClientConnectionSettings TcpClientConnectionSettings { get; set; } = new ();

        public List<DeviceSetting> Devices { get; set; } = new ();

        public int PollingInterval { get; set; } = 200;

        public string LastFileTransferDirectory { get; set; }

        public bool IsTracing { get; set; }
    }
}