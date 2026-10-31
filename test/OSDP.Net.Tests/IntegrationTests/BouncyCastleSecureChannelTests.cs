using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Tests.IntegrationTests;

/// <summary>
/// Runs secure channel with both sides on the BouncyCastle AES fallback, the path browser
/// WebAssembly takes. The switch is static, so this fixture must not run alongside others.
/// </summary>
[NonParallelizable]
[Category("Integration")]
[Category("Compliance.Security")]
public class BouncyCastleSecureChannelTests : IntegrationTestFixtureBase
{
    [OneTimeSetUp]
    public void ForceBouncyCastle() => BouncyCastleAes.ForceBouncyCastle = true;

    [OneTimeTearDown]
    public void RestorePlatformAes() => BouncyCastleAes.ForceBouncyCastle = false;

    [Test]
    public async Task DefaultKey_EstablishesSecureChannel()
    {
        await InitTestTargets(cfg => cfg.RequireSecurity = true);
        AddDeviceToPanel(IntegrationConsts.DefaultSCBK);
        await WaitForDeviceOnlineStatus();

        var id = await TargetPanel.IdReport(ConnectionId, DeviceAddress);

        Assert.That(id.VendorCode, Has.Length.EqualTo(3));
    }

    [Test]
    public async Task CustomKey_EstablishesSecureChannel()
    {
        await InitTestTargets(cfg =>
        {
            cfg.SecurityKey = IntegrationConsts.NonDefaultSCBK;
            cfg.RequireSecurity = true;
        });
        AddDeviceToPanel(IntegrationConsts.NonDefaultSCBK);
        await WaitForDeviceOnlineStatus();

        var capabilities = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);

        Assert.That(capabilities.Capabilities.Count(), Is.GreaterThan(0));
    }
}
