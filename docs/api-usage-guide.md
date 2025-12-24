# OSDP.Net API Usage Guide

This guide provides examples and best practices for using the OSDP.Net library to build Access Control Units (ACU) and Peripheral Devices (PD).

## Table of Contents

1. [Getting Started](#getting-started)
2. [Building an Access Control Unit (ACU)](#building-an-access-control-unit-acu)
3. [Building a Peripheral Device (PD)](#building-a-peripheral-device-pd)
4. [Connection Types](#connection-types)
5. [Security Configuration](#security-configuration)
6. [Command and Reply Handling](#command-and-reply-handling)
7. [Error Handling](#error-handling)
8. [Logging and Tracing](#logging-and-tracing)

## Getting Started

Install the NuGet package:

```bash
dotnet add package OSDP.Net
```

Basic using statements:

```csharp
using OSDP.Net;
using OSDP.Net.Connections;
using OSDP.Net.Model.CommandData;
using OSDP.Net.Model.ReplyData;
using OSDP.Net.Tracing;
using OSDP.Net.PanelCommands.DeviceDiscover;
using Microsoft.Extensions.Logging;
```

## Building an Access Control Unit (ACU)

An ACU manages and communicates with multiple peripheral devices.

### Basic ACU Setup

```csharp
// Create a control panel (ACU)
var controlPanel = new ControlPanel();

// Set up TCP connection to a device
var connection = new TcpClientOsdpConnection(IPAddress.Parse("192.168.1.100"), 3001);
var connectionId = controlPanel.StartConnection(connection);

// Add a device to the connection
controlPanel.AddDevice(connectionId, 0, true, true); // address=0, useSecureChannel=true, useCrc=true
```

### Sending Commands

```csharp
// Send an LED control command
var ledCommand = new ReaderLedControls
{
    LedControls = new[]
    {
        new ReaderLedControl
        {
            LedNumber = 0,
            TemporaryReaderControlCode = TemporaryReaderControlCode.SetTemporaryState,
            TemporaryOnTime = 5,
            TemporaryOffTime = 5,
            PermanentReaderControlCode = PermanentReaderControlCode.SetPermanentState,
            PermanentOnTime = 0,
            PermanentOffTime = 0,
            LedColor = LedColor.Red
        }
    }
};

await controlPanel.ReaderLedControl(connectionId, 0, ledCommand);

// Send a buzzer control command
var buzzerCommand = new ReaderBuzzerControl
{
    ToneCode = ToneCode.Default,
    OnTime = 3,
    OffTime = 1,
    RepeatCount = 2
};

await controlPanel.ReaderBuzzerControl(connectionId, 0, buzzerCommand);
```

### Reading Device Information

```csharp
// Get device identification
var deviceId = await controlPanel.IdReport(connectionId, 0);
Console.WriteLine($"Device: {deviceId.VendorCode}, Model: {deviceId.ModelNumber}");

// Get device capabilities  
var capabilities = await controlPanel.DeviceCapabilities(connectionId, 0);
foreach (var capability in capabilities.Capabilities)
{
    Console.WriteLine($"Function: {capability.Function}, Compliance: {capability.Compliance}");
}

// Check device status
var isOnline = controlPanel.IsOnline(connectionId, 0);
Console.WriteLine($"Device online: {isOnline}");
```

## Building a Peripheral Device (PD)

A PD responds to commands from an ACU and can report events.

### Basic PD Setup

```csharp
public class MyDevice : Device
{
    public MyDevice() : base(new DeviceConfiguration
    {
        Address = 0,
        RequireSecurity = true,
        SecurityKey = SecurityContext.DefaultKey
    })
    {
    }

    // Override command handlers as needed
    protected override PayloadData HandleIdReport()
    {
        return new DeviceIdentification
        {
            VendorCode = new byte[] { 0x12, 0x34, 0x56 },
            ModelNumber = 1,
            Version = 1,
            SerialNumber = 12345,
            FirmwareMajor = 1,
            FirmwareMinor = 0,
            FirmwareBuild = 1
        };
    }

    protected override PayloadData HandleDeviceCapabilities()
    {
        return new Model.ReplyData.DeviceCapabilities
        {
            Capabilities = new[]
            {
                new DeviceCapability
                {
                    Function = CapabilityFunction.ContactStatusMonitoring,
                    Compliance = 1,
                    NumberOf = 4
                }
            }
        };
    }
}

// Start the device
var device = new MyDevice();
var listener = new TcpConnectionListener(IPAddress.Any, 4000);
await device.StartListening(listener);
```

### Handling Commands

```csharp
public class MyDevice : Device
{
    protected override PayloadData HandleOutputControl(OutputControls commandPayload)
    {
        foreach (var control in commandPayload.Controls)
        {
            Console.WriteLine($"Setting output {control.OutputNumber} to {control.ControlCode}");
            // Implement your output control logic here
        }
        return new Ack();
    }

    protected override PayloadData HandleReaderLEDControl(ReaderLedControls commandPayload)
    {
        foreach (var led in commandPayload.LedControls)
        {
            Console.WriteLine($"Setting LED {led.LedNumber} to {led.LedColor}");
            // Implement your LED control logic here
        }
        return new Ack();
    }
}
```

## Connection Types

### TCP Connections

```csharp
// TCP Client (ACU connecting to PD)
var tcpClient = new TcpClientOsdpConnection(IPAddress.Parse("192.168.1.100"), 4000);

// TCP Server (ACU accepting connections from PDs)  
var tcpServer = new TcpServerOsdpConnection(IPAddress.Any, 4000);

// TCP Listener (PD accepting connections from ACU)
var tcpListener = new TcpConnectionListener(IPAddress.Any, 4000);
```

### Serial Connections

```csharp
// Serial connection
var serialConnection = new SerialPortOsdpConnection("COM1", 9600);

// Serial listener
var serialListener = new SerialPortConnectionListener("COM1", 9600);
```

## Security Configuration

### Setting Up Secure Communication

```csharp
// Device configuration with security
var deviceConfig = new DeviceConfiguration
{
    Address = 0,
    RequireSecurity = true,
    SecurityKey = new byte[] { 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 
                              0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F }
};

// Allow certain commands without security (if needed)
deviceConfig.AllowUnsecured = new[] { CommandType.Poll, CommandType.IdReport };
```

### Updating Security Keys

```csharp
// ACU can update device security key
var newKey = new byte[16]; // Your new 16-byte key
new Random().NextBytes(newKey);

var keyConfig = new EncryptionKeyConfiguration
{
    KeyData = newKey
};

await controlPanel.EncryptionKeySet(connectionId, deviceAddress, keyConfig);
```

## Command and Reply Handling

### Sending Multiple Commands

```csharp
// Send multiple commands in sequence
var tasks = new List<Task>
{
    controlPanel.ReaderBuzzerControl(connectionId, 0, buzzerCommand),
    controlPanel.ReaderLedControl(connectionId, 0, ledCommand),
    controlPanel.OutputControl(connectionId, 0, outputCommand)
};

await Task.WhenAll(tasks);
```

### Handling Events

```csharp
// Subscribe to connection status changes
controlPanel.ConnectionStatusChanged += (sender, args) =>
{
    Console.WriteLine($"Connection {args.ConnectionId} address {args.Address}: Connected={args.IsConnected}, Secure={args.IsSecureChannelEstablished}");
};

// Subscribe to specific reply types
controlPanel.RawCardDataReplyReceived += (sender, args) =>
{
    Console.WriteLine($"Card read from device {args.Address}: {BitConverter.ToString(args.RawCardData.Data)}");
};

controlPanel.KeypadReplyReceived += (sender, args) =>
{
    Console.WriteLine($"Keypad data from device {args.Address}: {args.KeypadData}");
};

controlPanel.NakReplyReceived += (sender, args) =>
{
    Console.WriteLine($"NAK from device {args.Address}: {args.Nak.ErrorCode}");
};
```

## Error Handling

### Exception Handling

```csharp
try
{
    var result = await controlPanel.IdReport(connectionId, 0);
}
catch (NackReplyException ex)
{
    Console.WriteLine($"Device returned NACK: {ex.ErrorCode}");
}
catch (TimeoutException)
{
    Console.WriteLine("Command timed out");
}
catch (InvalidPayloadException ex)
{
    Console.WriteLine($"Invalid payload: {ex.Message}");
}
```

### Checking Connection Status

```csharp
if (!controlPanel.IsOnline(connectionId, deviceAddress))
{
    Console.WriteLine("Device is not responding");
    // Handle offline device
}
```

## Logging and Tracing

### Setting Up Logging

```csharp
using Microsoft.Extensions.Logging;

// Use ILoggerFactory for logging (recommended)
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

var controlPanel = new ControlPanel(loggerFactory);

// For Device (PD) implementations
var device = new MyDevice(deviceConfig, loggerFactory);
```

> **Note:** The `ControlPanel(ILogger<ControlPanel>)` constructor is deprecated. Always use `ILoggerFactory` instead.

### Enabling Packet Tracing

```csharp
// Enable tracing to .osdpcap file
var connectionId = controlPanel.StartConnection(connection, TimeSpan.FromSeconds(5), isTracing: true);

// Custom tracing with TraceEntry callback
controlPanel.StartConnection(connection, TimeSpan.FromSeconds(5),
    traceEntry => Console.WriteLine($"[{traceEntry.Direction}] Address: {traceEntry.Address} Data: {BitConverter.ToString(traceEntry.Data)}"));
```

For more details on tracing capabilities, see the [Tracing Guide](tracing-guide.md).

## Best Practices

1. **Always use secure channels in production** - Set `RequireSecurity = true`
2. **Handle timeouts gracefully** - Network issues are common in access control systems
3. **Implement proper logging** - Essential for debugging and monitoring
4. **Use appropriate polling intervals** - Balance responsiveness with network overhead
5. **Validate device responses** - Check for NACK replies and handle appropriately
6. **Implement connection recovery** - Automatically reconnect when connections are lost
7. **Test with real hardware** - Simulators may not catch all edge cases

## Common Patterns

### Device Discovery

```csharp
// Discover device on one or more connections (e.g., different baud rates)
var connections = new IOsdpConnection[]
{
    new SerialPortOsdpConnection("COM1", 9600),
    new SerialPortOsdpConnection("COM1", 115200)
};

var options = new DiscoveryOptions
{
    ProgressCallback = result => Console.WriteLine($"Status: {result.Status}")
};

var discovery = await controlPanel.DiscoverDevice(connections, options);
if (discovery != null)
{
    Console.WriteLine($"Found device at address {discovery.Address}");
    Console.WriteLine($"Vendor: {BitConverter.ToString(discovery.Id.VendorCode)}");
    Console.WriteLine($"Uses default key: {discovery.UsesDefaultSecurityKey}");
}
```

### Periodic Status Checks

```csharp
// Periodic device health check
var timer = new Timer(async _ =>
{
    try
    {
        await controlPanel.LocalStatus(connectionId, deviceAddress);
    }
    catch
    {
        Console.WriteLine("Device health check failed");
    }
}, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
```

This guide covers the most common scenarios. For complete API documentation, refer to the XML documentation comments in the source code.