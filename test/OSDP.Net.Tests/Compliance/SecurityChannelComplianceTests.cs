using System;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Compliance;

/// <summary>
/// OSDP 2.2.2 Compliance Tests - Secure Channel Validation
///
/// Validates PD secure channel behavior per OSDP 2.2.2 Appendix D:
/// - 4-step handshake: CHLNG → CCRYPT → SCRYPT → RMAC_I (Section D.1)
/// - Key type handling: SCBK vs SCBK-D (Section D.1.3)
/// - Security mode transitions and enforcement
/// - Key update via osdp_KEYSET (Section 7.15)
/// - Secure channel re-establishment after disconnect
///
/// NOTE: Lower-level handshake details (cryptogram generation, MAC validation)
/// are tested implicitly through successful secure channel establishment.
/// Packet-level interception of CHLNG/CCRYPT/SCRYPT/RMAC_I would require
/// a message interceptor which is outside the scope of this test suite.
/// </summary>
[TestFixture]
[Category("Compliance")]
[Category("Compliance.Security")]
public class SecureChannelComplianceTests : IntegrationTestFixtureBase
{
    // OSDP 2.2.2 Appendix D.1 - 4-Step Handshake
    // The secure channel must be established through the full handshake sequence.
    // Successful establishment is verified by the IsSecureChannelEstablished status
    // and the ability to exchange encrypted commands.

    [Test]
    public async Task SecureChannel_EstablishedWithNonDefaultKey()
    {
        // OSDP 2.2.2 Appendix D.1 - Full security with SCBK
        var secureChannelEstablished = new TaskCompletionSource<bool>();

        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        TargetPanel.ConnectionStatusChanged += (_, e) =>
        {
            if (e.IsConnected && e.IsSecureChannelEstablished)
            {
                secureChannelEstablished.TrySetResult(true);
            }
        };

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);

        var result = await Task.WhenAny(secureChannelEstablished.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(secureChannelEstablished.Task),
            "Secure channel must be established after handshake with SCBK");
    }

    [Test]
    public async Task SecureChannel_EstablishedWithDefaultKey_InstallMode()
    {
        // OSDP 2.2.2 Appendix D.1.3 - Installation mode with SCBK-D
        var secureChannelEstablished = new TaskCompletionSource<bool>();

        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.DefaultSCBK;
            cfg.RequireSecurity = true;
        });

        TargetPanel.ConnectionStatusChanged += (_, e) =>
        {
            if (e.IsConnected && e.IsSecureChannelEstablished)
            {
                secureChannelEstablished.TrySetResult(true);
            }
        };

        AddDeviceToPanel(IntegrationConsts.DefaultSCBK);

        var result = await Task.WhenAny(secureChannelEstablished.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(secureChannelEstablished.Task),
            "Secure channel must be established in installation mode with SCBK-D");
    }

    [Test]
    public async Task SecureChannel_CommandsWorkAfterEstablishment()
    {
        // OSDP 2.2.2 - Verify encrypted commands work after handshake
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();

        // All mandatory commands should work over the established secure channel
        var id = await TargetPanel.IdReport(ConnectionId, DeviceAddress);
        Assert.That(id, Is.Not.Null);

        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);
        Assert.That(caps, Is.Not.Null);

        var localStatus = await TargetPanel.LocalStatus(ConnectionId, DeviceAddress);
        Assert.That(localStatus, Is.Not.Null);

        var inputStatus = await TargetPanel.InputStatus(ConnectionId, DeviceAddress);
        Assert.That(inputStatus, Is.Not.Null);

        var outputStatus = await TargetPanel.OutputStatus(ConnectionId, DeviceAddress);
        Assert.That(outputStatus, Is.Not.Null);

        var readerStatus = await TargetPanel.ReaderStatus(ConnectionId, DeviceAddress);
        Assert.That(readerStatus, Is.Not.Null);
    }

    // OSDP 2.2.2 Appendix D.1.3 - Key Type Mismatch
    // Handshake must fail when ACU and PD use different key types.

    [Test]
    public async Task SecureChannel_FailsWhenKeysMismatch()
    {
        // ACU uses non-default key, PD uses default key → handshake fails
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.DefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await AssertPanelRemainsDisconnected();
    }

    [Test]
    public async Task SecureChannel_FailsWhenKeysMismatchReverse()
    {
        // ACU uses default key, PD uses non-default key → handshake fails
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.DefaultSCBK);
        await AssertPanelRemainsDisconnected();
    }

    // OSDP 2.2.2 Section 7.15 - osdp_KEYSET
    // PD must accept key updates and use the new key for subsequent connections.

    [Test]
    public async Task KeySet_UpdatesKeyAndReconnectsSuccessfully()
    {
        // OSDP 2.2.2 Section 7.15 - Key update via osdp_KEYSET
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();

        // Update the key
        var newKey = new byte[] { 0xF0, 0xE1, 0xD2, 0xC3, 0xB4, 0xA5, 0x96, 0x87, 0x78, 0x69, 0x5A, 0x4B, 0x3C, 0x2D, 0x1E, 0x0F };
        var result = await TargetPanel.EncryptionKeySet(ConnectionId, DeviceAddress,
            new EncryptionKeyConfiguration(KeyType.SecureChannelBaseKey, newKey));
        Assert.That(result, Is.True, "Key update must succeed");

        // Verify commands still work on current session
        await AssertPanelToDeviceCommsAreHealthy();

        // Reconnect with the new key
        RemoveDeviceFromPanel();
        AddDeviceToPanel(newKey);
        await WaitForDeviceOnlineStatus();

        // Verify commands work with the new key
        await AssertPanelToDeviceCommsAreHealthy();
    }

    [Test]
    public async Task KeySet_OldKeyNoLongerWorksAfterUpdate()
    {
        // OSDP 2.2.2 - After key update, old key must not work
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();

        // Update the key
        var newKey = new byte[] { 0xF0, 0xE1, 0xD2, 0xC3, 0xB4, 0xA5, 0x96, 0x87, 0x78, 0x69, 0x5A, 0x4B, 0x3C, 0x2D, 0x1E, 0x0F };
        var result = await TargetPanel.EncryptionKeySet(ConnectionId, DeviceAddress,
            new EncryptionKeyConfiguration(KeyType.SecureChannelBaseKey, newKey));
        Assert.That(result, Is.True);

        // Try to reconnect with the old key - should fail
        RemoveDeviceFromPanel();
        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await AssertPanelRemainsDisconnected();
    }

    // OSDP 2.2.2 - Security Mode Transitions
    // PD can transition from installation mode to full security via key update.

    [Test]
    public async Task SecurityModeTransition_InstallModeToFullSecurity()
    {
        // Start in installation mode (default key)
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.DefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.DefaultSCBK);
        await WaitForDeviceOnlineStatus();

        // Update key from default (SCBK-D) to non-default (SCBK)
        var result = await TargetPanel.EncryptionKeySet(ConnectionId, DeviceAddress,
            new EncryptionKeyConfiguration(KeyType.SecureChannelBaseKey, IntegrationConsts.NonDefaultSCBK));
        Assert.That(result, Is.True, "Key update from SCBK-D to SCBK must succeed");

        // Reconnect with the new non-default key
        RemoveDeviceFromPanel();
        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();

        await AssertPanelToDeviceCommsAreHealthy();
    }

    // OSDP 2.2.2 - Secure Channel Re-establishment
    // PD must be able to re-establish secure channel after a disconnection.

    [Test]
    public async Task SecureChannel_ReEstablishedAfterDisconnect()
    {
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        // First connection
        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();
        await AssertPanelToDeviceCommsAreHealthy();

        // Disconnect
        RemoveDeviceFromPanel();
        await Task.Delay(TimeSpan.FromSeconds(9));
        Assert.That(TargetDevice.IsConnected, Is.False);

        // Re-establish secure channel
        var secureChannelEstablished = new TaskCompletionSource<bool>();
        TargetPanel.ConnectionStatusChanged += (_, e) =>
        {
            if (e.IsConnected && e.IsSecureChannelEstablished)
            {
                secureChannelEstablished.TrySetResult(true);
            }
        };

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);

        var waitResult = await Task.WhenAny(secureChannelEstablished.Task, Task.Delay(5000));
        Assert.That(waitResult, Is.EqualTo(secureChannelEstablished.Task),
            "Secure channel must be re-established after disconnect");

        await AssertPanelToDeviceCommsAreHealthy();
    }

    [Test]
    public async Task SecureChannel_MultipleHandshakesSucceed()
    {
        // OSDP 2.2.2 - PD must support repeated handshake sequences
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        for (int i = 0; i < 3; i++)
        {
            AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
            await WaitForDeviceOnlineStatus();
            await AssertPanelToDeviceCommsAreHealthy();
            RemoveDeviceFromPanel();

            // Wait for PD to go offline before next iteration
            await Task.Delay(TimeSpan.FromSeconds(9));
            Assert.That(TargetDevice.IsConnected, Is.False, $"Iteration {i}: PD should be offline");
        }
    }

    // OSDP 2.2.2 - Unsecured Connection with Security Key Configured
    // When PD does not require security, it can still accept unsecured connections
    // even if a security key is configured.

    [Test]
    public async Task UnsecuredConnection_WorksWhenSecurityNotRequired()
    {
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = false;
        });

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        await AssertPanelToDeviceCommsAreHealthy();
    }

    [Test]
    public async Task SecuredConnection_WorksWhenSecurityNotRequired()
    {
        // OSDP 2.2.2 - PD can accept secure channel even when not required
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = false;
        });

        var secureChannelEstablished = new TaskCompletionSource<bool>();
        TargetPanel.ConnectionStatusChanged += (_, e) =>
        {
            if (e.IsConnected && e.IsSecureChannelEstablished)
            {
                secureChannelEstablished.TrySetResult(true);
            }
        };

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);

        var result = await Task.WhenAny(secureChannelEstablished.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(secureChannelEstablished.Task),
            "Secure channel should be established even when security is not required");

        await AssertPanelToDeviceCommsAreHealthy();
    }
}
