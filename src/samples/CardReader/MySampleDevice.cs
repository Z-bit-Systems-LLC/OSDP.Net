using Microsoft.Extensions.Logging;
using OSDP.Net;
using OSDP.Net.Model;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using CommunicationConfiguration = OSDP.Net.Model.CommandData.CommunicationConfiguration;
using DeviceCapabilities = OSDP.Net.Model.ReplyData.DeviceCapabilities;

namespace CardReader;

internal class MySampleDevice : Device
{
    public MySampleDevice(DeviceConfiguration config, ILoggerFactory loggerFactory)
        : base(config, loggerFactory) { }

    protected override PayloadData HandleIdReport()
    {
        return new DeviceIdentification([0x01, 0x02, 0x03], 4, 5, 6, 7, 8, 9);
    }

    protected override PayloadData HandleDeviceCapabilities()
    {
        var deviceCapabilities = new DeviceCapabilities(new[]
        {
            new DeviceCapability(CapabilityFunction.CardDataFormat, 1, 0),
            new DeviceCapability(CapabilityFunction.ReaderLEDControl, 1, 0),
            new DeviceCapability(CapabilityFunction.ReaderTextOutput, 0, 0),
            new DeviceCapability(CapabilityFunction.CheckCharacterSupport, 1, 0),
            new DeviceCapability(CapabilityFunction.CommunicationSecurity, 1, 1),
            new DeviceCapability(CapabilityFunction.ReceiveBufferSize, 0, 1),
            new DeviceCapability(CapabilityFunction.OSDPVersion, 2, 0)
        });

        return deviceCapabilities;
    }

    protected override PayloadData HandleCommunicationSet(CommunicationConfiguration commandPayload)
    {
        // Update the settings in persistent storage
        
        return new OSDP.Net.Model.ReplyData.CommunicationConfiguration(commandPayload.Address, commandPayload.BaudRate);
    }

    protected override PayloadData HandleKeySettings(EncryptionKeyConfiguration commandPayload)
    {
        // Update the encryption key settings in persistent storage
        Console.WriteLine("Received a KeySet command with a key of - " + BitConverter.ToString(commandPayload.KeyData));

        return new Ack();
    }
}
