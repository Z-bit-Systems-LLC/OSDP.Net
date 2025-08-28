using System;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using NUnit.Framework;
using OSDP.Net.Connections;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using DeviceCapabilities = OSDP.Net.Model.ReplyData.DeviceCapabilities;
using System.Collections.Concurrent;
using OSDP.Net.Messages;
using OSDP.Net.Messages.SecureChannel;

namespace OSDP.Net.Tests.IntegrationTests;

public sealed class IntegrationConsts
{
    public static readonly byte[] NonDefaultSCBK = [0xa, 0xb, 0xc, 0xd, 0xe, 0xf, 0x1, 0x2, 0x3, 0x4, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf];
    public static readonly byte[] DefaultSCBK = "0123456789:;<=>?"u8.ToArray();
    public const int DefaultTestBaud = 9600;
    public const byte DefaultTestDeviceAddr = 0;
}

public class IntegrationTestFixtureBase
{
    protected ILoggerFactory LoggerFactory;

    protected byte DeviceAddress;
    protected Guid ConnectionId;

    private TaskCompletionSource<bool> _deviceOnlineCompletionSource;
    private readonly ConcurrentQueue<EventCheckpoint> _eventCheckpoints = new();
    private readonly object _syncLock = new();

    protected ControlPanel TargetPanel { get; private set; }
    protected TestDevice TargetDevice { get; private set; }

    [SetUp]
    public void Setup()
    {
        // Each test gets spun up with its own console capture so we have to create
        // a new logger factory instance for every single test. Otherwise, the test runner
        // isn't able to associate stdout output with the particular test
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("OSDP.Net", LogLevel.Debug)
                .SetMinimumLevel(LogLevel.Debug)
                .AddConsole();
        });
    }

    [TearDown]
    public async Task Teardown()
    {
        await (TargetPanel?.Shutdown() ?? Task.CompletedTask);
        await (TargetDevice?.StopListening() ?? Task.CompletedTask);

        TargetDevice?.Dispose();
        LoggerFactory?.Dispose();
    }

    protected async Task InitTestTargets(
        Action<DeviceConfiguration> configureDevice = null,
        Action<PanelConfiguration> configurePanel = null)
    {
        InitTestTargetDevice(configureDevice, baudRate: IntegrationConsts.DefaultTestBaud);
        await InitTestTargetPanel(configurePanel, baudRate: IntegrationConsts.DefaultTestBaud);
    }

    protected async Task InitTestTargetPanel(Action<PanelConfiguration> configurePanel = null, int baudRate = IntegrationConsts.DefaultTestBaud)
    {
        var panelConfiguration = new PanelConfiguration();
        configurePanel?.Invoke(panelConfiguration);

        _deviceOnlineCompletionSource = new TaskCompletionSource<bool>();

        TargetPanel = new ControlPanel(
            panelConfiguration.TestDeviceProxyNeeded ? new TestDeviceProxyFactory(panelConfiguration) : null,
            LoggerFactory.CreateLogger<ControlPanel>());

        TargetPanel.ConnectionStatusChanged += (_, e) =>
        {
            TestContext.WriteLine($"Received event: {e}");

            if (e.ConnectionId != ConnectionId) return;

            lock (_syncLock)
            {
                TestEventType currentEventType;

                if (e.IsConnected)
                {
                    _deviceOnlineCompletionSource.TrySetResult(true);
                    currentEventType = TestEventType.ConnectionEstablished;
                }
                else
                {
                    currentEventType = TestEventType.ConnectionLost;
                    if (_deviceOnlineCompletionSource.Task.IsCompleted)
                    {
                        _deviceOnlineCompletionSource = new TaskCompletionSource<bool>();
                    }
                }

                if (_eventCheckpoints.TryPeek(out var checkpoint) && checkpoint.EventType == currentEventType)
                {
                    _eventCheckpoints.TryDequeue(out var _);
                    checkpoint.Tcs.TrySetResult(true);
                }
            }
        };

        await RestartTargetPanelConnection(baudRate);
    }

    protected async Task RestartTargetPanelConnection(int baudRate = IntegrationConsts.DefaultTestBaud)
    {
        if (ConnectionId != Guid.Empty)
        {
            await TargetPanel.StopConnection(ConnectionId);
        }


        ConnectionId = TargetPanel.StartConnection(new TcpClientOsdpConnection("localhost", 6000, baudRate));
    }

    protected void InitTestTargetDevice(
        Action<DeviceConfiguration> configureDevice = null, int baudRate = IntegrationConsts.DefaultTestBaud)
    {
        var deviceConfig = new DeviceConfiguration() { Address = IntegrationConsts.DefaultTestDeviceAddr };
        configureDevice?.Invoke(deviceConfig);

        DeviceAddress = deviceConfig.Address;

        TargetDevice = new TestDevice(deviceConfig, LoggerFactory);
        TargetDevice.StartListening(new TcpConnectionListener(6000, baudRate, LoggerFactory));
    }

    protected void AddDeviceToPanel(
        byte[] securityKey = null, byte? address = null, bool useSecureChannel = true, bool useCrc = true)
    {
        if (address != null)
        {
            DeviceAddress = address.Value;
        }

        _deviceOnlineCompletionSource = new TaskCompletionSource<bool>();
        TargetPanel.AddDevice(ConnectionId, DeviceAddress, useCrc, useSecureChannel, securityKey);
    }

    protected void RemoveDeviceFromPanel()
    {
        TargetPanel.RemoveDevice(ConnectionId, DeviceAddress);
    }

    protected async Task WaitForDeviceOnlineStatus(int timeout = 10000)
    {
        var onlineTask = _deviceOnlineCompletionSource.Task;
        if (await Task.WhenAny(onlineTask, Task.Delay(timeout)) != onlineTask)
        {
            Assert.Fail("Timeout waiting for device connection to come online");
        }
    }

    protected async Task AssertPanelRemainsDisconnected(int timeout = 10000)
    {
        var onlineTask = _deviceOnlineCompletionSource.Task;
        if (await Task.WhenAny(onlineTask, Task.Delay(timeout)) == onlineTask)
        {
            Assert.Fail("This connections was expected to fail but IT DID NOT!!");
        }
    }

    protected async Task AssertPanelToDeviceCommsAreHealthy()
    {
        var capabilities = await TargetPanel.DeviceCapabilities(ConnectionId, DeviceAddress);
        Assert.NotNull(capabilities);
    }

    protected async Task SetupCheckpointForExpectedTestEvent(TestEventType eventType, int timeout = 10000)
    {
        TaskCompletionSource<bool> tcs = new();

        _eventCheckpoints.Enqueue(new EventCheckpoint() { EventType = eventType, Tcs = tcs });

        var result = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        if (result != tcs.Task)
        {
            Assert.Fail($"Timeout waiting for event checkpoint '{eventType}'");
        }
    }

    protected TestCommand BuildTestCommand(CommandType type)
    {
        TestCommand command = type switch
        {
            CommandType.IdReport => new IdReportCommand(),
            CommandType.DeviceCapabilities => new DeviceCapabilitiesCommand(),
            CommandType.LocalStatus => new LocalStatusCommand(),
            CommandType.InputStatus => new InputStatusCommand(),
            CommandType.OutputStatus => new OutputStatusCommand(),
            CommandType.ReaderStatus => new ReaderStatusCommand(),
            CommandType.OutputControl => new OutputControlCommand(),

            CommandType.CommunicationSet => new CommunicationSetCommand(),
            _ => throw new Exception("Unsupported command type")
        };
        
        command.Panel = TargetPanel;
        command.ConnectionId = ConnectionId;
        command.DeviceAddress = DeviceAddress;

        return command;
    }

    protected enum TestEventType
    {
        ConnectionLost,
        ConnectionEstablished
    }

    private class EventCheckpoint
    {
        public TestEventType EventType { get; init; }

        public TaskCompletionSource<bool> Tcs { get; init; }
    }
}

public class TestConfiguration
{
    public DeviceConfiguration Device { get; } = new () { Address = IntegrationConsts.DefaultTestDeviceAddr };

    internal PanelConfiguration Panel { get; } = new ();
}

public class PanelConfiguration
{
    internal Action<OutgoingMessage, IMessageSecureChannel> OnGetNextCommand { get; set; }

    public bool TestDeviceProxyNeeded { get => OnGetNextCommand != null; }
}


internal class TestDeviceProxy : DeviceProxy
{
    private readonly PanelConfiguration _panelConfiguration;

    public TestDeviceProxy(byte address, bool useCrc, bool useSecureChannel, 
        byte[] secureChannelKey, PanelConfiguration panelConfiguration)
        : base(address, useCrc, useSecureChannel, secureChannelKey) 
    {
        _panelConfiguration = panelConfiguration;
    }

    internal override OutgoingMessage GetNextCommandData(bool isPolling)
    {
        var command = base.GetNextCommandData(isPolling);
        _panelConfiguration.OnGetNextCommand?.Invoke(command, MessageSecureChannel);
        return command;
    }
}

internal class TestDeviceProxyFactory : IDeviceProxyFactory
{
    private readonly PanelConfiguration _panelConfiguration;

    internal TestDeviceProxyFactory(PanelConfiguration panelConfiguration)
    {
        _panelConfiguration = panelConfiguration;
    }

    public DeviceProxy Create(
        byte address, bool useCrc, bool useSecureChannel, byte[] secureChannelKey = null)
        => new TestDeviceProxy(address, useCrc, useSecureChannel, secureChannelKey, _panelConfiguration);
}

public abstract class TestCommand
{
    public ControlPanel Panel { get; set; }
    public Guid ConnectionId { get; set; }
    public byte DeviceAddress { get; set; }

    public abstract Task<TestReply> Run();
}

public class TestReply
{

}

public class IdReportCommand : TestCommand
{
    public override async Task<TestReply> Run() =>
        new IdReportReply { DeviceIdentification = await Panel.IdReport(ConnectionId, DeviceAddress) };
}

public class IdReportReply : TestReply
{
    public DeviceIdentification DeviceIdentification { get; set; }
}

public class DeviceCapabilitiesCommand : TestCommand
{
    public override async Task<TestReply> Run() =>
        new DeviceCapabilitiesReply { Response = await Panel.DeviceCapabilities(ConnectionId, DeviceAddress) };
}

public class DeviceCapabilitiesReply : TestReply
{
    public DeviceCapabilities Response { get; set; }
}

public class LocalStatusCommand : TestCommand
{
    public override async Task<TestReply> Run() =>
        new LocalStatusReply { Response = await Panel.LocalStatus(ConnectionId, DeviceAddress) };
}

public class LocalStatusReply : TestReply
{
    public LocalStatus Response { get; set; }
}

public class InputStatusCommand : TestCommand
{
    public override async Task<TestReply> Run() =>
        new InputStatusReply { Response = await Panel.InputStatus(ConnectionId, DeviceAddress) };
}

public class InputStatusReply : TestReply
{
    public InputStatus Response { get; set; }
}

public class OutputStatusCommand : TestCommand
{
    public override async Task<TestReply> Run() =>
        new OutputStatusReply { Response = await Panel.OutputStatus(ConnectionId, DeviceAddress) };
}

public class OutputStatusReply : TestReply
{
    public OutputStatus Response { get; set; }
}

public class ReaderStatusCommand : TestCommand
{
    public override async Task<TestReply> Run() =>
        new ReaderStatusReply { Response = await Panel.ReaderStatus(ConnectionId, DeviceAddress) };
}

public class ReaderStatusReply : TestReply
{
    public ReaderStatus Response { get; set; }
}

public class OutputControlCommand : TestCommand
{
    public OutputControls OutputControls { get; set; } = new OutputControls([
        new OutputControl(0, OutputControlCode.Nop, 0)
    ]);

    public override async Task<TestReply> Run() =>
        new OutputControlReply { Response = await Panel.OutputControl(ConnectionId, DeviceAddress, OutputControls) };
}

public class OutputControlReply : TestReply
{
    public ReturnReplyData<OutputStatus> Response { get; set; }
}

public class CommunicationSetCommand : TestCommand
{
    public Net.Model.CommandData.CommunicationConfiguration CommConfig { get; set; } = new(0, 9600);

    public override async Task<TestReply> Run() =>
        new CommunicationSetReply { Response = await Panel.CommunicationConfiguration(ConnectionId, DeviceAddress, CommConfig) };
}

public class CommunicationSetReply : TestReply
{
    public Net.Model.ReplyData.CommunicationConfiguration Response { get; set; }
}
