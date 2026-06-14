using System.Collections.Generic;
using OSDP.Net.Messages.SecureChannel;
using OSDP.Net.Model.ReplyData;

namespace PDConsole.Configuration
{
    public class Settings
    {
        public ConnectionSettings Connection { get; set; } = new();

        public DeviceSettings Device { get; set; } = new();

        public SecuritySettings Security { get; set; } = new();

        public SimulationSettings Simulation { get; set; } = new();

        public bool EnableLogging { get; set; } = true;

        public bool EnableTracing { get; set; } = false;
    }
    
    public class ConnectionSettings
    {
        public ConnectionType Type { get; set; } = ConnectionType.Serial;
        
        public string SerialPortName { get; set; } = "COM3";
        
        public int SerialBaudRate { get; set; } = 9600;
        
        public string TcpServerAddress { get; set; } = "0.0.0.0";
        
        public int TcpServerPort { get; set; } = 12000;
    }
    
    public enum ConnectionType
    {
        Serial,
        TcpServer
    }
    
    public class DeviceSettings
    {
        public byte Address { get; set; } = 0;

        public bool UseCrc { get; set; } = true;

        public string VendorCode { get; set; } = "000000";

        public string Model { get; set; } = "PDConsole";

        public string SerialNumber { get; set; } = "123456789";

        public byte FirmwareMajor { get; set; } = 1;

        public byte FirmwareMinor { get; set; } = 0;

        public byte FirmwareBuild { get; set; } = 0;

        public List<DeviceCapability> Capabilities { get; set; } = new()
        {
            new DeviceCapability(CapabilityFunction.CardDataFormat, 1, 1),
            new DeviceCapability(CapabilityFunction.ReaderLEDControl, 1, 2),
            new DeviceCapability(CapabilityFunction.ReaderAudibleOutput, 1, 1),
            new DeviceCapability(CapabilityFunction.ReaderTextOutput, 1, 1),
            new DeviceCapability(CapabilityFunction.CheckCharacterSupport, 1, 0),
            new DeviceCapability(CapabilityFunction.CommunicationSecurity, 1, 1),
            new DeviceCapability(CapabilityFunction.OSDPVersion, 2, 0),
            new DeviceCapability(CapabilityFunction.ExtendedIdResponse, 1, 0)
        };

        // Extended ID settings
        public ExtendedIdSettings ExtendedId { get; set; } = new();
    }

    public class ExtendedIdSettings
    {
        /// <summary>
        /// Manufacturer name for extended ID response. Uses vendor code by default.
        /// </summary>
        public string Manufacturer { get; set; } = "PDConsole Simulator";

        /// <summary>
        /// Hardware description for extended ID response.
        /// </summary>
        public string HardwareDescription { get; set; } = "Virtual PD";

        /// <summary>
        /// URL for extended ID response.
        /// </summary>
        public string Url { get; set; } = "";

        /// <summary>
        /// Configuration reference for extended ID response.
        /// </summary>
        public string ConfigurationReference { get; set; } = "";

        /// <summary>
        /// Additional firmware version entries (e.g., for multiple microcontrollers).
        /// </summary>
        public List<string> AdditionalFirmwareVersions { get; set; } = new();
    }
    
    /// <summary>
    /// Secure channel operating mode for the PD.
    /// </summary>
    public enum SecureChannelMode
    {
        /// <summary>Secure channel disabled; all communication is in the clear.</summary>
        ClearText,

        /// <summary>Secure channel using the well-known default key (SCBK-D) for first-time keying.</summary>
        Install,

        /// <summary>Secure channel using the configured per-installation key (SCBK).</summary>
        Secure
    }

    public class SecuritySettings
    {
        /// <summary>
        /// The well-known default secure channel key (SCBK-D) used during install mode.
        /// </summary>
        public static readonly byte[] DefaultKey =
            [0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F];

        /// <summary>
        /// The well-known default SC2 secure channel key (32 bytes) used during install mode when
        /// <see cref="SecureChannelVersion"/> is <see cref="SecureChannelVersion.V2"/>.
        /// </summary>
        public static readonly byte[] DefaultSC2Key =
        [
            0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47,
            0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
            0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57,
            0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F
        ];

        /// <summary>
        /// The secure channel operating mode.
        /// </summary>
        public SecureChannelMode SecureChannelMode { get; set; } = SecureChannelMode.ClearText;

        /// <summary>
        /// The secure channel base key (SCBK) as a hex string. Only used when
        /// <see cref="SecureChannelMode"/> is <see cref="SecureChannelMode.Secure"/>.
        /// 16 bytes (32 hex chars) for SC1, 32 bytes (64 hex chars) for SC2.
        /// </summary>
        public string SecureChannelKey { get; set; } = "303132333435363738393A3B3C3D3E3F";

        /// <summary>
        /// The secure channel protocol version (V1 = AES-128 CBC, V2 = AES-256 GCM).
        /// </summary>
        public SecureChannelVersion SecureChannelVersion { get; set; } = SecureChannelVersion.V1;
    }

    public class SimulationSettings
    {
        public string CardNumber { get; set; } = "01010101010101010101010101";

        public string PinNumber { get; set; } = "1234#";
    }
}