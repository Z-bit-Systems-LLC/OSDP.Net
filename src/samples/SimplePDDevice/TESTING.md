# Testing SimplePDDevice

## Manual Testing

### 1. Start the Device

```bash
cd /path/to/SimplePDDevice
dotnet run
```

Expected output:
```
Simple OSDP Peripheral Device
============================
info: SimplePDDevice.Program[0]
      Starting OSDP Peripheral Device on TCP port 4900
info: SimplePDDevice.Program[0]
      Device Address: 1
info: SimplePDDevice.Program[0]
      Security Required: False
info: SimplePDDevice.Program[0]
      Device is now listening for ACU connections...
info: SimplePDDevice.Program[0]
      Press 'q' to quit
[19:22:13] Waiting for ACU connection...
```

### 2. Test TCP Connection

In another terminal, test that the TCP port is listening:

```bash
# Test if port is open (should succeed)
nc -z localhost 4900 && echo "Port is open" || echo "Port is closed"

# Or using telnet
telnet localhost 4900
```

### 3. Connect with OSDP ACU

To fully test the device, you need an OSDP Access Control Unit (ACU). The device implements these handlers:

- **ID Report (0x61)** - Returns device identification
- **Device Capabilities (0x62)** - Returns supported capabilities
- **Communication Set (0x6E)** - Acknowledges communication changes  
- **Key Set (0x75)** - Acknowledges security key settings

### 4. Expected Behavior

When an ACU connects:
- Device status will change to "Device is connected to ACU"
- The device will respond to the four implemented commands
- All other commands will be handled by the base Device class

### 5. Configuration Testing

Test different configurations by modifying `appsettings.json`:

```json
{
  "OSDP": {
    "TcpPort": 5000,
    "DeviceAddress": 2,
    "RequireSecurity": true,
    "SecurityKey": "1122334455667788AABBCCDDEEFF0011"
  }
}
```

### 6. Build Verification

```bash
dotnet build
# Should succeed with no warnings or errors
```

### 7. Integration with Console App

The Console application in this project can act as an ACU. To test full integration:

1. Start SimplePDDevice: `dotnet run` (in SimplePDDevice directory)
2. In another terminal, start Console app as ACU with TCP connection to localhost:4900
3. The Console app should be able to discover and communicate with the SimplePDDevice

## Troubleshooting

- **Port already in use**: Change TcpPort in appsettings.json
- **Connection refused**: Ensure device is started before ACU connection
- **Build errors**: Ensure OSDP.Net project builds successfully first