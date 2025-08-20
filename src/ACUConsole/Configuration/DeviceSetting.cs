namespace ACUConsole.Configuration
{
    public class DeviceSetting
    {
        public static readonly byte[] DefaultKey =
            {0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F};

        public string Name { get; set; }

        public byte Address { get; set; }

        public bool UseSecureChannel { get; set; } = false;

        public bool UseCrc { get; set; } = true;

        public byte[] SecureChannelKey { get; set; } = DefaultKey;
    }
}