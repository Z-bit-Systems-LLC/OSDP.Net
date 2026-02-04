# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Memory Storage

The `/memory` directory contains local session memory for Claude Code:
- **Purpose**: Store recommendations, notes, and context between Claude Code sessions
- **Not committed**: This directory is git-ignored and exists only on your local machine
- **Update as needed**: Remove completed items, add new discoveries, track ongoing improvements
- **Files**: Markdown documents with implementation notes, future improvements, architectural decisions, etc.

## PowerShell
- **Always use PowerShell Core (`pwsh`)** instead of Windows PowerShell (`powershell`)
- Scripts in this repository use modern PowerShell features (e.g., multi-argument `Join-Path`) that require PowerShell Core
- Run scripts with: `pwsh -File script.ps1`

## Build Commands
- Build project: `dotnet build`
- Build with specific configuration: `dotnet build --configuration Release`

## Test Commands
- Run all tests: `dotnet test`
- Run a specific test: `dotnet test --filter "FullyQualifiedName=OSDP.Net.Tests.{TestClass}.{TestMethod}"`
- Run tests with specific configuration: `dotnet test --configuration Release`

## Code Inspection
- **IMPORTANT**: Azure pipeline runs ReSharper code inspection and will fail the build if there are any warnings or errors
- Always run ReSharper code inspection before committing changes
- Run inspection: `jb inspectcode OSDP.Net.sln --output=inspectcode-results.xml`
- The command will create a SARIF JSON report with inspection results
- Check counts (run with `pwsh`):
  - Errors: `(Select-String -Path inspectcode-results.xml -Pattern '"level": "error",' -SimpleMatch).Count` (must be 0)
  - Warnings: `(Select-String -Path inspectcode-results.xml -Pattern '"level": "warning",' -SimpleMatch).Count` (must be 0)
- View details of errors and warnings (run with `pwsh`):
```pwsh
$json = Get-Content inspectcode-results.xml -Raw | ConvertFrom-Json
$issues = $json.runs[0].results | Where-Object { $_.level -eq 'error' -or $_.level -eq 'warning' }
foreach ($issue in $issues) {
    Write-Host "$($issue.level.ToUpper()): $($issue.ruleId)" -ForegroundColor $(if($issue.level -eq 'error'){'Red'}else{'Yellow'})
    Write-Host "  Message: $($issue.message.text)"
    Write-Host "  File: $($issue.locations[0].physicalLocation.artifactLocation.uri)"
    Write-Host "  Line: $($issue.locations[0].physicalLocation.region.startLine)"
    Write-Host ""
}
```
- "note" level issues are style suggestions and don't block builds
- If JetBrains.ReSharper.GlobalTools is not installed, run: `dotnet tool install -g JetBrains.ReSharper.GlobalTools`
- Fix all warnings and errors before creating commits

## Code Style Guidelines
- Follow default ReSharper C# coding style conventions
- Maintain abbreviations in uppercase (ACU, LED, OSDP, PIN, PIV, UID, SCBK)
- Follow async/await patterns for asynchronous operations
- Use dependency injection for testability
- Follow Arrange-Act-Assert pattern in tests
- Implement proper exception handling with descriptive messages
- Avoid blocking event threads
- Use interfaces for abstraction (e.g., IOsdpConnection)
- New commands should follow the existing command/reply model pattern
- Place commands in appropriate namespaces (Model/CommandData or Model/ReplyData)

## Project Structure
- Core library in `/src/OSDP.Net`
- Tests in `/test/OSDP.Net.Tests`
- Console applications in `/src/ACUConsole` and `/src/PDConsole`
- Sample applications in `/src/samples`

## Documentation
- **API Usage Guide**: See `/docs/api-usage-guide.md` for examples of ACU and PD usage
- **Tracing Guide**: See `/docs/tracing-guide.md` for packet capture and debugging
- **Supported Commands**: See `/docs/supported_commands.md` for OSDP v2.2 implementation status

## Terminal GUI Development
- **Style Guide**: See `/docs/terminal-gui-style-guide.md` for comprehensive guidelines on creating dialogs and UI components
- **Console Applications**: PDConsole and ACUConsole use Terminal.Gui for interactive terminal interfaces
- **Dialog Standards**: All dialogs must follow the established patterns for layout, spacing, validation, and user experience
- **ComboBox Requirements**: All ComboBox instances MUST use `.ConfigureForOptimalUX()` extension and have minimum width of 30 characters
- **Dialog Organization**: Place dialogs in `{Console}/Dialogs/` and input models in `{Console}/Model/DialogInputs/`

## OSDP Implementation
- **Command Implementation Status**: See `/docs/supported_commands.md` for current implementation status of OSDP v2.2 commands and replies
- **Device (PD) Implementation**: The `Device` class in `/src/OSDP.Net/Device.cs` provides the base implementation for OSDP Peripheral Devices
- **Command Handlers**: All command handlers are virtual methods in the Device class that can be overridden by specific device implementations
- **Connection Architecture**:
  - Use `TcpClientOsdpConnection` for ACUs connecting to remote PDs
  - Use `TcpConnectionListener` + `TcpOsdpConnection` for PDs accepting ACU connections
  - Use `TcpServerOsdpConnection` for ACUs accepting device connections
  - Use `SerialPortConnectionListener` for serial-based PD implementations

## API Notes
- Use `ILoggerFactory` for logging (the `ILogger<ControlPanel>` constructor is deprecated)
- `Device.StartListening()` is async - always use `await`
- For device discovery, use `DiscoverDevice(IEnumerable<IOsdpConnection>, DiscoveryOptions)`
- Subscribe to specific reply events (e.g., `RawCardDataReplyReceived`, `KeypadReplyReceived`) rather than a generic reply handler

## Tracing and Debugging
- Use `OSDP.Net.Tracing` namespace for packet capture
- `TraceEntry` provides raw packet data with direction and address
- Support for `.osdpcap` file format for Wireshark-compatible captures
- Enable tracing via `StartConnection(connection, pollInterval, isTracing: true)`

## Domain-Specific Terms
- Maintain consistent terminology for domain-specific terms like APDU, INCITS, OSDP, osdpcap, rmac, Wiegand