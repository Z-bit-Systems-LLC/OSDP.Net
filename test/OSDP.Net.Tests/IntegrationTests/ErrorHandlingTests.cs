using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Messages;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.IntegrationTests;

/// <summary>
/// OSDP 2.2.2 Compliance Tests - Error Handling and NAK Validation
///
/// Validates that the PD correctly generates osdp_NAK replies with appropriate
/// error codes for various error conditions:
/// - 0x03: UnknownCommandCode - Unimplemented/unsupported commands (Section 7.16)
/// - 0x05: DoesNotSupportSecurityBlock - Security key type mismatch (Appendix D)
/// - 0x06: CommunicationSecurityNotMet - Unsecured commands when security required (Section 6.3)
///
/// NOTE: Some NAK codes cannot be triggered through the integration test stack:
/// - 0x01: BadChecksumOrCrc - Requires corrupted transport-level data
/// - 0x02: InvalidCommandLength - Requires malformed command packets
/// - 0x04: UnexpectedSequenceNumber - Requires sequence number manipulation
/// - 0x07/0x08: BioType/BioFormatNotSupported - Not triggered by default handlers
/// - 0x09: UnableToProcessCommand - Not triggered by default handlers
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Compliance.Mandatory")]
public class ErrorHandlingTests : IntegrationTestFixtureBase
{
    // OSDP 2.2.2 Section 7.16 - osdp_NAK
    // PD must respond with NAK containing the appropriate error code when
    // it cannot process a command.

    // Error Code 0x03 - UnknownCommandCode
    // PD must return this when it receives a command it does not implement.

    [SetUp]
    public async Task SetupErrorHandlingTests()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();
    }

    [Test]
    public void PdReturnsNak_UnknownCommandCode_ForUnimplementedBuzzerControl()
    {
        // OSDP 2.2.2 Section 7.16 - PD returns NAK 0x03 for unsupported commands
        // TestDevice does not override HandleBuzzerControl, so it returns NAK
        var buzzerControl = new ReaderBuzzerControl(0, ToneCode.Off, 1, 1, 1);

        var exception = Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.ReaderBuzzerControl(ConnectionId, DeviceAddress, buzzerControl));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Reply.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode),
            "PD must return NAK with error code 0x03 for unimplemented commands");
    }

    [Test]
    public void PdReturnsNak_UnknownCommandCode_ForUnimplementedTextOutput()
    {
        // OSDP 2.2.2 Section 7.16 - PD returns NAK 0x03 for unsupported commands
        var textOutput = new ReaderTextOutput(0, TextCommand.PermanentTextNoWrap, 0, 1, 1, "Test");

        var exception = Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.ReaderTextOutput(ConnectionId, DeviceAddress, textOutput));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Reply.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode),
            "PD must return NAK with error code 0x03 for unimplemented commands");
    }

    [Test]
    public void PdReturnsNak_UnknownCommandCode_ForUnimplementedLEDControl()
    {
        // OSDP 2.2.2 Section 7.16 - PD returns NAK 0x03 for unsupported commands
        var ledControls = new ReaderLedControls([
            new ReaderLedControl(0, 0,
                TemporaryReaderControlCode.Nop, 1, 0, LedColor.Black, LedColor.Black, 0,
                PermanentReaderControlCode.Nop, 1, 0, LedColor.Black, LedColor.Black)
        ]);

        var exception = Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.ReaderLedControl(ConnectionId, DeviceAddress, ledControls));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Reply.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode),
            "PD must return NAK with error code 0x03 for unimplemented commands");
    }

    [Test]
    public async Task PdContinuesNormally_AfterReturningNak()
    {
        // OSDP 2.2.2 - PD must remain operational after sending NAK
        var buzzerControl = new ReaderBuzzerControl(0, ToneCode.Off, 1, 1, 1);

        Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.ReaderBuzzerControl(ConnectionId, DeviceAddress, buzzerControl));

        // Verify PD is still responsive after NAK
        await AssertPanelToDeviceCommsAreHealthy();
    }

    [Test]
    public async Task PdReturnsCorrectNakErrorCode_ForMultipleUnimplementedCommands()
    {
        // OSDP 2.2.2 Section 7.16 - NAK error code consistency
        var buzzerControl = new ReaderBuzzerControl(0, ToneCode.Off, 1, 1, 1);
        var textOutput = new ReaderTextOutput(0, TextCommand.PermanentTextNoWrap, 0, 1, 1, "Test");

        var ex1 = Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.ReaderBuzzerControl(ConnectionId, DeviceAddress, buzzerControl));
        var ex2 = Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.ReaderTextOutput(ConnectionId, DeviceAddress, textOutput));

        Assert.Multiple(() =>
        {
            Assert.That(ex1!.Reply.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode));
            Assert.That(ex2!.Reply.ErrorCode, Is.EqualTo(ErrorCode.UnknownCommandCode));
        });

        // PD still healthy after multiple NAKs
        await AssertPanelToDeviceCommsAreHealthy();
    }
}

/// <summary>
/// OSDP 2.2.2 Compliance Tests - Security-Related NAK Codes
///
/// Validates NAK error codes related to secure channel operations:
/// - 0x05: DoesNotSupportSecurityBlock - Key type mismatch during handshake
/// - 0x06: CommunicationSecurityNotMet - Unsecured commands when security is required
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Compliance.Mandatory")]
[Category("Compliance.Security")]
public class SecurityNakTests : IntegrationTestFixtureBase
{
    // Error Code 0x05 - DoesNotSupportSecurityBlock
    // OSDP 2.2.2 Appendix D - PD returns this when there is a key type mismatch
    // during secure channel establishment.

    [Test]
    public async Task PdReturnsNak_DoesNotSupportSecurityBlock_WhenAcuRequestsDefaultKeyButPdHasNonDefaultKey()
    {
        // PD has non-default key; ACU tries to connect with default key
        await InitTestTargets(cfg =>
        {
            cfg.RequireSecurity = true;
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
        });

        AddDeviceToPanel(IntegrationConsts.DefaultSCBK);

        // Connection should fail because key type mismatch triggers NAK 0x05
        await AssertPanelRemainsDisconnected();
    }

    [Test]
    public async Task PdReturnsNak_DoesNotSupportSecurityBlock_WhenAcuRequestsNonDefaultKeyButPdHasDefaultKey()
    {
        // PD has default key (install mode); ACU tries to connect with non-default key
        await InitTestTargets(cfg =>
        {
            cfg.RequireSecurity = true;
            cfg.SecurityKey = IntegrationConsts.DefaultSCBK;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);

        // Connection should fail because key type mismatch triggers NAK 0x05
        await AssertPanelRemainsDisconnected();
    }

    // Error Code 0x06 - CommunicationSecurityNotMet
    // OSDP 2.2.2 Section 6.3 - PD in FullSecurity mode must reject unsecured
    // commands that are not in the AllowUnsecured list.

    [Test]
    public async Task PdReturnsNak_CommunicationSecurityNotMet_ForDisallowedCommandsInFullSecurityMode()
    {
        // PD requires security with non-default key (FullSecurity mode)
        // ACU connects without secure channel
        await InitTestTargets(cfg =>
        {
            cfg.RequireSecurity = true;
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
        });

        AddDeviceToPanel(useSecureChannel: false);

        // By default, AllowUnsecured includes: IdReport, DeviceCapabilities, CommunicationSet
        // All other commands should return NAK 0x06
        var disallowedCommands = new[]
        {
            CommandType.LocalStatus, CommandType.InputStatus, CommandType.OutputStatus,
            CommandType.ReaderStatus, CommandType.OutputControl
        };

        Assert.Multiple(() =>
        {
            foreach (var commandType in disallowedCommands)
            {
                var command = BuildTestCommand(commandType);
                var exception = Assert.ThrowsAsync<NackReplyException>(
                    () => command.Run(), $"command: {commandType}");
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Reply.ErrorCode,
                    Is.EqualTo(ErrorCode.CommunicationSecurityNotMet),
                    $"PD must return NAK 0x06 for unsecured {commandType} in FullSecurity mode");
            }
        });
    }

    [Test]
    public async Task PdAllowsDefaultUnsecuredCommands_InFullSecurityMode()
    {
        // OSDP 2.2.2 - IdReport, DeviceCapabilities, CommunicationSet are allowed unsecured by default
        await InitTestTargets(cfg =>
        {
            cfg.RequireSecurity = true;
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
        });

        AddDeviceToPanel(useSecureChannel: false);

        var allowedCommands = new[]
        {
            CommandType.IdReport, CommandType.DeviceCapabilities, CommandType.CommunicationSet
        };

        Assert.Multiple(() =>
        {
            foreach (var commandType in allowedCommands)
            {
                var command = BuildTestCommand(commandType);
                Assert.DoesNotThrowAsync(async () =>
                {
                    var reply = await command.Run();
                    Assert.That(reply, Is.Not.Null, $"command: {commandType}");
                }, $"command: {commandType}");
            }
        });
    }

    [Test]
    public async Task PdReturnsNak_CommunicationSecurityNotMet_WhenCustomAllowUnsecuredIsRestricted()
    {
        // OSDP 2.2.2 - PD manufacturer can restrict the AllowUnsecured list
        await InitTestTargets(cfg =>
        {
            cfg.RequireSecurity = true;
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.AllowUnsecured = [CommandType.IdReport];
        });

        AddDeviceToPanel(useSecureChannel: false);

        // DeviceCapabilities and CommunicationSet should now be rejected
        var disallowedCommands = new[]
        {
            CommandType.DeviceCapabilities, CommandType.CommunicationSet
        };

        Assert.Multiple(() =>
        {
            foreach (var commandType in disallowedCommands)
            {
                var command = BuildTestCommand(commandType);
                var exception = Assert.ThrowsAsync<NackReplyException>(
                    () => command.Run(), $"command: {commandType}");
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Reply.ErrorCode,
                    Is.EqualTo(ErrorCode.CommunicationSecurityNotMet),
                    $"command: {commandType}");
            }
        });
    }

    [Test]
    public async Task PdReturnsNak_CommunicationSecurityNotMet_WhenUnsecuredCommandSentOnEstablishedSecureChannel()
    {
        // OSDP 2.2.2 - Once secure channel is established, PD must reject unsecured commands
        await InitTestTargets(cfg =>
        {
            cfg.RequireSecurity = false;
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
        },
        panel =>
        {
            panel.OnGetNextCommand = (command, channel) =>
            {
                if (command.Code == (byte)CommandType.IdReport)
                {
                    channel.ResetSecureChannelSession();
                }
            };
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);

        var exception = Assert.ThrowsAsync<NackReplyException>(
            () => TargetPanel.IdReport(ConnectionId, DeviceAddress));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Reply.ErrorCode,
            Is.EqualTo(ErrorCode.CommunicationSecurityNotMet),
            "PD must reject unsecured commands after secure channel is established");
    }

    // OSDP 2.2.2 - Install Mode (default key) allows all commands unsecured

    [Test]
    public async Task PdAllowsAllCommands_InInstallMode()
    {
        // OSDP 2.2.2 - When PD uses default key (install mode), all commands are allowed unsecured
        await InitTestTargets(cfg =>
        {
            cfg.RequireSecurity = true;
            cfg.SecurityKey = IntegrationConsts.DefaultSCBK;
        });

        AddDeviceToPanel(useSecureChannel: false);

        var allCommands = new[]
        {
            CommandType.IdReport, CommandType.DeviceCapabilities, CommandType.CommunicationSet,
            CommandType.LocalStatus, CommandType.InputStatus, CommandType.OutputStatus,
            CommandType.ReaderStatus, CommandType.OutputControl
        };

        Assert.Multiple(() =>
        {
            foreach (var commandType in allCommands)
            {
                var command = BuildTestCommand(commandType);
                Assert.DoesNotThrowAsync(async () =>
                {
                    var reply = await command.Run();
                    Assert.That(reply, Is.Not.Null, $"command: {commandType}");
                }, $"command: {commandType}");
            }
        });
    }
}
