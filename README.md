# StickyTerm

Serial terminal with sticky COM port assignments for USB serial devices.

A Windows desktop application that keeps your USB serial devices on consistent COM ports. Discovers USB serial devices, lets you bind device identities (VID/PID/Serial) to preferred COM ports, and applies bindings automatically via registry writes.

[battlewithbytes.io/projects/stickyterm](https://battlewithbytes.io/projects/stickyterm)

## Features

### Built-in Serial Terminal
- Full-featured serial terminal with configurable baud rate, data bits, stop bits, parity, and flow control
- ANSI color code support
- Command history with up/down arrow keys
- Hex mode for sending/receiving raw bytes
- DTR/RTS control line toggles with CTS/DSR/CD status indicators

### Device Discovery
- Automatically discovers all COM port devices (USB CDC, USB-UART bridges, Bluetooth SPP, etc.)
- Displays device identity: VID, PID, USB serial number, instance path, friendly name, driver
- Detects common chip families: FTDI, CP210x, CH340/CH341, Prolific, RP2040, STM32 CDC, Arduino, etc.
- Visual indicator for device stability (green = has serial number, orange = unstable)
- Device aliases for friendly naming

### Sticky Port Assignments
- One-click "Track This Port" to keep a device on its current COM port
- Create rules to bind specific devices to preferred COM port numbers
- Multiple match types: VID+PID+Serial, VID+PID, Instance ID, Friendly Name
- Priority-based rule matching when multiple rules could apply
- Watch mode: automatically apply rules when devices are connected

### Additional Features
- System tray integration with minimize-to-tray
- Windows startup support via Task Scheduler (with admin privileges)
- Optional HTTP REST API for AI coding assistants to query port information
- Dark and light themes
- Import/export rules for backup or sharing
- Daily log files with configurable retention

## Requirements

- Windows 10 or Windows 11
- .NET 8.0 Runtime
- Administrator privileges (required for registry modifications)

## Building

```bash
dotnet build StickyTerm.sln
```

## Running Tests

```bash
dotnet test StickyTerm.Tests/StickyTerm.Tests.csproj
```

## Usage

1. **Scan**: Devices are discovered automatically on startup
2. **Select Device**: Choose a device from the Port Manager tab
3. **Track**: Click "Track This Port" to keep the device on its current COM port
4. **Terminal**: Use the Terminal tab for serial communication
5. **Watch Mode**: Always active - rules are applied automatically when devices connect

## Project Structure

```
StickyTerm/
  Models/           - Data models (ComDevice, PortRule, AppSettings, etc.)
  Services/         - Business logic services
  ViewModels/       - MVVM view models
  Views/            - XAML views and dialogs
  Converters/       - WPF value converters
  Themes/           - Dark and light theme resources
  Helpers/          - Utility classes
StickyTerm.Tests/   - xUnit test project
Installer/          - Inno Setup installer script
```

## License

MIT - see [LICENSE](LICENSE)
