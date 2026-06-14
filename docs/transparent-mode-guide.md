# OSDP Transparent Mode Guide

This guide explains how to use OSDP transparent mode (commands `osdp_XWR` / `osdp_XRD`) in OSDP.Net to tunnel ISO 7816-4 smart-card APDUs between an ACU and a smart card through a PD's reader.

## Table of Contents

1. [What Transparent Mode Is](#what-transparent-mode-is)
2. [Protocol Overview](#protocol-overview)
3. [ACU Usage](#acu-usage)
4. [PD Usage](#pd-usage)
5. [End-to-End Example](#end-to-end-example)
6. [Security](#security)
7. [Reference](#reference)

## What Transparent Mode Is

Transparent mode lets an ACU send raw smart-card commands (APDUs) to a contact or contactless smart card by tunneling them through a PD's reader over the OSDP link. The PD acts as a bridge: it forwards the ACU's APDUs to the card and relays the card's responses back to the ACU.

Typical uses:

- PIV / CAC credential authentication
- Challenge/response with smart cards
- Any ISO 7816-4 based credential where the authentication logic lives on the ACU rather than the PD

Two OSDP message types carry transparent-mode traffic:

| Code | Name      | Direction | Purpose                                    |
|------|-----------|-----------|--------------------------------------------|
| 0xA1 | osdp_XWR  | ACU → PD  | Extended write — configuration or APDU     |
| 0xB1 | osdp_XRD  | PD → ACU  | Extended read — status or APDU response    |

Each message carries three fields:

- **Mode** — `0` for configuration, `1` for transparent APDU
- **PCommand** / **PReply** — mode-specific command or reply code
- **PData** — variable-length payload

## Protocol Overview

Transparent mode has two high-level phases:

### Phase 1 — Enable Mode 1

The ACU queries the PD for its current mode and, if needed, switches it to Mode 1 (transparent APDU passthrough). This is done with two Mode-0 commands:

```
ACU → PD : XWR Mode=0 PCommand=1             ; ReadModeSetting
PD → ACU : XRD Mode=0 PReply=1   PData=[curMode, enabled]
ACU → PD : XWR Mode=0 PCommand=2 PData=[1,0] ; ModeOneConfiguration
PD → ACU : ACK
```

### Phase 2 — APDU Exchange

Once Mode 1 is active, the ACU can scan for a card and, when one is present, pass APDUs:

```
ACU → PD : XWR Mode=1 PCommand=4 PData=[readerNumber]       ; ModeOneSmartCardScan
PD → ACU : ACK                                              ; no card yet (or…)
PD → ACU : XRD Mode=1 PReply=1   PData=[readerNumber]       ; unsolicited card-present on next Poll
ACU → PD : XWR Mode=1 PCommand=1 PData=[reader, apdu...]    ; ModeOnePassAPDUCommand
PD → ACU : XRD Mode=1 PReply=1   PData=[reader, resp...]    ; APDU response
ACU → PD : XWR Mode=1 PCommand=2 PData=[readerNumber]       ; ModeOneTerminateSmartCardConnection
PD → ACU : XRD Mode=1 PReply=2   PData=[readerNumber]       ; session terminated
```

Note that unsolicited XRDs (card-present notifications) ride on the next `osdp_POLL`, so their latency equals the ACU's configured poll interval.

## ACU Usage

### Sending an XWR and awaiting the XRD reply

Use `ControlPanel.ExtendedWriteData` for synchronous request/response:

```csharp
var panel = new ControlPanel(loggerFactory);
var connectionId = panel.StartConnection(new SerialPortOsdpConnection("COM1", 9600));
panel.AddDevice(connectionId, deviceAddress: 0, useCrc: true, useSecureChannel: true);

// Ask the PD what mode it is in
var reply = await panel.ExtendedWriteData(
    connectionId,
    address: 0,
    ExtendedWrite.ReadModeSetting());

if (reply.ReplyData is { Mode: 0, PReply: 1 })
{
    byte currentMode = reply.ReplyData.PData[0];
    bool enabled = reply.ReplyData.PData[1] != 0;
    Console.WriteLine($"PD mode: {currentMode}, enabled: {enabled}");
}
```

`ReturnReplyData<ExtendedRead>` exposes:

- `Ack` — true if the PD acknowledged with a bare `osdp_ACK`
- `ReplyData` — populated when the PD replied with `osdp_XRD`, otherwise `null`

### Switching to Mode 1

```csharp
await panel.ExtendedWriteData(
    connectionId, address,
    ExtendedWrite.ModeOneConfiguration());
```

### Passing APDUs through to the card

```csharp
byte readerNumber = 0;
byte[] selectAid = [0x00, 0xA4, 0x04, 0x00, 0x07, 0xA0, 0x00, 0x00, 0x03, 0x08, 0x00, 0x00];

var apduReply = await panel.ExtendedWriteData(
    connectionId, address,
    ExtendedWrite.ModeOnePassAPDUCommand(readerNumber, selectAid));

if (apduReply.ReplyData is { Mode: 1, PReply: 1 })
{
    // PData layout: [readerNumber, apduResponseBytes...]
    byte[] response = apduReply.ReplyData.PData[1..];
    Console.WriteLine($"Card response: {BitConverter.ToString(response)}");
}
```

### Subscribing to unsolicited XRDs

When the PD pushes an XRD on a Poll (for example, a card-present notification), the ACU surfaces it through `ExtendedReadReplyReceived`:

```csharp
panel.ExtendedReadReplyReceived += (_, e) =>
{
    var xrd = e.ExtendedRead;
    if (xrd is { Mode: 1, PReply: 1 })
    {
        byte reader = xrd.PData[0];
        Console.WriteLine($"Card present on reader {reader}");
    }
};
```

### Terminating the smart-card session

```csharp
await panel.ExtendedWriteData(
    connectionId, address,
    ExtendedWrite.ModeOneTerminateSmartCardConnection(readerNumber));
```

## PD Usage

On the PD side, override `Device.HandleExtendedWrite` to react to incoming XWR commands. The base implementation returns `Nak(UnknownCommandCode)`, so any PD that needs transparent-mode support must override this.

```csharp
public class MyPd : Device
{
    private byte _transparentMode;
    private bool _sessionActive;

    public MyPd(DeviceConfiguration config, ILoggerFactory factory)
        : base(config, factory) { }

    protected override PayloadData HandleExtendedWrite(ExtendedWrite commandPayload)
    {
        switch (commandPayload.Mode)
        {
            case 0:
                return HandleModeZero(commandPayload);
            case 1:
                return HandleModeOne(commandPayload);
            default:
                return new Nak(ErrorCode.UnableToProcessCommand);
        }
    }

    private PayloadData HandleModeZero(ExtendedWrite cmd) => cmd.PCommand switch
    {
        1 => ExtendedRead.ModeZeroSettingReport(_transparentMode, _transparentMode == 1),
        2 when cmd.PData.Length >= 2 => SetMode(cmd.PData[0]),
        _ => new Nak(ErrorCode.UnableToProcessCommand)
    };

    private PayloadData SetMode(byte newMode)
    {
        _transparentMode = newMode;
        _sessionActive = false;
        return new Ack();
    }

    private PayloadData HandleModeOne(ExtendedWrite cmd)
    {
        if (cmd.PData.Length < 1)
            return new Nak(ErrorCode.UnableToProcessCommand);

        byte reader = cmd.PData[0];

        return cmd.PCommand switch
        {
            1 => ForwardApduToCard(reader, cmd.PData[1..]),
            2 => TerminateSession(reader),
            4 => ScanForCard(reader),
            _ => new Nak(ErrorCode.UnableToProcessCommand)
        };
    }

    private PayloadData ForwardApduToCard(byte reader, byte[] apdu)
    {
        _sessionActive = true;
        byte[] response = MySmartCardDriver.Transmit(apdu);
        return ExtendedRead.ApduResponse(reader, response);
    }

    private PayloadData TerminateSession(byte reader)
    {
        _sessionActive = false;
        return ExtendedRead.SessionTerminated(reader);
    }

    private PayloadData ScanForCard(byte reader)
        => _sessionActive
            ? ExtendedRead.CardPresent(reader)
            : new Ack();
}
```

### Pushing unsolicited card-present notifications

When a real card is presented to the reader, the PD should push an XRD on the next poll by enqueuing a reply:

```csharp
public void OnCardInserted(byte readerNumber)
{
    _sessionActive = true;
    EnqueuePollReply(ExtendedRead.CardPresent(readerNumber));
}
```

The ACU will receive it on its next `osdp_POLL` and surface it through the `ExtendedReadReplyReceived` event. Push latency therefore equals the ACU's poll interval (typically 100–200 ms).

### Handler return types

| Return value              | Wire effect                                                    |
|---------------------------|----------------------------------------------------------------|
| `ExtendedRead.*(...)`     | Sends `osdp_XRD` — synchronous answer to the ACU's XWR call    |
| `new Ack()`               | Sends `osdp_ACK` — used when there is no data to return yet    |
| `new Nak(ErrorCode.*)`    | Sends `osdp_NAK` — ACU's `ExtendedWriteData` call will throw   |

### Semantic factory methods on `ExtendedRead`

| Factory                                           | Produces                              | Typical use                                                              |
|---------------------------------------------------|---------------------------------------|--------------------------------------------------------------------------|
| `ModeZeroSettingReport(mode, enabled)`            | `Mode=0, PReply=1, [mode, enabled]`   | Reply to `ReadModeSetting`                                               |
| `CardPresent(readerNumber)`                       | `Mode=1, PReply=1, [reader]`          | Unsolicited card-present or scan confirmation                            |
| `ApduResponse(readerNumber, response)`            | `Mode=1, PReply=1, [reader, resp...]` | APDU response from the smart card                                        |
| `SessionTerminated(readerNumber)`                 | `Mode=1, PReply=2, [reader]`          | Confirmation of `ModeOneTerminateSmartCardConnection`                    |

You can also construct an `ExtendedRead` directly if your PD emits non-standard transparent payloads:

```csharp
return new ExtendedRead(mode: 1, pReply: 1, pData: [readerNumber, 0x6A, 0x82]);
```

## End-to-End Example

A full ACU sample that drives transparent mode ships in `src/samples/SmartCardSample`. It demonstrates:

- Polling `ReadModeSetting` to detect when the PD is still in Mode 0
- Switching to Mode 1 via `ModeOneConfiguration`
- Waiting for an unsolicited `CardPresent` notification via `ExtendedReadReplyReceived`
- Running an interactive APDU REPL that forwards hex-encoded APDUs to the card
- Cleanly tearing down the session with `ModeOneTerminateSmartCardConnection`

For the PD side, `src/PDConsole/PDDevice.cs` contains a simulated implementation that answers every transparent-mode command with canned responses, including `SimulateSmartCardPresent(readerNumber)` to push an unsolicited card-present notification from the UI. The two samples can be pointed at each other over a loopback serial port to exercise the full flow without real hardware.

## Security

Both `ExtendedWrite` (XWR) and `ExtendedRead` (XRD) are sent with `CommandMessageWithDataSecurity` / `ReplyMessageWithDataSecurity` security control blocks. This means their payloads are encrypted when the secure channel is established.

Because smart-card APDUs frequently carry sensitive material (PINs, challenges, certificates), running transparent mode over a fully-established secure channel is strongly recommended:

```csharp
var deviceConfig = new DeviceConfiguration(clientId)
{
    SecurityKey = productionScbk,
    RequireSecurity = true
};
```

On the ACU side, pass `useSecureChannel: true` when adding the device and supply the matching SCBK:

```csharp
panel.AddDevice(connectionId, address, useCrc: true, useSecureChannel: true, securityKey: productionScbk);
```

## Reference

### `ExtendedWrite` factory methods

| Factory                                       | Wire fields                  | Purpose                                                  |
|------------------------------------------------|------------------------------|----------------------------------------------------------|
| `ReadModeSetting()`                            | `0, 1, []`                   | Query current mode                                       |
| `ModeZeroConfiguration(bool enabled)`          | `0, 2, [0, enabled]`         | Enable / disable transparent mode                        |
| `ModeOneConfiguration()`                       | `0, 2, [1, 0]`               | Switch PD to Mode 1                                      |
| `ModeOneSmartCardScan(byte reader)`            | `1, 4, [reader]`             | Ask the PD to scan the reader for a card                 |
| `ModeOnePassAPDUCommand(byte reader, byte[])`  | `1, 1, [reader, apdu...]`    | Forward a raw APDU to the card                           |
| `ModeOneTerminateSmartCardConnection(byte)`    | `1, 2, [reader]`             | Tear down the card session                               |

### `ExtendedRead` properties

- `byte Mode`
- `byte PReply`
- `byte[] PData`
- `byte Code` — always `0xB1`
- `ReadOnlySpan<byte> SecurityControlBlock()` — always `ReplyMessageWithDataSecurity`

### Relevant types and events

| Member                                        | Location                                           |
|-----------------------------------------------|----------------------------------------------------|
| `ExtendedWrite`                               | `OSDP.Net.Model.CommandData`                       |
| `ExtendedRead`                                | `OSDP.Net.Model.ReplyData`                         |
| `ControlPanel.ExtendedWriteData`              | `OSDP.Net.ControlPanel`                            |
| `ControlPanel.ExtendedReadReplyReceived`      | `OSDP.Net.ControlPanel`                            |
| `Device.HandleExtendedWrite`                  | `OSDP.Net.Device`                                  |
| `Device.EnqueuePollReply`                     | `OSDP.Net.Device`                                  |
