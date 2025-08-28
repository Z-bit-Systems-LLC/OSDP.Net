# OSDP.Net Public API Checklist

This checklist ensures the correct classes and methods remain publicly accessible for NuGet consumers.

## ✅ Required Public Classes

### Core API
- [ ] `ControlPanel` - Main ACU class
- [ ] `Device` - Main PD base class  
- [ ] `DeviceConfiguration` - Device configuration
- [ ] `DeviceComSetUpdatedEventArgs` - Event arguments

### Connection Types
- [ ] `IOsdpConnection` - Connection interface
- [ ] `IOsdpConnectionListener` - Connection listener interface
- [ ] `TcpClientOsdpConnection` - TCP client connection
- [ ] `TcpServerOsdpConnection` - TCP server connection
- [ ] `SerialPortOsdpConnection` - Serial port connection
- [ ] `TcpConnectionListener` - TCP connection listener
- [ ] `SerialPortConnectionListener` - Serial connection listener

### Exception Types  
- [ ] `OSDPNetException` - Base exception
- [ ] `NackReplyException` - NACK reply exception
- [ ] `InvalidPayloadException` - Invalid payload exception
- [ ] `SecureChannelRequired` - Security requirement exception

### Enums
- [ ] `CommandType` - OSDP command types
- [ ] `ReplyType` - OSDP reply types
- [ ] `MessageType` - Message type enum
- [ ] `BiometricFormat` - Biometric data formats
- [ ] `BiometricType` - Biometric types
- [ ] `CapabilityFunction` - Device capability functions
- [ ] `OutputControlCode` - Output control codes
- [ ] All LED, buzzer, and control enums

### Command Data Models
- [ ] `CommandData` - Base command class
- [ ] All classes in `OSDP.Net.Model.CommandData` namespace
- [ ] `OutputControls`, `ReaderLedControls`, `ReaderBuzzerControl`, etc.

### Reply Data Models  
- [ ] `PayloadData` - Base payload class
- [ ] All classes in `OSDP.Net.Model.ReplyData` namespace
- [ ] `DeviceIdentification`, `DeviceCapabilities`, `Ack`, `Nak`, etc.

### Discovery System
- [ ] `DiscoveryOptions` - Discovery configuration
- [ ] `DiscoveryResult` - Discovery results
- [ ] `DiscoveryStatus` - Discovery status enum
- [ ] Related exception types

### Utilities & Extensions
- [ ] `BinaryExtensions` - Binary utility methods
- [ ] `SecurityContext` - Security utilities

## ❌ Internal Implementation (Should NOT be public)

- [ ] `DeviceProxy` - Internal device proxy
- [ ] `Bus` - Internal message bus
- [ ] `IncomingMessage` - Internal message handling
- [ ] `OutgoingMessage` - Internal message handling  
- [ ] `ReplyTracker` - Internal reply tracking
- [ ] `MessageSpy` - Internal message tracing

## 🔍 Validation Commands

### Build Test
```bash
dotnet build src/OSDP.Net/OSDP.Net.csproj --verbosity quiet
```

### API Validation
```powershell
# Generate current API baseline
./scripts/generate-api-baseline.ps1

# Check current API count and validate
./scripts/validate-api.ps1
```

### Manual Inspection
```csharp
// Test key public APIs are accessible
var controlPanel = new OSDP.Net.ControlPanel();
var config = new OSDP.Net.DeviceConfiguration();
var connection = new OSDP.Net.Connections.TcpClientOsdpConnection(IPAddress.Any, 4000);
```

## 📝 Review Process

1. **Pre-Release**: Run validation scripts
2. **API Changes**: Document in release notes
3. **Breaking Changes**: Increment major version
4. **New APIs**: Add to this checklist

## 🔗 References

- [API Usage Guide](api-usage-guide.md)
- [Supported Commands](../docs/supported_commands.md)
- [Project Instructions](../CLAUDE.md)