using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Model;
using OSDP.Net.Model.ReplyData;

namespace OSDP.Net.Tests.Compliance;

/// <summary>
/// OSDP 2.2.2 Compliance Tests - Protocol State Machine Validation
///
/// Validates PD behavior related to:
/// - Connection state management and the 8-second offline timeout (Section 6.1)
/// - Poll reply queue behavior (Section 7.14 - osdp_POLL)
/// - Address filtering (Section 6.2)
/// - Connection establishment and recovery (Section 6.1)
/// </summary>
[TestFixture]
[Category("Compliance")]
[Category("Compliance.Mandatory")]
public class ProtocolStateMachineTests : IntegrationTestFixtureBase
{
    // OSDP 2.2.2 Section 6.1 - Connection State Management
    // The PD must track connection state. If no valid command is received within
    // 8 seconds, the PD should consider the connection offline.

    [Test]
    public async Task PdIsConnected_WhenAcuIsCommunicating()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        Assert.That(TargetDevice.IsConnected, Is.True);
    }

    [Test]
    public void PdIsNotConnected_BeforeAnyCommunication()
    {
        // OSDP 2.2.2: PD must not consider itself connected before receiving any command
        var config = new DeviceConfiguration(new ClientIdentification([0x01, 0x02, 0x03], 12345));
        using var device = new Device(config);

        Assert.That(device.IsConnected, Is.False);
    }

    [Test]
    public async Task PdIsNotConnected_AfterOfflineTimeout()
    {
        // OSDP 2.2.2 Section 6.1 - PD offline after 8 seconds without valid command
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        Assert.That(TargetDevice.IsConnected, Is.True);

        // Stop the panel so it stops polling
        await TargetPanel.Shutdown();

        // Wait longer than the 8-second offline timeout
        await Task.Delay(TimeSpan.FromSeconds(9));

        Assert.That(TargetDevice.IsConnected, Is.False);
    }

    // OSDP 2.2.2 Section 7.14 - osdp_POLL
    // PD must respond with ACK when no data to report.
    // PD must respond with queued reply data when available.

    [Test]
    public async Task PollReturnsAck_WhenNoPendingReplies()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        // Verify comms are healthy - this implicitly tests POLL returns ACK
        // since the panel must be polling successfully to stay online
        await AssertPanelToDeviceCommsAreHealthy();
    }

    [Test]
    public async Task PollReturnsQueuedReply_WhenCardDataEnqueued()
    {
        // OSDP 2.2.2 Section 7.14 - PD returns unsolicited data on POLL
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var rawCardReplyReceived = new TaskCompletionSource<RawCardData>();
        TargetPanel.RawCardDataReplyReceived += (_, e) =>
        {
            rawCardReplyReceived.TrySetResult(e.RawCardData);
        };

        // Enqueue a card read reply with 26-bit data
        var bits = new BitArray(26);
        bits[0] = true;
        bits[25] = true;
        var cardData = new RawCardData(0, FormatCode.NotSpecified, bits);
        TargetDevice.EnqueuePollReply(cardData);

        // The next POLL from the ACU should dequeue and return the card data
        var result = await Task.WhenAny(rawCardReplyReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(rawCardReplyReceived.Task), "Timed out waiting for card data reply");

        var receivedCard = await rawCardReplyReceived.Task;
        Assert.That(receivedCard.BitCount, Is.EqualTo(26));
    }

    [Test]
    public async Task PollReturnsQueuedReply_WhenKeypadDataEnqueued()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var keypadReplyReceived = new TaskCompletionSource<KeypadData>();
        TargetPanel.KeypadReplyReceived += (_, e) =>
        {
            keypadReplyReceived.TrySetResult(e.KeypadData);
        };

        // Enqueue a keypad data reply
        var keypadData = new KeypadData(0, "1234");
        TargetDevice.EnqueuePollReply(keypadData);

        var result = await Task.WhenAny(keypadReplyReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(keypadReplyReceived.Task), "Timed out waiting for keypad data reply");

        var receivedKeypad = await keypadReplyReceived.Task;
        Assert.That(receivedKeypad.DigitCount, Is.EqualTo(4));
    }

    [Test]
    public async Task MultipleQueuedReplies_AreDeliveredInOrder()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var receivedCards = new ConcurrentQueue<RawCardData>();
        var allReceived = new TaskCompletionSource<bool>();

        TargetPanel.RawCardDataReplyReceived += (_, e) =>
        {
            receivedCards.Enqueue(e.RawCardData);
            if (receivedCards.Count >= 2)
            {
                allReceived.TrySetResult(true);
            }
        };

        // Enqueue two card reads with different bit counts
        TargetDevice.EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, new BitArray(8, true)));
        TargetDevice.EnqueuePollReply(new RawCardData(0, FormatCode.NotSpecified, new BitArray(16, true)));

        var result = await Task.WhenAny(allReceived.Task, Task.Delay(5000));
        Assert.That(result, Is.EqualTo(allReceived.Task), "Timed out waiting for queued replies");

        Assert.That(receivedCards.Count, Is.EqualTo(2));
        receivedCards.TryDequeue(out var first);
        receivedCards.TryDequeue(out var second);
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        Assert.That(first!.BitCount, Is.EqualTo(8));
        Assert.That(second!.BitCount, Is.EqualTo(16));
    }

    // OSDP 2.2.2 Section 6.2 - Address Filtering
    // PD must only respond to commands addressed to its configured address

    [Test]
    public async Task PdIgnoresCommands_WhenAddressedToDifferentDevice()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        // Add device to panel with wrong address - PD is at address 0, panel sends to 5
        AddDeviceToPanel(address: 5, useSecureChannel: false);

        await AssertPanelRemainsDisconnected();
    }

    // OSDP 2.2.2 Section 6.1 - Connection Establishment
    // Verify PD establishes connection with ACU using both secure and unsecure modes

    [Test]
    public async Task PdEstablishesConnection_WithoutSecureChannel()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        Assert.That(TargetDevice.IsConnected, Is.True);
        await AssertPanelToDeviceCommsAreHealthy();
    }

    [Test]
    public async Task PdEstablishesConnection_WithSecureChannel()
    {
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();

        // Send a real command to update _lastValidReceivedCommand on PD
        await AssertPanelToDeviceCommsAreHealthy();

        Assert.That(TargetDevice.IsConnected, Is.True);
    }

    [Test]
    public async Task PdEstablishesConnection_WithDefaultKey_InstallMode()
    {
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.DefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.DefaultSCBK);
        await WaitForDeviceOnlineStatus();

        // Send a real command to update _lastValidReceivedCommand on PD
        await AssertPanelToDeviceCommsAreHealthy();

        Assert.That(TargetDevice.IsConnected, Is.True);
    }

    // OSDP 2.2.2 Section 6.1 - Connection Recovery
    // After the connection goes offline, the PD should be able to re-establish

    [Test]
    public async Task PdRecoversConnection_AfterPanelReconnects()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);

        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        Assert.That(TargetDevice.IsConnected, Is.True);

        // Remove device from panel (simulates ACU disconnect)
        RemoveDeviceFromPanel();

        // Wait for the PD to go offline
        await Task.Delay(TimeSpan.FromSeconds(9));
        Assert.That(TargetDevice.IsConnected, Is.False);

        // Re-add device to panel (simulates ACU reconnect)
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        Assert.That(TargetDevice.IsConnected, Is.True);
        await AssertPanelToDeviceCommsAreHealthy();
    }

}
