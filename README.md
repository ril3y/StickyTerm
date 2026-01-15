# COM Port Manager

A Windows desktop GUI application for managing static COM port assignments for USB serial devices.

## Features

### Device Discovery
- Automatically discovers all COM port devices (USB CDC, USB-UART bridges, Bluetooth SPP, etc.)
- Displays device identity: VID, PID, USB serial number, instance path, friendly name, driver
- Detects common chip families: FTDI, CP210x, CH340/CH341, Prolific, RP2040, STM32 CDC, Arduino, etc.
- Visual indicator for device stability (green = has serial number, orange = unstable)

### Rule-Based Port Binding
- Create rules to bind specific devices to preferred COM port numbers
- Multiple match types:
  - **VID+PID+Serial**: Most specific, stable across USB topology changes
  - **VID+PID**: For devices without serial numbers (less stable)
  - **Instance ID**: Match by PnP device ID pattern (supports wildcards)
  - **Friendly Name**: Match by device name pattern (supports wildcards)
- Priority-based rule matching when multiple rules could apply
- Warnings for devices without serial numbers

### Rule Application
- Apply rules manually or automatically on device connection
- Write PortName to device registry key
- Optional device restart (disable/enable) after port change
- Dry-run mode to preview changes without applying

### Watch Mode
- Monitor for device connection/disconnection events
- Automatically apply matching rules when devices are connected

### Data Management
- Rules and settings persisted to JSON files in %LocalAppData%\ComPortManager
- Import/export rules for backup or sharing
- Daily log files with configurable retention

## Requirements

- Windows 10 or Windows 11
- .NET 8.0 Runtime
- Administrator privileges (required for registry modifications)

## Building

```bash
dotnet build ComPortManager/ComPortManager.csproj
```

## Usage

1. **Scan**: Click "Scan" to discover all COM port devices
2. **Select Device**: Choose a device from the list to view details
3. **Create Rule**: Select a target COM port and click "Create Rule"
4. **Apply**: Click "Apply All" to apply all matching rules, or select a specific rule and device and click "Apply"
5. **Watch Mode**: Enable to auto-apply rules when devices connect

### Important Notes

- Devices without a USB serial number may not maintain stable COM port assignments
- After applying a rule, you may need to unplug/replug the device for the change to take effect
- Use "Watch Mode" for persistent environments where devices are frequently connected/disconnected

## Project Structure

```
ComPortManager/
  Models/           - Data models (ComDevice, PortRule, AppSettings, etc.)
  Services/         - Business logic services
    IDeviceEnumerationService.cs    - Device discovery interface
    WmiDeviceEnumerationService.cs  - WMI-based device enumeration
    IRegistryService.cs             - Registry access interface
    RegistryService.cs              - COM port registry operations
    IDeviceManagerService.cs        - Device enable/disable interface
    DeviceManagerService.cs         - SetupAPI-based device management
    IRuleService.cs                 - Rule management interface
    RuleService.cs                  - Rule matching and application
    IPersistenceService.cs          - Data persistence interface
    PersistenceService.cs           - JSON file storage
    ILoggingService.cs              - Logging interface
    LoggingService.cs               - File-based logging
  ViewModels/       - MVVM view models
  Views/            - XAML views
  Converters/       - WPF value converters
```

## License

MIT
