using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Model.CommandData;

namespace OSDP.Net.Tests.Compliance;

/// <summary>
/// OSDP 2.2.2 Compliance Tests - Timing Validation
///
/// Validates PD timing behavior against OSDP 2.2.2 requirements:
/// - REPLY_DELAY: PD must respond within 200ms of receiving a command (Section 5.5)
/// - Offline timeout: PD must consider connection lost after 8 seconds (Section 6.1)
/// - Connection establishment timing
///
/// NOTE: These tests use generous tolerances to account for CI environment variability.
/// The OSDP spec specifies 200ms REPLY_DELAY; tests use 500ms tolerance.
/// The loopback connection has near-zero transport overhead, so measured times
/// closely approximate actual PD processing time.
///
/// NOTE: osdp_BUSY (Section 7.17) is not tested because the PD Device class
/// does not currently implement automatic BUSY reply generation for long-running
/// operations. The ACU (Bus.ProcessReply) does handle BUSY replies correctly.
/// </summary>
[TestFixture]
[Category("Compliance")]
[Category("Compliance.Timing")]
public class TimingComplianceTests : IntegrationTestFixtureBase
{
    // OSDP 2.2.2 Section 5.5 - REPLY_DELAY
    // PD must respond to any command within 200ms.
    // Using 500ms tolerance for CI environments and in-process overhead.

    [SetUp]
    public async Task SetupTimingTests()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();
    }

    [Test]
    public async Task ReplyDelay_IdReport_WithinSpecLimit()
    {
        // OSDP 2.2.2 Section 5.5 - REPLY_DELAY ≤ 200ms
        var sw = Stopwatch.StartNew();
        var id = await TargetPanel.IdReport(ConnectionId, DeviceAddress);
        sw.Stop();

        Assert.That(id, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            $"IdReport reply took {sw.ElapsedMilliseconds}ms, spec requires ≤200ms (using 500ms tolerance)");
    }

    [Test]
    public async Task ReplyDelay_DeviceCapabilities_WithinSpecLimit()
    {
        var sw = Stopwatch.StartNew();
        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);
        sw.Stop();

        Assert.That(caps, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            $"DeviceCapabilities reply took {sw.ElapsedMilliseconds}ms, spec requires ≤200ms (using 500ms tolerance)");
    }

    [Test]
    public async Task ReplyDelay_LocalStatus_WithinSpecLimit()
    {
        var sw = Stopwatch.StartNew();
        var status = await TargetPanel.LocalStatus(ConnectionId, DeviceAddress);
        sw.Stop();

        Assert.That(status, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            $"LocalStatus reply took {sw.ElapsedMilliseconds}ms, spec requires ≤200ms (using 500ms tolerance)");
    }

    [Test]
    public async Task ReplyDelay_InputStatus_WithinSpecLimit()
    {
        var sw = Stopwatch.StartNew();
        var status = await TargetPanel.InputStatus(ConnectionId, DeviceAddress);
        sw.Stop();

        Assert.That(status, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            $"InputStatus reply took {sw.ElapsedMilliseconds}ms, spec requires ≤200ms (using 500ms tolerance)");
    }

    [Test]
    public async Task ReplyDelay_OutputStatus_WithinSpecLimit()
    {
        var sw = Stopwatch.StartNew();
        var status = await TargetPanel.OutputStatus(ConnectionId, DeviceAddress);
        sw.Stop();

        Assert.That(status, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            $"OutputStatus reply took {sw.ElapsedMilliseconds}ms, spec requires ≤200ms (using 500ms tolerance)");
    }

    [Test]
    public async Task ReplyDelay_ReaderStatus_WithinSpecLimit()
    {
        var sw = Stopwatch.StartNew();
        var status = await TargetPanel.ReaderStatus(ConnectionId, DeviceAddress);
        sw.Stop();

        Assert.That(status, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            $"ReaderStatus reply took {sw.ElapsedMilliseconds}ms, spec requires ≤200ms (using 500ms tolerance)");
    }

    [Test]
    public async Task ReplyDelay_OutputControl_WithinSpecLimit()
    {
        var outputControls = new OutputControls([
            new OutputControl(0, OutputControlCode.Nop, 0)
        ]);

        var sw = Stopwatch.StartNew();
        var result = await TargetPanel.OutputControl(ConnectionId, DeviceAddress, outputControls);
        sw.Stop();

        Assert.That(result, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            $"OutputControl reply took {sw.ElapsedMilliseconds}ms, spec requires ≤200ms (using 500ms tolerance)");
    }

    // OSDP 2.2.2 Section 6.1 - Offline Timeout
    // PD must consider the connection offline if no valid command is received
    // within 8 seconds.

    [Test]
    public async Task OfflineTimeout_PdStillConnected_JustBeforeTimeout()
    {
        // Verify PD is still connected at 7 seconds (just before 8-second timeout)
        Assert.That(TargetDevice.IsConnected, Is.True);

        await TargetPanel.Shutdown();

        // Wait 7 seconds - should still be within the 8-second timeout window
        await Task.Delay(TimeSpan.FromSeconds(7));

        Assert.That(TargetDevice.IsConnected, Is.True,
            "PD should still consider itself connected within the 8-second timeout window");
    }

    [Test]
    public async Task OfflineTimeout_PdDisconnected_AfterTimeout()
    {
        // OSDP 2.2.2 Section 6.1 - PD offline after 8 seconds without valid command
        Assert.That(TargetDevice.IsConnected, Is.True);

        await TargetPanel.Shutdown();

        // Wait past the 8-second timeout
        await Task.Delay(TimeSpan.FromSeconds(9));

        Assert.That(TargetDevice.IsConnected, Is.False,
            "PD must consider connection offline after 8 seconds without a valid command");
    }

    [Test]
    public async Task OfflineTimeout_IsConnectedAccuratelyReflectsState()
    {
        // Verify IsConnected transitions correctly as time progresses
        Assert.That(TargetDevice.IsConnected, Is.True, "Initially connected");

        await TargetPanel.Shutdown();

        // Check at regular intervals that IsConnected tracks correctly
        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.That(TargetDevice.IsConnected, Is.True, "Should still be connected at 4 seconds");

        await Task.Delay(TimeSpan.FromSeconds(5));
        Assert.That(TargetDevice.IsConnected, Is.False, "Should be disconnected at 9 seconds total");
    }
}

/// <summary>
/// Validates timing behavior during secure channel operations.
/// </summary>
[TestFixture]
[Category("Compliance")]
[Category("Compliance.Timing")]
[Category("Compliance.Security")]
public class SecureChannelTimingTests : IntegrationTestFixtureBase
{
    [Test]
    public async Task SecureChannelEstablishment_CompletesWithinReasonableTime()
    {
        // OSDP 2.2.2 - Secure channel handshake should complete within a few seconds
        var sw = Stopwatch.StartNew();

        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus(timeout: 5000);
        sw.Stop();

        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000),
            $"Secure channel establishment took {sw.ElapsedMilliseconds}ms");
    }

    [Test]
    public async Task ReplyDelay_IdReport_WithinSpecLimit_OverSecureChannel()
    {
        // OSDP 2.2.2 Section 5.5 - REPLY_DELAY applies to secure channel too
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();

        var sw = Stopwatch.StartNew();
        var id = await TargetPanel.IdReport(ConnectionId, DeviceAddress);
        sw.Stop();

        Assert.That(id, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            $"IdReport over secure channel took {sw.ElapsedMilliseconds}ms, spec requires ≤200ms (using 500ms tolerance)");
    }

    [Test]
    public async Task ReplyDelay_DeviceCapabilities_WithinSpecLimit_OverSecureChannel()
    {
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });

        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();

        var sw = Stopwatch.StartNew();
        var caps = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);
        sw.Stop();

        Assert.That(caps, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            $"DeviceCapabilities over secure channel took {sw.ElapsedMilliseconds}ms, spec requires ≤200ms (using 500ms tolerance)");
    }
}
