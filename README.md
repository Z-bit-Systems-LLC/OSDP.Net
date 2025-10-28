# OSDP.Net

[![Build Status](https://dev.azure.com/Z-bitSystems/OSDP.Net/_apis/build/status%2FZ-bit-Systems-LLC.OSDP.Net?branchName=main)](https://dev.azure.com/Z-bitSystems/OSDP.Net/_build/latest?definitionId=5&branchName=main)
[![NuGet](https://img.shields.io/nuget/v/OSDP.Net.svg?style=flat)](https://www.nuget.org/packages/OSDP.Net/)

OSDP.Net is a .NET implementation of the Open Supervised Device Protocol (OSDP). 
This protocol has been adopted by the Security Industry Association (SIA) to standardize access control hardware communication. 
Further information can be found at [SIA OSDP Homepage](https://www.securityindustry.org/industry-standards/open-supervised-device-protocol/).

## Important Announcement 10/14/2025

We have switched to trunk-based development. The `main` branch is now the default branch. Please submit pull requests against the `main` branch.

## Prerequisites

OSDP.Net supports the following .NET implementations:
- .NET Framework 4.6.2 and later
- NET 8.0 and later

## Getting Started

The OSDP.Net library provides a Nuget package to quickly add OSDP capability to a .NET project. 
You can install it using the NuGet Package Console window:

```shell
PM> Install-Package OSDP.Net
``` 

A control panel can be created and started with a few lines. 
Be sure to register events before starting the connection.

```c#
var panel = new ControlPanel();
panel.ConnectionStatusChanged += (sender, eventArgs) =>
{
    // NOTE: Avoid blocking the thread so the control panel can continue polling
    Task.Run(async () =>
    {
        // Handle connection change event
    });
};
Guid connectionId = panel.StartConnection(new SerialPortOsdpConnection(portName, baudRate));
```

Once the connection has started, add Peripheral Devices (PD).

```c#
panel.AddDevice(connectionId, address, useCrc, useSecureChannel, secureChannelKey);
```

The following code will install a PD with a unique Secure Channel key. The OSDP standard requires that setting the secure key can only occur while communications are secure.

```c#
panel.AddDevice(connectionId, address, useCrc, useSecureChannel); // connect using default SC key
bool successfulSet = await panel.EncryptionKeySet(connectionId, address, 
    new EncryptionKeyConfiguration(KeyType.SecureChannelBaseKey, uniqueKey));
```

The ControlPanel object can then be used to send a command to the PD.

```c#
var returnReplyData = await panel.OutputControl(connectionId, address, new OutputControls(new[]
{
    new OutputControl(outputNumber, activate
        ? OutputControlCode.PermanentStateOnAbortTimedOperation
        : OutputControlCode.PermanentStateOffAbortTimedOperation, 0)
});
```

The reader number parameter found in some commands is used for devices with multiple readers attached. If the device has a single reader, a value of zero should be used.
```c#
byte defaultReaderNumber = 0;
bool success = await panel.ReaderBuzzerControl(connectionId, address, 
    new ReaderBuzzerControl(defaultReaderNumber, ToneCode.Default, 2, 2, repeatNumber))
```

## Common ACU Usage Examples

### Reading Card Data
```c#
// Register for card read events
panel.CardRead += async (sender, eventArgs) =>
{
    await Task.Run(() =>
    {
        Console.WriteLine($"Card read from device {eventArgs.Address}");
        if (eventArgs.CardData is RawCardData rawData)
        {
            Console.WriteLine($"Raw card data: {BitConverter.ToString(rawData.Data)}");
        }
        else if (eventArgs.CardData is FormattedCardData formattedData)
        {
            Console.WriteLine($"Formatted card data: {formattedData.CardNumber}");
        }
    });
};
```

### Handling Device Events
```c#
// Monitor device status changes
panel.InputStatusReport += async (sender, eventArgs) =>
{
    await Task.Run(() =>
    {
        Console.WriteLine($"Input status changed on device {eventArgs.Address}");
        foreach (var input in eventArgs.InputStatuses)
        {
            Console.WriteLine($"Input {input.Number}: {(input.Active ? "Active" : "Inactive")}");
        }
    });
};

// Handle NAK responses
panel.NakReplyReceived += async (sender, eventArgs) =>
{
    await Task.Run(() =>
    {
        Console.WriteLine($"NAK received from device {eventArgs.Address}: {eventArgs.Nak.ErrorCode}");
    });
};
```

### Managing Multiple Devices
```c#
// Add multiple devices on the same connection
var devices = new[] { 0, 1, 2, 3 }; // Device addresses
foreach (var address in devices)
{
    panel.AddDevice(connectionId, address, useCrc: true, useSecureChannel: true);
}

// Send commands to all devices
foreach (var address in devices)
{
    await panel.ReaderLedControl(connectionId, address, new ReaderLedControls(new[]
    {
        new ReaderLedControl(0, 0, LedColor.Green, LedColor.Black, 
            30, 30, PermanentControlCode.SetPermanentState)
    }));
}
```

### Error Handling
```c#
try
{
    var deviceId = await panel.IdReport(connectionId, address);
    Console.WriteLine($"Device ID: {deviceId.VendorCode:X}-{deviceId.ModelNumber}-{deviceId.Version}");
}
catch (TimeoutException)
{
    Console.WriteLine("Device communication timeout");
}
catch (Exception ex)
{
    Console.WriteLine($"Error communicating with device: {ex.Message}");
}
```

## Custom Communication Implementations

OSDP.Net is able to plug in different methods of communications beyond what is included with the default package. 
It simply requires the installation of a new NuGet package. The code needs to be updated by using its implementation of the IOsdpConnection interface when starting a connection for the ControlPanel.

- **SerialPortStream**
  - Author: Fredrik Hall 
  - Links: [GitHub](https://github.com/hallsbyra/OSDP.Net.SerialPortStreamOsdpConnection) [Nuget](https://www.nuget.org/packages/OSDP.Net.SerialPortStreamOsdpConnection/)
  - Notes: Requires compilation of native libraries for non-Windows platforms

## Test Console

**Current Version 5.0.22-beta released October 28th, 2025**

There are compiled versions of the test console applications for all the major platforms available for download. 
They have all the required assemblies included to run as a self-contained executable. 

- ACU Console
  - [ARM64 Linux](https://www.dropbox.com/scl/fi/9io2ot3xi0w2213fx20hw/ACUConsole?rlkey=p1zaev2gsmhxsrddiveh9j20x&st=dnxuvdt8&dl=1)
  - [x64 Linux](https://www.dropbox.com/scl/fi/fnpteprkr9rjmr1mm653f/ACUConsole?rlkey=c1txkn10ak0a71numayd8uel7&st=8tt2rg46&dl=1)
  - [ARM64 macOS](https://www.dropbox.com/scl/fi/adks520hl6urybfr1ev85/ACUConsole?rlkey=ph2fiaq0352rdl1yhynqvbgq2&st=5zmrjd5t&dl=1)
  - [x64 Windows](https://www.dropbox.com/scl/fi/uraj72ufisky91k1xdvtz/ACUConsole.exe?rlkey=f7xvnd34mx79xfthxgnpdzd4j&st=zvqh9p1o&dl=1) 
- PD Console (Beta)
  - [ARM64 Linux](https://www.dropbox.com/scl/fi/wpa167qd4njrz1zmfutqe/PDConsole?rlkey=qe8uot0op8owtgnemji980y7s&st=fp3fw4dc&dl=1)
  - [x64 Linux](https://www.dropbox.com/scl/fi/xa5kbaxm6226uah7faryg/PDConsole?rlkey=ibwa6bjory8q1tl0qyj4km7kl&st=4zpzb34b&dl=1)
  - [ARM64 macOS](https://www.dropbox.com/scl/fi/nspczsnbhmv6ygtbgfvxw/PDConsole?rlkey=685ohfx01ac1o6jz29hp585cw&st=vgpy6e8z&dl=1)
  - [x64 Windows](https://www.dropbox.com/scl/fi/5wksm69vs4o4s7kfsjiz4/PDConsole.exe?rlkey=1jcbtnq3nj5wqehtjkiyw3tf8&st=161okkhr&dl=1)

NOTE: First, determine the COM port identifier of the 485 bus connected to the computer.
This will need to be entered when starting the connection.
Be sure to save configuration before exiting.

### PuTTY SSH Sessions

If you experience rendering issues (blank screen) when running PDConsole or ACUConsole through a PuTTY SSH session, set the terminal type to support 256 colors:

```bash
export TERM=xterm-256color
```

For more information, see the [Terminal.Gui PuTTY discussion](https://github.com/gui-cs/Terminal.Gui/discussions/1455).

## Documentation 

- [PowerShell Support](docs/powershell.md)
- [Supported Commands and Replies](docs/supported_commands.md)

## Contributing

The current goal is to properly support all the commands and replies outlined in OSDP v2.2 standard. 
The document that outlines the specific of the standard can be found on the [SIA website](https://mysia.securityindustry.org/ProductCatalog/Product.aspx?ID=16773). Contact me through my consulting company [Z-bit System, LLC](https://z-bitco.com) if you're interested in collaborating on the OSDP.Net library.
