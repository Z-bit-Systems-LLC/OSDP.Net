# %OSDP.Net API reference {#mainpage}

%OSDP.Net is a .NET implementation of the Open Supervised Device Protocol (%OSDP), the SIA standard for
communication between access control panels and the readers and other peripheral devices (PDs) connected to
them. The library can act as either side of the bus: a control panel (ACU) or a peripheral device.

It targets .NET 8 and later and .NET Framework 4.6.2 and later. Install it from
[NuGet](https://www.nuget.org/packages/OSDP.Net/):

```shell
dotnet add package OSDP.Net
```

## Where to start

| You are building | Start with | Then look at |
|:--|:--|:--|
| A control panel (ACU) | OSDP.Net.ControlPanel | Its command methods and `...Received` events |
| A peripheral device (PD) | OSDP.Net.Device and OSDP.Net.DeviceConfiguration | The `Handle...` methods to override |
| A connection | OSDP.Net.Connections.SerialPortOsdpConnection, OSDP.Net.Connections.TcpClientOsdpConnection, OSDP.Net.Connections.TcpServerOsdpConnection | OSDP.Net.Connections.IOsdpConnection to write your own |
| A PD listener | OSDP.Net.Connections.SerialPortConnectionListener, OSDP.Net.Connections.TcpConnectionListener | OSDP.Net.Connections.IOsdpConnectionListener |

Command payloads live in the OSDP.Net.Model.CommandData namespace and reply payloads in OSDP.Net.Model.ReplyData.

## A control panel in a few lines

Register event handlers before starting the connection, and keep them short: they run on the thread that
polls the bus.

```cs
var panel = new ControlPanel();
panel.ConnectionStatusChanged += (_, e) =>
    Console.WriteLine($"PD {e.Address} online: {e.IsConnected}, secure: {e.IsSecureChannelEstablished}");
panel.RawCardDataReplyReceived += (_, e) =>
    Console.WriteLine($"Card read on PD {e.Address}: {e.RawCardData.BitCount} bits");

Guid connectionId = panel.StartConnection(new SerialPortOsdpConnection("COM3", 9600));
panel.AddDevice(connectionId, address: 0, useCrc: true, useSecureChannel: true);

await panel.ReaderBuzzerControl(connectionId, 0,
    new ReaderBuzzerControl(0, ToneCode.Default, 2, 2, 1));
```

## A peripheral device in a few lines

Derive from OSDP.Net.Device and override the handlers for the commands your device supports. Commands
without an override are answered with an `osdp_NAK`.

```cs
public class MyReader : Device
{
    // Vendor code and serial number identify the PD during the Secure Channel handshake.
    public MyReader() : base(new DeviceConfiguration(new ClientIdentification([0x12, 0x34, 0x56], 12345)))
    {
    }

    protected override PayloadData HandleReaderLEDControl(ReaderLedControls commandPayload)
    {
        // Drive the LEDs here.
        return new Ack();
    }
}

var reader = new MyReader();
await reader.StartListening(new SerialPortConnectionListener("COM4", 9600));
```

Card reads, keypad entries and other unsolicited replies are queued with OSDP.Net.Device.EnqueuePollReply
and sent on the next poll.

## Secure Channel

The library runs the Secure Channel handshake itself. On the ACU, pass `useSecureChannel: true` (and a
`secureChannelKey`, or the default installation key is used) to OSDP.Net.ControlPanel.AddDevice, then
watch `IsSecureChannelEstablished` on OSDP.Net.ControlPanel.ConnectionStatusChanged. To give a PD its own
key, call OSDP.Net.ControlPanel.EncryptionKeySet while the channel is secure. On the PD, set
OSDP.Net.DeviceConfiguration.RequireSecurity and OSDP.Net.DeviceConfiguration.SecurityKey.

## Errors

Command methods are awaitable. When the PD rejects a command with `osdp_NAK`, the task fails with
OSDP.Net.NackReplyException, whose `Reply` holds the error code. A PD that doesn't answer within
8 seconds fails the task with a `TimeoutException`. Every NAK is also raised as OSDP.Net.ControlPanel.NakReplyReceived.

## More documentation

- [API usage guide](https://github.com/Z-bit-Systems-LLC/OSDP.Net/blob/main/docs/api-usage-guide.md): longer
  ACU and PD examples, TCP connections, logging
- [Supported commands and replies](https://github.com/Z-bit-Systems-LLC/OSDP.Net/blob/main/docs/supported_commands.md),
  with links back into this reference
- [Tracing guide](https://github.com/Z-bit-Systems-LLC/OSDP.Net/blob/main/docs/tracing-guide.md) and
  [transparent mode guide](https://github.com/Z-bit-Systems-LLC/OSDP.Net/blob/main/docs/transparent-mode-guide.md)
- [Source code and issues](https://github.com/Z-bit-Systems-LLC/OSDP.Net) on GitHub
- [OSDP.Net on z-bitco.com](https://z-bitco.com/osdp-net/), including the ACU and PD test consoles
