using System.Collections.Generic;

namespace OSDP.Net.Messages;

/// <summary>
/// Extension methods for CommandType and ReplyType enums.
/// </summary>
public static class CommandReplyExtensions
{
    private static readonly Dictionary<CommandType, string> CommandDisplayNames = new()
    {
        { CommandType.Poll, "osdp_POLL" },
        { CommandType.IdReport, "osdp_ID" },
        { CommandType.DeviceCapabilities, "osdp_CAP" },
        { CommandType.LocalStatus, "osdp_LSTAT" },
        { CommandType.InputStatus, "osdp_ISTAT" },
        { CommandType.OutputStatus, "osdp_OSTAT" },
        { CommandType.ReaderStatus, "osdp_RSTAT" },
        { CommandType.OutputControl, "osdp_OUT" },
        { CommandType.LEDControl, "osdp_LED" },
        { CommandType.BuzzerControl, "osdp_BUZ" },
        { CommandType.TextOutput, "osdp_TEXT" },
        { CommandType.CommunicationSet, "osdp_COMSET" },
        { CommandType.BioRead, "osdp_BIOREAD" },
        { CommandType.BioMatch, "osdp_BIOMATCH" },
        { CommandType.KeySet, "osdp_KEYSET" },
        { CommandType.SessionChallenge, "osdp_CHLNG" },
        { CommandType.ServerCryptogram, "osdp_SCRYPT" },
        { CommandType.MaxReplySize, "osdp_ACURXSIZE" },
        { CommandType.FileTransfer, "osdp_FILETRANSFER" },
        { CommandType.ManufacturerSpecific, "osdp_MFG" },
        { CommandType.ExtendedWrite, "osdp_XWR" },
        { CommandType.Abort, "osdp_ABORT" },
        { CommandType.PivData, "osdp_PIVDATA" },
        { CommandType.GenerateChallenge, "osdp_GENAUTH" },
        { CommandType.AuthenticateChallenge, "osdp_CRAUTH" },
        { CommandType.KeepActive, "osdp_KEEPACTIVE" }
    };

    private static readonly Dictionary<ReplyType, string> ReplyDisplayNames = new()
    {
        { ReplyType.Ack, "osdp_ACK" },
        { ReplyType.Nak, "osdp_NAK" },
        { ReplyType.PdIdReport, "osdp_PDID" },
        { ReplyType.PdCapabilitiesReport, "osdp_PDCAP" },
        { ReplyType.LocalStatusReport, "osdp_LSTATR" },
        { ReplyType.InputStatusReport, "osdp_ISTATR" },
        { ReplyType.OutputStatusReport, "osdp_OSTATR" },
        { ReplyType.ReaderStatusReport, "osdp_RSTATR" },
        { ReplyType.RawReaderData, "osdp_RAW" },
        { ReplyType.FormattedReaderData, "osdp_FMT" },
        { ReplyType.KeypadData, "osdp_KEYPAD" },
        { ReplyType.PdCommunicationsConfigurationReport, "osdp_COM" },
        { ReplyType.BiometricData, "osdp_BIOREADR" },
        { ReplyType.BiometricMatchResult, "osdp_BIOMATCHR" },
        { ReplyType.ExtendedPdIdReport, "osdp_EXT_PDID" },
        { ReplyType.CrypticData, "osdp_CCRYPT" },
        { ReplyType.InitialRMac, "osdp_RMAC_I" },
        { ReplyType.Busy, "osdp_BUSY" },
        { ReplyType.FileTransferStatus, "osdp_FTSTAT" },
        { ReplyType.PIVData, "osdp_PIVDATAR" },
        { ReplyType.ResponseToChallenge, "osdp_CRAUTHR" },
        { ReplyType.ManufactureSpecific, "osdp_MFGSTATR" },
        { ReplyType.ExtendedRead, "osdp_XRD" }
    };

    /// <summary>
    /// Gets the OSDP protocol name for the command type.
    /// </summary>
    /// <param name="commandType">The command type.</param>
    /// <returns>The OSDP protocol name (e.g., "osdp_LED").</returns>
    /// <example>
    /// CommandType.LEDControl.GetDisplayName() returns "osdp_LED"
    /// </example>
    public static string GetDisplayName(this CommandType commandType)
    {
        return CommandDisplayNames.TryGetValue(commandType, out var name)
            ? name
            : commandType.ToString();
    }

    /// <summary>
    /// Gets the OSDP protocol name for the reply type.
    /// </summary>
    /// <param name="replyType">The reply type.</param>
    /// <returns>The OSDP protocol name (e.g., "osdp_ACK").</returns>
    /// <example>
    /// ReplyType.PdIdReport.GetDisplayName() returns "osdp_PDID"
    /// </example>
    public static string GetDisplayName(this ReplyType replyType)
    {
        return ReplyDisplayNames.TryGetValue(replyType, out var name)
            ? name
            : replyType.ToString();
    }
}
