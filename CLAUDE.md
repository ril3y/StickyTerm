# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Product Context

**StickyTerm** is a Windows desktop GUI for managing static COM port assignments. Discovers USB serial devices (CDC, USB-UART bridges, Bluetooth SPP), lets users bind device identities (VID/PID/Serial) to preferred COM ports, and applies bindings via registry writes. Requires admin privileges. Target users: embedded developers, hardware hackers, lab environments.

**Core workflow**: Scan -> Select device -> Choose COM -> Create rule -> Apply

## Build Commands

```bash
# Build the main application
dotnet build StickyTerm/StickyTerm.csproj

# Run the application (requires admin privileges)
dotnet run --project StickyTerm/StickyTerm.csproj

# Build entire solution (includes tests)
dotnet build StickyTerm.sln

# Run all tests
dotnet test StickyTerm.Tests/StickyTerm.Tests.csproj

# Run a single test by name
dotnet test StickyTerm.Tests/StickyTerm.Tests.csproj --filter "FullyQualifiedName~RuleServiceTests.FindMatchingRule"

# Run tests in a specific class
dotnet test StickyTerm.Tests/StickyTerm.Tests.csproj --filter "FullyQualifiedName~PersistenceServiceTests"
```

## Architecture Overview

This is a .NET 8 WPF application that manages persistent COM port assignments for USB serial devices. It requires administrator privileges to modify device registry settings.

### Key Architectural Patterns

**MVVM with CommunityToolkit.Mvvm**: ViewModels inherit from `ObservableObject`. Main UI logic is in `MainViewModel`, with `SerialTerminalViewModel` for terminal features.

**Manual Dependency Injection**: Services are constructed and wired in `App.xaml.cs` during `OnStartup()`. No DI container is used.

**Service-Oriented Design**: All business logic is encapsulated in services with interface/implementation pairs in `Services/`.

### Core Services

| Service | Purpose |
|---------|---------|
| `WmiDeviceEnumerationService` | Discovers COM ports via WMI, monitors device arrival/removal |
| `RegistryService` | Reads/writes COM port registry settings (HKLM) |
| `DeviceManagerService` | Enables/disables devices for restart after port change |
| `RuleService` | Rule matching engine with priority-based selection |
| `PersistenceService` | JSON storage to `%LocalAppData%\StickyTerm\` |
| `LoggingService` | In-memory + file-based logging with rotation |
| `ApiServerService` | Optional HTTP REST API (default port 27182) |
| `TrayIconService` | System tray integration |
| `StartupService` | Windows Task Scheduler integration for auto-start |
| `SerialTerminalService` | Serial port communication for built-in terminal |

### Data Models

- `ComDevice` - Physical COM port device with VID/PID/Serial/stability tracking, chip family detection (FTDI, CP210x, CH340, Prolific, RP2040, STM32, Arduino)
- `PortRule` - Port assignment rule with match type (VidPidSerial, VidPid, InstanceId, FriendlyName), priority-based matching
- `DeviceAlias` - User-defined friendly name for a device, matched by VID+PID+Serial
- `AppSettings` - All application configuration
- `ApplyResult` - Operation outcome with factory methods (Succeeded, Failed, NoChange, Conflict, DryRun)

### Rule Matching

Rules are matched by priority (highest wins). Match types in order of specificity:
1. **VID+PID+Serial** - Most stable, survives topology changes
2. **VID+PID** - For devices without serial numbers (less stable)
3. **Instance ID** - PnP device ID pattern with wildcards
4. **Friendly Name** - Device name pattern with wildcards

Devices without USB serial numbers are flagged as unstable.

### Application Entry Points

1. `Program.cs` - Custom Main() with early exception handling for MSIX scenarios
2. `App.xaml.cs` - Service initialization, single-instance enforcement, global exception handlers
3. `MainWindow.xaml.cs` - Async initialization, theme application, minimize-to-tray

### Special Build Requirements

The application requires administrator privileges for registry writes to `HKLM\SYSTEM\CurrentControlSet\Enum\<PNPDeviceID>\Device Parameters\PortName`. The csproj contains a custom MSBuild target `EmbedManifestInAppHost` that embeds the UAC manifest (`app.manifest`) into the executable using Windows SDK `mt.exe`.

### Test Framework

Tests use xUnit with Moq for mocking. Tests that need file I/O create temp directories and use reflection to inject custom data directories into `PersistenceService`.

## API Server

When enabled, provides REST endpoints for external tools to query COM port information:
- `GET /api/ports` - Connected ports with aliases
- `GET /api/devices` - All known devices
- `GET /api/rules` - Configured rules
- `GET /api/status` - Server health

See `docs/API.md` for full documentation.
