# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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
- Check counts:
  - Errors: `(Select-String -Path inspectcode-results.xml -Pattern '"level": "error",' -SimpleMatch).Count` (must be 0)
  - Warnings: `(Select-String -Path inspectcode-results.xml -Pattern '"level": "warning",' -SimpleMatch).Count` (must be 0)
- View details of errors and warnings:
```powershell
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
- Tests in `/src/OSDP.Net.Tests`
- Console application in `/src/Console`
- Sample applications in `/src/samples`

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
  - Use `TcpConnectionListener` + `TcpOsdpConnection` for PDs accepting ACU connections
  - Use `TcpServerOsdpConnection` for ACUs accepting device connections
  - Use `SerialPortConnectionListener` for serial-based PD implementations

## Domain-Specific Terms
- Maintain consistent terminology for domain-specific terms like APDU, INCITS, OSDP, osdpcap, rmac, Wiegand