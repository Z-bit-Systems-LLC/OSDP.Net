using System;

using OSDP.Net.Messages;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using DeviceCapabilities = OSDP.Net.Model.ReplyData.DeviceCapabilities;

namespace OSDP.Net.Model;

/// <summary>
/// Information details of a message packet
/// </summary>
public class Packet
{
    private readonly byte[] _rawPayloadData;
    private readonly byte[] _rawData;
    private readonly bool _isUsingDefaultKey;

    internal Packet(IncomingMessage message)
    {
        IncomingMessage = message;
            
        Address = message.Address;
        Sequence = message.Sequence;
        if (message.MessageType == MessageType.Command)
        {
            CommandType = (CommandType)message.Type;
        }
        else
        {
            ReplyType = (ReplyType)message.Type;
        }

        IsUsingCrc = message.IsUsingCrc;
        _rawPayloadData = message.Payload;
        _rawData = message.OriginalMessageData.ToArray();
        _isUsingDefaultKey = message.IsUsingDefaultKey;
    }
    
    internal IncomingMessage IncomingMessage { get; }

    /// <summary>
    /// Address of the message
    /// </summary>
    public byte Address { get; }
    
    /// <summary>
    /// Sequence number of the message
    /// </summary>
    public byte Sequence { get; }
    
    /// <summary>
    /// The type of command sent or null it is a reply
    /// </summary>
    public CommandType? CommandType { get; }
    
    /// <summary>
    /// The type of reply sent or null it is a command
    /// </summary>
    public ReplyType? ReplyType { get; }
    
    /// <summary>
    /// Is CRC being used
    /// </summary>
    public bool IsUsingCrc { get; }
    
    /// <summary>
    /// Parse the payload data into an object
    /// </summary>
    /// <returns>A message data object representation of the payload data</returns>
    public object ParsePayloadData()
    {
        if (IncomingMessage.HasSecureData && !IncomingMessage.IsValidMac)
        {
            return "*** Unable to parse secure payload data ***";
        }
        
        switch (CommandType)
        {
            case Messages.CommandType.Poll:
                return null;
            case Messages.CommandType.IdReport:
                return null;
            case Messages.CommandType.DeviceCapabilities:
                return null;
            case Messages.CommandType.LocalStatus:
                return null;
            case Messages.CommandType.InputStatus:
                return null;
            case Messages.CommandType.OutputStatus:
                return null;
            case Messages.CommandType.ReaderStatus:
                return null;
            case Messages.CommandType.OutputControl:
                return OutputControls.ParseData(RawPayloadData);
            case Messages.CommandType.LEDControl:
                return ReaderLedControls.ParseData(RawPayloadData);
            case Messages.CommandType.BuzzerControl:
                return ReaderBuzzerControl.ParseData(RawPayloadData);
            case Messages.CommandType.TextOutput:
                return ReaderTextOutput.ParseData(RawPayloadData);
            case Messages.CommandType.CommunicationSet:
                return CommandData.CommunicationConfiguration.ParseData(RawPayloadData);
            case Messages.CommandType.BioRead:
                return BiometricReadData.ParseData(RawPayloadData);
            case Messages.CommandType.BioMatch:
                return BiometricTemplateData.ParseData(RawPayloadData);
            case Messages.CommandType.KeySet:
                return EncryptionKeyConfiguration.ParseData(RawPayloadData);
            case Messages.CommandType.SessionChallenge:
                return _rawPayloadData;
            case Messages.CommandType.ServerCryptogram:
                return _rawPayloadData;
            case Messages.CommandType.MaxReplySize:
                return null;
            case Messages.CommandType.FileTransfer:
                return FileTransferFragment.ParseData(RawPayloadData);
            case Messages.CommandType.ManufacturerSpecific:
                return CommandData.ManufacturerSpecific.ParseData(RawPayloadData);
            case Messages.CommandType.ExtendedWrite:
                return null;
            case Messages.CommandType.Abort:
                return null;
            case Messages.CommandType.PivData:
                return GetPIVData.ParseData(RawPayloadData);
            case Messages.CommandType.GenerateChallenge:
                return null;
            case Messages.CommandType.AuthenticateChallenge:
                return MessageDataFragment.ParseData(RawPayloadData);
            case Messages.CommandType.KeepActive:
                return null;
        }

        switch (ReplyType)
        {
            case Messages.ReplyType.Ack:
                break;
            case Messages.ReplyType.Nak:
                return Nak.ParseData(RawPayloadData);
            case Messages.ReplyType.PdIdReport:
                return DeviceIdentification.ParseData(RawPayloadData);
            case Messages.ReplyType.PdCapabilitiesReport:
                return DeviceCapabilities.ParseData(RawPayloadData);
            case Messages.ReplyType.LocalStatusReport:
                return LocalStatus.ParseData(RawPayloadData);
            case Messages.ReplyType.InputStatusReport:
                return InputStatus.ParseData(RawPayloadData);
            case Messages.ReplyType.OutputStatusReport:
                return OutputStatus.ParseData(RawPayloadData);
            case Messages.ReplyType.ReaderStatusReport:
                return ReaderStatus.ParseData(RawPayloadData);
            case Messages.ReplyType.RawReaderData:
                return RawCardData.ParseData(RawPayloadData);
            case Messages.ReplyType.FormattedReaderData:
                break;
            case Messages.ReplyType.KeypadData:
                return KeypadData.ParseData(RawPayloadData);
            case Messages.ReplyType.PdCommunicationsConfigurationReport:
                return ReplyData.CommunicationConfiguration.ParseData(RawPayloadData);
            case Messages.ReplyType.BiometricData:
                return BiometricReadResult.ParseData(RawPayloadData);
            case Messages.ReplyType.BiometricMatchResult:
                return BiometricMatchResult.ParseData(RawPayloadData);
            case Messages.ReplyType.CrypticData:
                return _rawPayloadData;
            case Messages.ReplyType.InitialRMac:
                return _rawPayloadData;
            case Messages.ReplyType.Busy:
                break;
            case Messages.ReplyType.FileTransferStatus:
                return FileTransferStatus.ParseData(RawPayloadData);
            case Messages.ReplyType.PIVData:
                return DataFragmentResponse.ParseData(RawPayloadData);
            case Messages.ReplyType.ResponseToChallenge:
                return ChallengeResponse.ParseData(RawPayloadData, _isUsingDefaultKey);
            case Messages.ReplyType.ManufactureSpecific:
                return ReplyData.ManufacturerSpecific.ParseData(RawPayloadData);
            case Messages.ReplyType.ExtendedRead:
                break;
        }

        return null;
    }

    /// <summary>
    /// Raw bytes of the payload data
    /// </summary>
    public ReadOnlySpan<byte> RawPayloadData => _rawPayloadData;

    /// <summary>
    /// Raw bytes of the entire message data
    /// </summary>
    public ReadOnlySpan<byte> RawData => _rawData;
}