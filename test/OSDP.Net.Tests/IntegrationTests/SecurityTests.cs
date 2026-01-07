using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Messages;

namespace OSDP.Net.Tests.IntegrationTests
{
    [Category("Integration")]
    public class SecurityTests : IntegrationTestFixtureBase
    {
        [Test]
        public async Task GivenRequireSecurityWithSCBK_WhenSecChanNotEstablished_MostCommandsAreNotAllowed()
        {
            await InitTestTargets(cfg =>
            {
                cfg.RequireSecurity = true;
                cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            });

            // The panel WILL NOT establish secure channel in this test before sending the set of commands
            AddDeviceToPanel(useSecureChannel: false);

            // By default, the OSDP protocol allows these commands to be sent to the PD even when secure channel
            // is not established in the secure mode
            var allowedCommands = new[] { 
                CommandType.IdReport, CommandType.DeviceCapabilities, CommandType.CommunicationSet };
            
            // All other commands should not be allowed
            var disallowedCommands = new[] {
                CommandType.LocalStatus, CommandType.InputStatus, CommandType.OutputStatus,
                CommandType.ReaderStatus, CommandType.OutputControl
            };

            Assert.Multiple(() =>
            {
                foreach (var commandType in allowedCommands)
                {
                    var command = BuildTestCommand(commandType);
                    Assert.DoesNotThrowAsync(async () => {
                        var reply = await command.Run();
                        Assert.That(reply, Is.Not.Null, $"command: {commandType}");
                    }, $"command: {commandType}");
                }

                foreach (var commandType in disallowedCommands)
                {
                    var command = BuildTestCommand(commandType);
                    var exception = Assert.ThrowsAsync<NackReplyException>(() => command.Run(), $"command: {commandType}");
                    Assert.That(exception, Is.Not.Null);
                    Assert.That(
                        exception.Reply.ErrorCode,
                        Is.EqualTo(Net.Model.ReplyData.ErrorCode.CommunicationSecurityNotMet),
                        $"command: {commandType}");
                }
            });
        }

        [Test]
        public async Task GivenRequireSecurityWithSCBK_WhenSecChanEstablished_UnsecureCommandsNotAllowed()
        {
            await InitTestTargets(device =>
            {
                device.RequireSecurity = true;
                device.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            },
            panel =>
            {
                // Add a test-specific hook here so that right before IdReport command is sent, we will
                // reset the secure channel thus forcing the report command to be sent unsecure to the PD
                panel.OnGetNextCommand = (command, channel) =>
                {
                    if (command.Code == (byte)CommandType.IdReport)
                    {
                        channel.ResetSecureChannelSession();
                    }
                };
            });

            AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);

            var exception = Assert.ThrowsAsync<NackReplyException>(() => TargetPanel.IdReport(ConnectionId, DeviceAddress));
            Assert.That(exception, Is.Not.Null);
            Assert.That(
                exception.Reply.ErrorCode,
                Is.EqualTo(Net.Model.ReplyData.ErrorCode.CommunicationSecurityNotMet));
        }

        [Test]
        public async Task GivenRequireSecurityWithSCBK_WhenSecChanNotEstablished_PdMayChooseToLimitAllowedUnsecureCommands()
        {
            await InitTestTargets(cfg =>
            {
                cfg.RequireSecurity = true;
                cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;

                // PD instantiation overrides defaults to only allow IdReport command to be sent unsecured
                cfg.AllowUnsecured = [CommandType.IdReport];
            });

            // The panel WILL NOT establish secure channel in this test before sending the set of commands
            AddDeviceToPanel(useSecureChannel: false);

            var allowedCommands = new[] { CommandType.IdReport };

            // These commands would have been allowed by default but because we updated device configuration
            // to not allow them, let's verify that they will now return a NAK
            var disallowedCommands = new[] {
                CommandType.DeviceCapabilities, CommandType.CommunicationSet
            };

            Assert.Multiple(() =>
            {
                foreach (var commandType in allowedCommands)
                {
                    var command = BuildTestCommand(commandType);
                    Assert.DoesNotThrowAsync(async () => {
                        var reply = await command.Run();
                        Assert.That(reply, Is.Not.Null, $"command: {commandType}");
                    }, $"command: {commandType}");
                }

                foreach (var commandType in disallowedCommands)
                {
                    var command = BuildTestCommand(commandType);
                    var exception = Assert.ThrowsAsync<NackReplyException>(() => command.Run(), $"command: {commandType}");
                    Assert.That(exception, Is.Not.Null);
                    Assert.That(
                        exception.Reply.ErrorCode,
                        Is.EqualTo(Net.Model.ReplyData.ErrorCode.CommunicationSecurityNotMet),
                        $"command: {commandType}");
                }
            });
        }

        [Test]
        public async Task GivenRequiredSecurityWithDefaultSCBK_WhenSecChanNotEstablished_AllCommandsAreAllowed()
        {
            await InitTestTargets(cfg =>
            {
                cfg.RequireSecurity = true;
                cfg.SecurityKey = IntegrationConsts.DefaultSCBK;
            });

            // The panel WILL NOT establish secure channel in this test before sending the set of commands
            AddDeviceToPanel(useSecureChannel: false);

            // By default, the OSDP protocol allows these commands to be sent to the PD even when secure channel
            // is not established in the secure mode
            var allowedCommands = new[] {
                CommandType.IdReport, CommandType.DeviceCapabilities, CommandType.CommunicationSet,
                CommandType.LocalStatus, CommandType.InputStatus, CommandType.OutputStatus,
                CommandType.ReaderStatus, CommandType.OutputControl
            };

            Assert.Multiple(() =>
            {
                foreach (var commandType in allowedCommands)
                {
                    var command = BuildTestCommand(commandType);
                    Assert.DoesNotThrowAsync(async () => {
                        var reply = await command.Run();
                        Assert.That(reply, Is.Not.Null, $"command: {commandType}");
                    }, $"command: {commandType}");
                }
            });
        }

        [Test]
        public async Task GivenNoRequireSecurity_WhenSecChanNotEstablished_AllCommandsAreAllowed()
        {
            await InitTestTargets(cfg =>
            {
                cfg.RequireSecurity = false;

                // We have a non-default security key BUT we also told PD that security isn't 
                // required
                cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            });

            // The panel WILL NOT establish secure channel in this test before sending the set of commands
            AddDeviceToPanel(useSecureChannel: false);

            // By default, the OSDP protocol allows these commands to be sent to the PD even when secure channel
            // is not established in the secure mode
            var allowedCommands = new[] {
                CommandType.IdReport, CommandType.DeviceCapabilities, CommandType.CommunicationSet,
                CommandType.LocalStatus, CommandType.InputStatus, CommandType.OutputStatus,
                CommandType.ReaderStatus, CommandType.OutputControl
            };

            Assert.Multiple(() =>
            {
                foreach (var commandType in allowedCommands)
                {
                    var command = BuildTestCommand(commandType);
                    Assert.DoesNotThrowAsync(async () => {
                        var reply = await command.Run();
                        Assert.That(reply, Is.Not.Null, $"command: {commandType}");
                    }, $"command: {commandType}");
                }
            });
        }

        [Test]
        public async Task GivenNoRequireSecurity_WhenSecChanEstablished_UnsecureCommandsNotAllowed() 
        {
            await InitTestTargets(cfg =>
            {
                cfg.RequireSecurity = false;

                // We have a non-default security key BUT we also told PD that security isn't 
                // required
                cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            },
            panel =>
            {
                // Add a test-specific hook here so that right before IdReport command is sent, we will
                // reset the secure channel thus forcing the report command to be sent unsecure to the PD
                panel.OnGetNextCommand = (command, channel) =>
                {
                    if (command.Code == (byte)CommandType.IdReport)
                    {
                        channel.ResetSecureChannelSession();
                    }
                };
            });

            AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);

            var exception = Assert.ThrowsAsync<NackReplyException>(() => TargetPanel.IdReport(ConnectionId, DeviceAddress));
            Assert.That(exception, Is.Not.Null);
            Assert.That(
                exception.Reply.ErrorCode,
                Is.EqualTo(Net.Model.ReplyData.ErrorCode.CommunicationSecurityNotMet));
        }
    }
}


