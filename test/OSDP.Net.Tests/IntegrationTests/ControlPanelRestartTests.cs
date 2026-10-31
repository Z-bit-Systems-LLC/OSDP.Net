using System.Threading.Tasks;
using NUnit.Framework;

namespace OSDP.Net.Tests.IntegrationTests;

/// <summary>
/// A <see cref="ControlPanel"/> stays usable after <see cref="ControlPanel.Shutdown"/>; ACUConsole
/// shuts down and starts a new connection on the same panel when its settings change.
/// </summary>
[Category("Integration")]
public class ControlPanelRestartTests : IntegrationTestFixtureBase
{
    [Test]
    public async Task Shutdown_ThenStartConnection_StillDeliversReplies()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = false);
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();
        await TargetPanel.IdReport(ConnectionId, DeviceAddress);

        await TargetPanel.Shutdown();
        await RestartTargetPanelConnection();
        AddDeviceToPanel(useSecureChannel: false);
        await WaitForDeviceOnlineStatus();

        var id = await TargetPanel.IdReport(ConnectionId, DeviceAddress);

        Assert.That(id, Is.Not.Null);
    }
}
