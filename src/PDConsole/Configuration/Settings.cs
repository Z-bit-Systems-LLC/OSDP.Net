using System.Collections.Generic;
using OSDP.Net.Model.ReplyData;

namespace PDConsole.Configuration
{
    public class Settings
    {
        public ConnectionSettings Connection { get; set; } = new();
        
        public DeviceSettings Device { get; set; } = new();
        
        public SecuritySettings Security { get; set; } = new();
        
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
            new DeviceCapability(CapabilityFunction.OSDPVersion, 2, 0)
        };
    }
    
    public class SecuritySettings
    {
        public static readonly byte[] DefaultKey =
            [0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F];
        
        public bool RequireSecureChannel { get; set; } = false;
        
        public byte[] SecureChannelKey { get; set; } = DefaultKey;
    }
}