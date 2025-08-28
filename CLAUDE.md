# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands
- Build project: `dotnet build`
- Build with specific configuration: `dotnet build --configuration Release`

## Test Commands
- Run all tests: `dotnet test`
- Run a specific test: `dotnet test --filter "FullyQualifiedName=OSDP.Net.Tests.{TestClass}.{TestMethod}"`
- Run tests with specific configuration: `dotnet test --configuration Release`

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