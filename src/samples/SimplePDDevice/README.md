# SimplePDDevice

A minimal OSDP Peripheral Device (PD) implementation that demonstrates the essential handlers for OSDP communication.

## Features

This simplified device implements only the core handlers that are guaranteed to work:

- **HandleIdReport()** - Returns basic device identification
- **HandleDeviceCapabilities()** - Returns supported device capabilities  
- **HandleCommunicationSet()** - Acknowledges communication settings changes
- **HandleKeySettings()** - Acknowledges security key configuration

All other commands are handled by the base Device class with default behavior.

## Configuration

The device can be configured via `appsettings.json`:

```json
{
  "OSDP": {
    "TcpPort": 4900,
    "DeviceAddress": 1,
    "RequireSecurity": false,
    "SecurityKey": "0011223344556677889900AABBCCDDEEFF"
  }
}
```

## Running

```bash
dotnet run
```

The device will:
- Listen on TCP port 4900 (configurable)
- Use device address 1 (configurable)  
- Run without security by default (configurable)
- Display connection status every second
- Automatically stop after 30 seconds (for demo purposes)

## Usage

This device is designed to be connected to by an OSDP Access Control Unit (ACU). Once an ACU connects, it can:

- Request device identification
- Query device capabilities
- Send communication configuration commands
- Send security key configuration commands

## Building

```bash
dotnet build
```

## Purpose

This implementation serves as a starting point for OSDP device development, focusing on simplicity and the essential functionality that works reliably. Additional features can be added incrementally as needed.