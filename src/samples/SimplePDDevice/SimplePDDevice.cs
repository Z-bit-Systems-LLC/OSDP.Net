using OSDP.Net;
using OSDP.Net.Model;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using CommunicationConfiguration = OSDP.Net.Model.CommandData.CommunicationConfiguration;

namespace SimplePDDevice;

/// <summary>
/// Simplified OSDP Peripheral Device implementation with only essential handlers
/// </summary>
public class SimplePDDevice(DeviceConfiguration config) : Device(config)
{
    /// <summary>
    /// Handle ID Report command - returns basic device identification
    /// </summary>
    protected override PayloadData HandleIdReport()
    {
        // Return simple device identification
        return new DeviceIdentification(
            vendorCode: [0x01, 0x02, 0x03],  // Vendor code (3 bytes)
            modelNumber: 1,                   // Model number
            version: 1,                       // Hardware version  
            serialNumber: 12345,              // Serial number
            firmwareMajor: 1,                 // Firmware major version
            firmwareMinor: 0,                 // Firmware minor version
            firmwareBuild: 0                  // Firmware build version
        );
    }

    /// <summary>
    /// Handle Device Capabilities command - returns supported capabilities
    /// </summary>
    protected override PayloadData HandleDeviceCapabilities()
    {
        var deviceCapabilities = new DeviceCapabilities([
            new DeviceCapability(CapabilityFunction.CardDataFormat, 1, 0),
            new DeviceCapability(CapabilityFunction.ReaderLEDControl, 1, 0),
            new DeviceCapability(CapabilityFunction.ReaderTextOutput, 0, 0),
            new DeviceCapability(CapabilityFunction.CheckCharacterSupport, 1, 0),
            new DeviceCapability(CapabilityFunction.CommunicationSecurity, 1, 1),
            new DeviceCapability(CapabilityFunction.ReceiveBufferSize, 0, 1),
            new DeviceCapability(CapabilityFunction.OSDPVersion, 2, 0)
        ]);

        return deviceCapabilities;
    }

    /// <summary>
    /// Handle Communication Set command - acknowledges communication settings
    /// </summary>
    protected override PayloadData HandleCommunicationSet(CommunicationConfiguration commandPayload)
    {
        // Simply acknowledge the new communication settings
        return new OSDP.Net.Model.ReplyData.CommunicationConfiguration(
            commandPayload.Address, 
            commandPayload.BaudRate
        );
    }

    /// <summary>
    /// Handle Key Settings command - acknowledges security key configuration
    /// </summary>
    protected override PayloadData HandleKeySettings(EncryptionKeyConfiguration commandPayload)
    {
        // Simply acknowledge the key settings
        return new Ack();
    }

}