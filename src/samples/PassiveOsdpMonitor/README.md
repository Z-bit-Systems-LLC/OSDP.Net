# Passive OSDP Monitor

A passive monitoring tool that captures OSDP packets from an existing ACU-PD serial communication without participating in or interfering with the communication.

## Features

- Passively monitors OSDP communication on a serial line
- Dual output format:
  - **OSDPCap JSON** (`.osdpcap`) - Machine-readable format for programmatic use
  - **Parsed Text** (`.txt`) - Human-readable format matching ACUConsole output
- Support for secure channel packets (with encryption key)
- Real-time packet parsing and logging
- Minimal hardware requirements

## Hardware Requirements

To passively monitor a serial line, you need a **Serial Tap/Y-Cable** setup:

- USB-to-Serial adapter (FTDI FT232, CH340, CP2102, etc.)
- Jumper wires or serial tap cable
- Examples:
  - USB-to-Serial adapter tapped into the serial line
  - Professional serial tap device
  - DIY Y-cable with isolation

### Connection Diagram

```
ACU ←──────────────→ PD
      (Serial Line)
           |
           └─→ Tap Point
                  |
                  ├─ GND ──→ [USB-Serial] GND
                  ├─ TX ───→ [USB-Serial] RX
                  └─ RX ───→ [USB-Serial] TX
                             [USB-Serial] USB → PC
```

**Important:**
- Connect both **RX and TX** lines to monitor bidirectional communication
- Connect **common ground** between all devices
- Do **NOT** connect control signals (RTS, DTR, etc.)

## Software Requirements

- .NET 8.0 SDK or later
- Serial port access

## Configuration

The monitor can be configured using `appsettings.json` (optional). If the file doesn't exist, default values are used.

Create `appsettings.json` in the same directory as the executable:

```json
{
  "PassiveOsdpMonitor": {
    "SerialPort": "COM3",
    "BaudRate": 9600,
    "OutputPath": "./captures",
    "OutputFilePrefix": "passive-capture",
    "SecurityKey": null
  }
}
```

### Configuration Options

- **SerialPort**: Serial port name (e.g., `COM3` on Windows, `/dev/ttyUSB0` on Linux)
- **BaudRate**: Communication speed (typically 9600 for OSDP)
- **OutputPath**: Directory to save capture files
- **OutputFilePrefix**: Prefix for capture filenames
- **SecurityKey**: Optional encryption key for secure channel packets (byte array)

### Secure Channel Support

The monitor supports decrypting secure channel communications:

- **Default Behavior** (`SecurityKey: null`): Uses the OSDP default key (SCBK-D: `30 31 32 33 34 35 36 37 38 39 3A 3B 3C 3D 3E 3F`)
- **Custom Key**: Provide a 16-byte array for custom secure channel keys

Example with custom key:

```json
{
  "PassiveOsdpMonitor": {
    "SerialPort": "COM3",
    "BaudRate": 9600,
    "SecurityKey": [64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79]
  }
}
```

**Note:** The security key must match the key used by the ACU-PD pair to successfully decrypt secure channelRemo packets.

## Usage

### Build and Run

```bash
cd src/samples/PassiveOsdpMonitor

# (Optional) Edit configuration
notepad appsettings.json  # Windows
nano appsettings.json     # Linux

# Build and run
dotnet build
dotnet run
```

### Publish as Single Executable

Create a standalone executable that doesn't require .NET runtime installation:

**Windows:**
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

**Linux:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

The executable will be in `bin/Release/net8.0/{runtime}/publish/`.

**Deployment:**
1. Copy the executable to your target machine
2. (Optional) Place `appsettings.json` in the same directory as the executable
3. Run the executable - it will use `appsettings.json` if present, otherwise defaults

### Example Output

```
Passive OSDP Monitor v1.0
========================
Serial Port: COM3
Baud Rate: 9600
Security Key: Using default OSDP key (SCBK-D)
Note: Specify SecurityKey in appsettings.json for custom keys
OSDPCap File: ./captures/passive-capture-20231202-143530.osdpcap
Parsed Text: ./captures/passive-capture-20231202-143530.txt

Monitoring... Press Ctrl+C to stop

Packets captured: 100
Packets captured: 200
...
^C
Stopping monitor...
Monitor stopped
Total packets captured: 347
Total bytes read: 18,234
```

## Output Formats

### Format 1: OSDPCap JSON (`.osdpcap`)

Machine-readable JSON lines format for archival and programmatic analysis:

```json
{"timeSec":"1733246730","timeNano":"123456700","io":"trace","data":"53-00-00-09-00-60-02-5A-8E","osdpTraceVersion":"1","osdpSource":"PassiveOsdpMonitor"}
{"timeSec":"1733246730","timeNano":"145678900","io":"trace","data":"53-80-00-08-00-40-00-90-3D","osdpTraceVersion":"1","osdpSource":"PassiveOsdpMonitor"}
```

All packets are marked with `"io":"trace"` since this is a passive observer.

### Format 2: Parsed Text (`.txt`)

Human-readable format matching ACUConsole output:

```
23-12-02 14:30:45.123 [ 0:00:00.200 ] ACU -> PD: osdp_POLL
    Address: 1 Sequence: 0

23-12-02 14:30:45.145 [ 0:00:00.022 ] PD -> ACU: osdp_ACK
    Address: 1 Sequence: 0

23-12-02 14:30:45.345 [ 0:00:00.200 ] ACU -> PD: osdp_LED
    Address: 1 Sequence: 1
    ReaderLedControl { ... }
```

## Performance Characteristics

| Baud Rate | Throughput | Packet Rate | CPU Usage | Memory | Disk I/O |
|-----------|------------|-------------|-----------|--------|----------|
| 9600      | ~960 B/s   | 30 pkt/s    | <3%       | <40 MB | Low      |
| 19200     | ~1,920 B/s | 60 pkt/s    | <4%       | <40 MB | Low      |
| 115200    | ~11,520 B/s| 350 pkt/s   | <7%       | <50 MB | Medium   |

**Disk Usage:**
- OSDPCap JSON: ~150-200 bytes per packet
- Parsed text: ~100-300 bytes per packet
- Total: ~250-500 bytes per packet

## Troubleshooting

### Serial Port Access Denied

On Linux, add your user to the `dialout` group:
```bash
sudo usermod -a -G dialout $USER
# Log out and back in
```

### No Packets Captured

- Verify serial port name is correct
- Check baud rate matches the ACU-PD communication
- Ensure serial tap is connected correctly
- Verify data is flowing on the line (use oscilloscope or logic analyzer)

### Corrupted/Invalid Packets

- Check ground connection between devices
- Verify baud rate matches exactly
- Ensure serial tap doesn't add too much capacitance to the line
- Try a shorter cable

## Architecture

```
     Serial Tap (Y-Cable)
            |
    [ACU] ←→ [PD]
            |
            ↓ (read-only)
    [Passive Monitor]
            ├─→ {timestamp}.osdpcap (JSON format)
            └─→ {timestamp}.txt (Parsed text)
```

**How it works:**
1. Open serial port in read-only mode
2. Read all bytes from the line continuously
3. Buffer and parse into complete OSDP packets
4. Parse packet using MessageSpy
5. Write to both OSDPCap JSON and parsed text formats simultaneously

## Technical Details

### Packet Parsing

The `MessageSpy` class (from OSDP.Net.Tracing) handles:
- Parsing raw bytes into structured packets
- Decrypting secure channel packets (if key provided)
- Extracting command/reply types and payload data
- Tracking secure channel state across packets

### Timestamp Synchronization

Both files use the **same timestamp** for each packet to enable correlation between raw and parsed data.

### Error Handling

If packet parsing fails (corrupted data, sync loss, etc.):
- OSDPCap file still gets the raw packet
- Parsed text file shows error with raw hex data
- Monitor continues capturing

## License

This tool is part of the OSDP.Net library.
