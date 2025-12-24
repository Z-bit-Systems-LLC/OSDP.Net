using NUnit.Framework;
using OSDP.Net.Messages;

namespace OSDP.Net.Tests.Messages;

[TestFixture]
public class CommandReplyExtensionsTests
{
    [TestCase(CommandType.Poll, "osdp_POLL")]
    [TestCase(CommandType.IdReport, "osdp_ID")]
    [TestCase(CommandType.DeviceCapabilities, "osdp_CAP")]
    [TestCase(CommandType.LocalStatus, "osdp_LSTAT")]
    [TestCase(CommandType.InputStatus, "osdp_ISTAT")]
    [TestCase(CommandType.OutputStatus, "osdp_OSTAT")]
    [TestCase(CommandType.ReaderStatus, "osdp_RSTAT")]
    [TestCase(CommandType.OutputControl, "osdp_OUT")]
    [TestCase(CommandType.LEDControl, "osdp_LED")]
    [TestCase(CommandType.BuzzerControl, "osdp_BUZ")]
    [TestCase(CommandType.TextOutput, "osdp_TEXT")]
    [TestCase(CommandType.CommunicationSet, "osdp_COMSET")]
    [TestCase(CommandType.BioRead, "osdp_BIOREAD")]
    [TestCase(CommandType.BioMatch, "osdp_BIOMATCH")]
    [TestCase(CommandType.KeySet, "osdp_KEYSET")]
    [TestCase(CommandType.SessionChallenge, "osdp_CHLNG")]
    [TestCase(CommandType.ServerCryptogram, "osdp_SCRYPT")]
    [TestCase(CommandType.MaxReplySize, "osdp_ACURXSIZE")]
    [TestCase(CommandType.FileTransfer, "osdp_FILETRANSFER")]
    [TestCase(CommandType.ManufacturerSpecific, "osdp_MFG")]
    [TestCase(CommandType.ExtendedWrite, "osdp_XWR")]
    [TestCase(CommandType.Abort, "osdp_ABORT")]
    [TestCase(CommandType.PivData, "osdp_PIVDATA")]
    [TestCase(CommandType.GenerateChallenge, "osdp_GENAUTH")]
    [TestCase(CommandType.AuthenticateChallenge, "osdp_CRAUTH")]
    [TestCase(CommandType.KeepActive, "osdp_KEEPACTIVE")]
    public void GetDisplayName_CommandType_ReturnsProtocolName(CommandType commandType, string expected)
    {
        // Act
        var result = commandType.GetDisplayName();

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(ReplyType.Ack, "osdp_ACK")]
    [TestCase(ReplyType.Nak, "osdp_NAK")]
    [TestCase(ReplyType.PdIdReport, "osdp_PDID")]
    [TestCase(ReplyType.PdCapabilitiesReport, "osdp_PDCAP")]
    [TestCase(ReplyType.LocalStatusReport, "osdp_LSTATR")]
    [TestCase(ReplyType.InputStatusReport, "osdp_ISTATR")]
    [TestCase(ReplyType.OutputStatusReport, "osdp_OSTATR")]
    [TestCase(ReplyType.ReaderStatusReport, "osdp_RSTATR")]
    [TestCase(ReplyType.RawReaderData, "osdp_RAW")]
    [TestCase(ReplyType.FormattedReaderData, "osdp_FMT")]
    [TestCase(ReplyType.KeypadData, "osdp_KEYPAD")]
    [TestCase(ReplyType.PdCommunicationsConfigurationReport, "osdp_COM")]
    [TestCase(ReplyType.BiometricData, "osdp_BIOREADR")]
    [TestCase(ReplyType.BiometricMatchResult, "osdp_BIOMATCHR")]
    [TestCase(ReplyType.ExtendedPdIdReport, "osdp_EXT_PDID")]
    [TestCase(ReplyType.CrypticData, "osdp_CCRYPT")]
    [TestCase(ReplyType.InitialRMac, "osdp_RMAC_I")]
    [TestCase(ReplyType.Busy, "osdp_BUSY")]
    [TestCase(ReplyType.FileTransferStatus, "osdp_FTSTAT")]
    [TestCase(ReplyType.PIVData, "osdp_PIVDATAR")]
    [TestCase(ReplyType.ResponseToChallenge, "osdp_CRAUTHR")]
    [TestCase(ReplyType.ManufactureSpecific, "osdp_MFGSTATR")]
    [TestCase(ReplyType.ExtendedRead, "osdp_XRD")]
    public void GetDisplayName_ReplyType_ReturnsProtocolName(ReplyType replyType, string expected)
    {
        // Act
        var result = replyType.GetDisplayName();

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void GetDisplayName_UndefinedCommandType_ReturnsEnumToString()
    {
        // Arrange
        var undefinedCommand = (CommandType)0xFF;

        // Act
        var result = undefinedCommand.GetDisplayName();

        // Assert
        Assert.That(result, Is.EqualTo("255"));
    }

    [Test]
    public void GetDisplayName_UndefinedReplyType_ReturnsEnumToString()
    {
        // Arrange
        var undefinedReply = (ReplyType)0xFF;

        // Act
        var result = undefinedReply.GetDisplayName();

        // Assert
        Assert.That(result, Is.EqualTo("255"));
    }
}
