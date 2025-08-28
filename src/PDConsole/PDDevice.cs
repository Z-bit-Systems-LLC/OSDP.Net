using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OSDP.Net;
using OSDP.Net.Model;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using PDConsole.Configuration;
using CommunicationConfiguration = OSDP.Net.Model.CommandData.CommunicationConfiguration;

namespace PDConsole
{
    public class PDDevice(DeviceConfiguration config, DeviceSettings settings, ILoggerFactory loggerFactory = null)
        : Device(config, loggerFactory)
    {
        private readonly List<CommandEvent> _commandHistory = new();
        
        public event EventHandler<CommandEvent> CommandReceived;
        
        protected override PayloadData HandleIdReport()
        {
            LogCommand("ID Report");
            
            var vendorCode = ConvertHexStringToBytes(settings.VendorCode, 3);
            return new DeviceIdentification(
                vendorCode,
                (byte)settings.Model[0],
                settings.FirmwareMajor,
                settings.FirmwareMinor,
                settings.FirmwareBuild,
                (byte)ConvertStringToBytes(settings.SerialNumber, 4),
                settings.FirmwareBuild);
        }
        
        protected override PayloadData HandleDeviceCapabilities()
        {
            LogCommand("Device Capabilities");
            return new DeviceCapabilities(settings.Capabilities.ToArray());
        }
        
        protected override PayloadData HandleCommunicationSet(CommunicationConfiguration commandPayload)
        {
            LogCommand("Communication Set", commandPayload.ToString());
            
            return new OSDP.Net.Model.ReplyData.CommunicationConfiguration(
                commandPayload.Address,
                commandPayload.BaudRate);
        }
        
        protected override PayloadData HandleKeySettings(EncryptionKeyConfiguration commandPayload)
        {
            LogCommand("Key Settings", commandPayload);
            return new Ack();
        }
        
        // Override other handlers to just return ACK or NAK
        protected override PayloadData HandleLocalStatusReport()
        {
            LogCommand("Local Status Report");
            return new Ack(); // Simplified - just return ACK
        }
        
        protected override PayloadData HandleInputStatusReport()
        {
            LogCommand("Input Status Report");
            return new Ack(); // Simplified - just return ACK
        }
        
        protected override PayloadData HandleOutputStatusReport()
        {
            LogCommand("Output Status Report");
            return new Ack(); // Simplified - just return ACK
        }
        
        protected override PayloadData HandleReaderStatusReport()
        {
            LogCommand("Reader Status Report");
            return new Ack(); // Simplified - just return ACK
        }
        
        protected override PayloadData HandleReaderLEDControl(ReaderLedControls commandPayload)
        {
            LogCommand("LED Control", commandPayload);
            return new Ack();
        }
        
        protected override PayloadData HandleBuzzerControl(ReaderBuzzerControl commandPayload)
        {
            LogCommand("Buzzer Control", commandPayload);
            return new Ack();
        }
        
        protected override PayloadData HandleTextOutput(ReaderTextOutput commandPayload)
        {
            LogCommand("Text Output", commandPayload);
            return new Ack();
        }
        
        protected override PayloadData HandleOutputControl(OutputControls commandPayload)
        {
            LogCommand("Output Control", commandPayload);
            return new Ack();
        }
        
        protected override PayloadData HandleBiometricRead(BiometricReadData commandPayload)
        {
            LogCommand("Biometric Read", commandPayload);
            return new Nak(ErrorCode.UnableToProcessCommand);
        }
        
        protected override PayloadData HandleManufacturerCommand(OSDP.Net.Model.CommandData.ManufacturerSpecific commandPayload)
        {
            LogCommand("Manufacturer Specific", commandPayload);
            return new Ack();
        }
        
        protected override PayloadData HandlePivData(GetPIVData commandPayload)
        {
            LogCommand("Get PIV Data", commandPayload);
            return new Nak(ErrorCode.UnableToProcessCommand);
        }
        
        protected override PayloadData HandleAbortRequest()
        {
            LogCommand("Abort Request");
            return new Ack();
        }
        
        // Method to send a simulated card read
        public void SendSimulatedCardRead(string cardData)
        {
            if (!string.IsNullOrEmpty(cardData))
            {
                try
                {
                    var cardBytes = ConvertHexStringToBytes(cardData, cardData.Length / 2);
                    var bitArray = new BitArray(cardBytes);
                    
                    // Enqueue the card data reply for the next poll
                    EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, bitArray));
                    LogCommand("Simulated Card Read", new { CardData = cardData });
                }
                catch (Exception)
                {
                    LogCommand("Error Simulating Card Read");
                }
            }
        }
        
        // Method to simulate keypad entry (using formatted card data as a workaround)
        public void SimulateKeypadEntry(string keys)
        {
            if (string.IsNullOrEmpty(keys)) return;
            
            try
            {
                // Note: KeypadData doesn't inherit from PayloadData, so we use FormattedCardData as a workaround
                EnqueuePollReply(new FormattedCardData(0, ReadDirection.Forward, keys));
                LogCommand("Simulated Keypad Entry", new { Keys = keys });
            }
            catch (Exception)
            {
                LogCommand("Error Simulating Keypad Entry");
            }
        }
        
        private void LogCommand(string commandDescription, object payload = null)
        {
            var commandEvent = new CommandEvent
            {
                Timestamp = DateTime.Now,
                Description = commandDescription,
                Details = payload?.ToString() ?? string.Empty
            };
            
            _commandHistory.Add(commandEvent);
            if (_commandHistory.Count > 100) // Keep only the last 100 commands
            {
                _commandHistory.RemoveAt(0);
            }
            
            CommandReceived?.Invoke(this, commandEvent);
        }
        
        private static byte[] ConvertHexStringToBytes(string hex, int expectedLength)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            var bytes = new byte[expectedLength];
            
            for (int i = 0; i < Math.Min(hex.Length / 2, expectedLength); i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            
            return bytes;
        }
        
        private static uint ConvertStringToBytes(string str, int byteCount)
        {
            uint result = 0;
            for (int i = 0; i < Math.Min(str.Length, byteCount); i++)
            {
                result = (result << 8) | str[i];
            }
            return result;
        }
    }
    
    public class CommandEvent
    {
        public DateTime Timestamp { get; init; }
        public string Description { get; init; }
        public string Details { get; init; }
    }
}