# OSDP.Net Tracing Guide

This guide covers the tracing and packet capture capabilities in OSDP.Net for debugging, monitoring, and analyzing OSDP communications.

## Table of Contents

1. [Overview](#overview)
2. [Enabling Tracing](#enabling-tracing)
3. [TraceEntry](#traceentry)
4. [Parsing Packets](#parsing-packets)
5. [Display Names](#display-names)
6. [File-Based Capture](#file-based-capture)
7. [Human-Readable Output](#human-readable-output)
8. [Testing with IPacket](#testing-with-ipacket)
9. [Passive Monitoring](#passive-monitoring)

## Overview

OSDP.Net provides comprehensive tracing capabilities through the `OSDP.Net.Tracing` namespace:

| Class                     | Purpose                                                                         |
|---------------------------|---------------------------------------------------------------------------------|
| `TraceEntry`              | Raw packet data with direction and device address                               |
| `MessageSpy`              | Parse raw bytes into structured `Packet` objects, tracking secure channel state |
| `PacketBuffer`            | Buffer streaming data and extract complete OSDP packets                         |
| `OSDPCaptureFileWriter`   | Write traces to `.osdpcap` files                                                |
| `ParsedTextWriter`        | Write human-readable packet descriptions                                        |
| `OSDPPacketTextFormatter` | Format packets as readable text                                                 |

```csharp
using OSDP.Net.Tracing;
using OSDP.Net.Messages;
using OSDP.Net.Model;
```

## Enabling Tracing

### Basic Tracing to File

```csharp
// Enable default file tracing (creates .osdpcap files)
var connectionId = controlPanel.StartConnection(connection,
    TimeSpan.FromMilliseconds(100),
    isTracing: true);
```

### Custom Trace Callback

```csharp
// Custom callback receives every packet
var connectionId = controlPanel.StartConnection(connection,
    TimeSpan.FromMilliseconds(100),
    traceEntry =>
    {
        Console.WriteLine($"[{traceEntry.Direction}] Address: {traceEntry.Address} Data: {BitConverter.ToString(traceEntry.Data)}");
    });
```

### Multiple Trace Handlers

```csharp
// Combine file capture with custom logging
var captureWriter = new OSDPCaptureFileWriter("capture.osdpcap", "MyApp");

var connectionId = controlPanel.StartConnection(connection,
    TimeSpan.FromMilliseconds(100),
    traceEntry =>
    {
        // Write to file
        captureWriter.WriteTrace(traceEntry);

        // Also log to console
        if (traceEntry.Address == 5)  // Filter by device address
        {
            Console.WriteLine($"Device 5: {traceEntry.Direction}");
        }
    });
```

## TraceEntry

`TraceEntry` is a struct containing raw packet data captured from the wire.

### Properties

| Property       | Type             | Description                                  |
|----------------|------------------|----------------------------------------------|
| `Direction`    | `TraceDirection` | `Input`, `Output`, or `Trace`                |
| `ConnectionId` | `Guid`           | Identifies the connection/bus                |
| `Data`         | `byte[]`         | Raw packet bytes (without SOM byte)          |
| `Address`      | `byte?`          | Device address extracted from packet (0-127) |

### Using the Address Property

The `Address` property extracts the device address from the packet data, automatically masking the reply bit (0x80):

```csharp
void HandleTrace(TraceEntry trace)
{
    // Address is automatically extracted and reply bit is masked
    if (trace.Address == 5)
    {
        Console.WriteLine("Packet for device 5");
    }

    // Address is null for empty or malformed packets
    if (trace.Address == null)
    {
        Console.WriteLine("Could not determine device address");
    }

    // Filter by multiple devices
    var monitoredDevices = new HashSet<byte> { 0, 1, 5 };
    if (trace.Address.HasValue && monitoredDevices.Contains(trace.Address.Value))
    {
        ProcessPacket(trace);
    }
}
```

### Determining Packet Type

```csharp
void AnalyzeTrace(TraceEntry trace)
{
    if (trace.Data == null || trace.Data.Length == 0)
        return;

    // Check if this is a command (ACU -> PD) or reply (PD -> ACU)
    bool isReply = (trace.Data[0] & 0x80) != 0;
    byte deviceAddress = trace.Address ?? 0;

    Console.WriteLine(isReply
        ? $"Reply from device {deviceAddress}"
        : $"Command to device {deviceAddress}");
}
```

## Parsing Packets

### Using MessageSpy

`MessageSpy` parses raw OSDP packets and tracks secure channel state for encrypted communications:

```csharp
// Create a MessageSpy (optionally with security key for encrypted packets)
var messageSpy = new MessageSpy();  // or: new MessageSpy(securityKey)

// Parse a packet
var packet = messageSpy.ParsePacket(traceEntry.Data);

Console.WriteLine($"Address: {packet.Address}");
Console.WriteLine($"Sequence: {packet.Sequence}");
Console.WriteLine($"Type: {packet.CommandType ?? packet.ReplyType}");
```

### Using TryParsePacket (Recommended)

The `TryParsePacket` method provides exception-free parsing:

```csharp
var messageSpy = new MessageSpy();

// Simple parsing
if (messageSpy.TryParsePacket(traceEntry.Data, out var packet))
{
    Console.WriteLine($"Parsed: {packet.CommandType?.GetDisplayName()}");
}
else
{
    Console.WriteLine("Failed to parse packet");
}

// With encryption key for secure channel decryption
byte[] securityKey = new byte[] { 0x30, 0x31, 0x32, /* ... */ };
var secureMessageSpy = new MessageSpy(securityKey);

if (secureMessageSpy.TryParsePacket(traceEntry.Data, out var securePacket))
{
    if (securePacket.IsPayloadDecrypted)
    {
        var payload = securePacket.ParsePayloadData();
        Console.WriteLine($"Payload: {payload}");
    }
}
```

### Accessing Packet Properties

```csharp
var messageSpy = new MessageSpy();
if (messageSpy.TryParsePacket(data, out var packet))
{
    // Basic properties
    Console.WriteLine($"Address: {packet.Address}");
    Console.WriteLine($"Sequence: {packet.Sequence}");
    Console.WriteLine($"Using CRC: {packet.IsUsingCrc}");

    // Command or Reply
    if (packet.CommandType.HasValue)
    {
        Console.WriteLine($"Command: {packet.CommandType.Value.GetDisplayName()}");
    }
    else if (packet.ReplyType.HasValue)
    {
        Console.WriteLine($"Reply: {packet.ReplyType.Value.GetDisplayName()}");
    }

    // Security status
    Console.WriteLine($"Secure: {packet.IsSecureMessage}");
    Console.WriteLine($"Default Key: {packet.IsUsingDefaultKey}");
    Console.WriteLine($"Decrypted: {packet.IsPayloadDecrypted}");

    // Raw data access
    ReadOnlySpan<byte> rawPayload = packet.RawPayloadData;
    ReadOnlySpan<byte> rawPacket = packet.RawData;

    // Parse payload into typed object
    if (packet.IsPayloadDecrypted)
    {
        var payload = packet.ParsePayloadData();
        switch (payload)
        {
            case DeviceIdentification id:
                Console.WriteLine($"Vendor: {BitConverter.ToString(id.VendorCode)}");
                break;
            case Nak nak:
                Console.WriteLine($"NAK: {nak.ErrorCode}");
                break;
        }
    }
}
```

## Display Names

The `GetDisplayName()` extension methods return OSDP protocol names:

```csharp
using OSDP.Net.Messages;

// Command types
CommandType.Poll.GetDisplayName();           // "osdp_POLL"
CommandType.LEDControl.GetDisplayName();     // "osdp_LED"
CommandType.BuzzerControl.GetDisplayName();  // "osdp_BUZ"
CommandType.FileTransfer.GetDisplayName();   // "osdp_FILETRANSFER"

// Reply types
ReplyType.Ack.GetDisplayName();              // "osdp_ACK"
ReplyType.Nak.GetDisplayName();              // "osdp_NAK"
ReplyType.PdIdReport.GetDisplayName();       // "osdp_PDID"
ReplyType.RawReaderData.GetDisplayName();    // "osdp_RAW"
```

### Using in Trace Output

```csharp
void LogPacket(Packet packet)
{
    string typeName = packet.CommandType?.GetDisplayName()
                   ?? packet.ReplyType?.GetDisplayName()
                   ?? "Unknown";

    string direction = packet.CommandType != null ? "CMD" : "RPY";

    Console.WriteLine($"[{direction}] {typeName} to/from address {packet.Address}");
}
```

## File-Based Capture

### Writing Capture Files

```csharp
using var writer = new OSDPCaptureFileWriter("trace.osdpcap", "MyApplication");

// Write individual traces
writer.WriteTrace(traceEntry);

// Write raw packet data
writer.WritePacket(packetBytes, TraceDirection.Output, DateTime.UtcNow);
```

### Reading Capture Files

```csharp
// Read and parse .osdpcap file
string json = File.ReadAllText("trace.osdpcap");
byte[] securityKey = null; // Optional: provide key for encrypted packets

var messageSpy = new MessageSpy(securityKey);
foreach (var entry in messageSpy.ParseCaptureFile(json))
{
    Console.WriteLine($"{entry.TimeStamp:HH:mm:ss.fff} [{entry.Direction}]");
    Console.WriteLine($"  Type: {entry.Packet.CommandType?.GetDisplayName() ?? entry.Packet.ReplyType?.GetDisplayName()}");
    Console.WriteLine($"  Address: {entry.Packet.Address}");
    Console.WriteLine($"  Source: {entry.Source}");
}
```

### Capture File Format

The `.osdpcap` format is JSON lines with the following structure:

```json
{"timeSec":"1689599213","timeNano":"141793300","io":"output","data":"53-00-08-00-04-60-3D-57","osdpTraceVersion":"1","osdpSource":"OSDP.Net"}
{"timeSec":"1689599213","timeNano":"245123400","io":"input","data":"53-80-07-00-05-40-FE-9C","osdpTraceVersion":"1","osdpSource":"OSDP.Net"}
```

Details about the `.osdpcap` can be found at [osdpcap format document](https://github.com/Security-Industry-Association/libosdp-conformance/blob/master/doc/doc-src/osdpcap-format.md).

## Human-Readable Output

### Using ParsedTextWriter

```csharp
var formatter = new OSDPPacketTextFormatter();
using var textWriter = new ParsedTextWriter("trace.txt", formatter);

// Write parsed packets
textWriter.WritePacket(packetBytes, DateTime.UtcNow);
```

### Sample Output

```
25-12-23 10:30:45.123 ACU -> PD: osdp_LED
    Address: 0 Sequence: 2 [Clear Text]
    Reader: 0, LED: 0, Color: Red

25-12-23 10:30:45.225 PD -> ACU: osdp_ACK
    Address: 0 Sequence: 2 [Clear Text]
```

### Custom Formatting

```csharp
public class MyPacketFormatter : IPacketTextFormatter
{
    public string FormatPacket(Packet packet, DateTime timestamp, TimeSpan? timeDelta = null)
    {
        var type = packet.CommandType?.GetDisplayName()
                ?? packet.ReplyType?.GetDisplayName()
                ?? "Unknown";

        return $"[{timestamp:HH:mm:ss}] Addr={packet.Address} {type}";
    }

    public string FormatError(byte[] rawData, DateTime timestamp, TimeSpan? timeDelta, string errorMessage)
    {
        return $"[{timestamp:HH:mm:ss}] ERROR: {errorMessage}";
    }
}
```

## Testing with IPacket

The `IPacket` interface enables mocking for unit tests:

```csharp
using Moq;
using OSDP.Net.Model;

[Test]
public void ProcessPacket_WithPollCommand_LogsCorrectly()
{
    // Arrange
    var mockPacket = new Mock<IPacket>();
    mockPacket.Setup(p => p.Address).Returns(5);
    mockPacket.Setup(p => p.CommandType).Returns(CommandType.Poll);
    mockPacket.Setup(p => p.ReplyType).Returns((ReplyType?)null);
    mockPacket.Setup(p => p.IsSecureMessage).Returns(false);

    var processor = new PacketProcessor();

    // Act
    processor.Process(mockPacket.Object);

    // Assert
    // ... verify expected behavior
}
```

### IPacket Properties

| Property             | Type                   | Description                     |
|----------------------|------------------------|---------------------------------|
| `Address`            | `byte`                 | Device address                  |
| `Sequence`           | `byte`                 | Message sequence number         |
| `CommandType`        | `CommandType?`         | Command type (null for replies) |
| `ReplyType`          | `ReplyType?`           | Reply type (null for commands)  |
| `IsUsingCrc`         | `bool`                 | True if CRC is used             |
| `IsPayloadDecrypted` | `bool`                 | True if payload was decrypted   |
| `IsSecureMessage`    | `bool`                 | True if sent via secure channel |
| `IsUsingDefaultKey`  | `bool`                 | True if using default SCBK      |
| `RawPayloadData`     | `ReadOnlyMemory<byte>` | Raw payload bytes               |
| `RawData`            | `ReadOnlyMemory<byte>` | Complete raw message            |
| `ParsePayloadData()` | `object?`              | Parsed payload object           |

## Passive Monitoring

For monitoring OSDP traffic without participating in communication:

```csharp
// See the PassiveOsdpMonitor sample for a complete example
var serialPort = new ReadOnlySerialPortOsdpConnection("COM3", 9600);
var captureWriter = new OSDPCaptureFileWriter("monitor.osdpcap", "PassiveMonitor");
var textWriter = new ParsedTextWriter("monitor.txt");

// PacketBuffer handles buffering and extracting complete packets from streaming data
var buffer = new PacketBuffer();
byte[] readBuffer = new byte[1024];

while (true)
{
    int bytesRead = await serialPort.ReadAsync(readBuffer, CancellationToken.None);
    if (bytesRead > 0)
    {
        buffer.Append(readBuffer, bytesRead);

        // Extract all complete packets from buffer
        while (buffer.TryExtractPacket(out byte[]? packet) && packet != null)
        {
            var timestamp = DateTime.Now;
            captureWriter.WritePacket(packet, TraceDirection.Trace);
            textWriter.WritePacket(packet, timestamp);
        }
    }
}
```

### PacketBuffer

`PacketBuffer` handles the challenge of extracting complete OSDP packets from a stream of bytes:

```csharp
var buffer = new PacketBuffer();

// Append data as it arrives (may be fragments of packets)
buffer.Append(data, bytesRead);

// Extract complete packets
while (buffer.TryExtractPacket(out byte[]? packet))
{
    // packet contains a complete OSDP message
    ProcessPacket(packet);
}

// Properties
int bytesInBuffer = buffer.Length;  // Current buffer size
buffer.Clear();                      // Clear all buffered data
```

## Best Practices

1. **Use TryParsePacket** - Avoid exceptions in high-throughput tracing scenarios
2. **Filter by Address** - Use `TraceEntry.Address` to filter packets for specific devices
3. **Provide Security Keys** - Pass the SCBK to `MessageSpy` when parsing encrypted packets
4. **Use PacketBuffer for streams** - When reading from serial/TCP, use `PacketBuffer` to handle fragmented data
5. **Dispose Writers** - Always dispose `OSDPCaptureFileWriter` and `ParsedTextWriter`
6. **Use GetDisplayName** - Display protocol names for better readability
7. **Mock with IPacket** - Use the interface for unit testing packet processors

## Related Resources

- [API Usage Guide](api-usage-guide.md) - General OSDP.Net usage
- [Supported Commands](supported_commands.md) - List of implemented commands
- [PassiveOsdpMonitor Sample](../src/samples/PassiveOsdpMonitor/) - Example passive monitoring application
