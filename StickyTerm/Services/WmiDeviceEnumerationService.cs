using System.Management;
using System.Text.RegularExpressions;
using StickyTerm.Models;
using Microsoft.Win32;

namespace StickyTerm.Services;

/// <summary>
/// Device enumeration service using WMI.
/// </summary>
public partial class WmiDeviceEnumerationService : IDeviceEnumerationService, IDisposable
{
    private readonly ILoggingService _logger;
    private ManagementEventWatcher? _arrivalWatcher;
    private ManagementEventWatcher? _removalWatcher;
    private bool _isWatching;
    private readonly object _watchLock = new();

    public event EventHandler<ComDevice>? DeviceArrived;
    public event EventHandler<ComDevice>? DeviceRemoved;

    public WmiDeviceEnumerationService(ILoggingService logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<ComDevice>> DiscoverDevicesAsync()
    {
        return await Task.Run(() => DiscoverDevices());
    }

    private List<ComDevice> DiscoverDevices()
    {
        var devices = new List<ComDevice>();
        var seenComPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Query Win32_PnPEntity for serial ports
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE ClassGuid='{4d36e978-e325-11ce-bfc1-08002be10318}'");

            foreach (ManagementObject obj in searcher.Get())
            {
                try
                {
                    var device = ParseDevice(obj);
                    if (device != null)
                    {
                        // Skip duplicate COM ports (can happen with ghost devices or multiple PnP entries)
                        if (seenComPorts.Contains(device.ComPort))
                        {
                            continue;
                        }

                        seenComPorts.Add(device.ComPort);
                        devices.Add(device);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogEntry.Warning($"Failed to parse device: {ex.Message}"));
                }
            }

            // Also try Win32_SerialPort for additional info
            EnrichFromSerialPort(devices);
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to enumerate devices: {ex.Message}", ex.ToString()));
        }

        return devices;
    }

    private ComDevice? ParseDevice(ManagementObject obj)
    {
        var pnpDeviceId = obj["PNPDeviceID"]?.ToString() ?? string.Empty;
        var name = obj["Name"]?.ToString() ?? string.Empty;
        var caption = obj["Caption"]?.ToString() ?? name;

        // Extract COM port from name (e.g., "USB Serial Port (COM3)")
        var comPort = ExtractComPort(caption);
        if (string.IsNullOrEmpty(comPort))
        {
            // Try to get from registry
            comPort = GetComPortFromRegistry(pnpDeviceId);
        }

        if (string.IsNullOrEmpty(comPort))
        {
            return null; // Not a valid COM device
        }

        var device = new ComDevice
        {
            ComPort = comPort,
            FriendlyName = caption.Replace($" ({comPort})", "").Trim(),
            PnpDeviceId = pnpDeviceId,
            DeviceClass = obj["PNPClass"]?.ToString() ?? "Ports",
            Manufacturer = obj["Manufacturer"]?.ToString() ?? string.Empty,
            Driver = obj["Service"]?.ToString() ?? string.Empty,
            IsPresent = obj["Status"]?.ToString() == "OK"
        };

        // Parse VID/PID/Serial from PnPDeviceID
        ParseUsbIdentifiers(pnpDeviceId, device);

        // Determine device type
        device.DeviceType = DetermineDeviceType(pnpDeviceId, device.Driver);

        // Get registry path
        device.RegistryKeyPath = GetRegistryKeyPath(pnpDeviceId);

        // Set HasSerialNumber flag
        device.HasSerialNumber = !string.IsNullOrEmpty(device.SerialNumber);

        return device;
    }

    private void ParseUsbIdentifiers(string pnpDeviceId, ComDevice device)
    {
        if (string.IsNullOrEmpty(pnpDeviceId))
            return;

        // USB\VID_1234&PID_5678\SERIALNUMBER
        // USB\VID_1234&PID_5678&MI_00\7&12345678&0&0000
        var vidMatch = VidRegex().Match(pnpDeviceId);
        var pidMatch = PidRegex().Match(pnpDeviceId);

        if (vidMatch.Success)
            device.Vid = vidMatch.Groups[1].Value.ToUpperInvariant();

        if (pidMatch.Success)
            device.Pid = pidMatch.Groups[1].Value.ToUpperInvariant();

        // Extract serial number - it's typically the last part after the second backslash
        // but only if it doesn't look like an instance ID (containing & or starting with numbers)
        var parts = pnpDeviceId.Split('\\');
        if (parts.Length >= 3)
        {
            var lastPart = parts[^1];
            // If it contains & it's likely an instance ID, not a serial
            if (!lastPart.Contains('&') && !InstanceIdPattern().IsMatch(lastPart))
            {
                // Check if it looks like a real serial number (not just numbers)
                if (lastPart.Length >= 4)
                {
                    device.SerialNumber = lastPart;
                }
            }
        }

        device.InstancePath = pnpDeviceId;
    }

    private static string DetermineDeviceType(string pnpDeviceId, string driver)
    {
        var id = pnpDeviceId.ToUpperInvariant();
        var drv = driver.ToUpperInvariant();

        // Check common chip families
        if (id.Contains("VID_0403")) return "FTDI";
        if (id.Contains("VID_10C4")) return "CP210x (Silicon Labs)";
        if (id.Contains("VID_1A86")) return "CH340/CH341";
        if (id.Contains("VID_067B")) return "Prolific";
        if (id.Contains("VID_2E8A")) return "RP2040 (Raspberry Pi)";
        if (id.Contains("VID_0483")) return "STM32 CDC";
        if (id.Contains("VID_239A")) return "Adafruit";
        if (id.Contains("VID_2341")) return "Arduino";
        if (id.Contains("VID_1B4F")) return "SparkFun";
        if (id.Contains("VID_16C0")) return "Teensy";

        // Check by driver
        if (drv.Contains("FTDIBUS") || drv.Contains("FTSER")) return "FTDI";
        if (drv.Contains("SILABSER")) return "CP210x (Silicon Labs)";
        if (drv.Contains("CH341SER")) return "CH340/CH341";
        if (drv.Contains("SER2PL")) return "Prolific";
        if (drv.Contains("USBSER")) return "USB CDC ACM";

        // Check bus type
        if (id.StartsWith("BTHENUM")) return "Bluetooth SPP";
        if (id.StartsWith("USB")) return "USB Serial";
        if (id.StartsWith("ACPI") || id.StartsWith("PCI")) return "Native COM Port";

        return "Unknown";
    }

    private static string ExtractComPort(string name)
    {
        var match = ComPortRegex().Match(name);
        return match.Success ? match.Value : string.Empty;
    }

    private static string GetComPortFromRegistry(string pnpDeviceId)
    {
        if (string.IsNullOrEmpty(pnpDeviceId))
            return string.Empty;

        try
        {
            var keyPath = $@"SYSTEM\CurrentControlSet\Enum\{pnpDeviceId}\Device Parameters";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue("PortName")?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetRegistryKeyPath(string pnpDeviceId)
    {
        if (string.IsNullOrEmpty(pnpDeviceId))
            return string.Empty;

        return $@"HKLM\SYSTEM\CurrentControlSet\Enum\{pnpDeviceId}\Device Parameters";
    }

    private void EnrichFromSerialPort(List<ComDevice> devices)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SerialPort");
            var serialPorts = searcher.Get().Cast<ManagementObject>().ToList();

            foreach (var device in devices)
            {
                var serialPort = serialPorts.FirstOrDefault(sp =>
                    sp["DeviceID"]?.ToString() == device.ComPort);

                if (serialPort != null)
                {
                    if (string.IsNullOrEmpty(device.Manufacturer))
                        device.Manufacturer = serialPort["ProviderType"]?.ToString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Win32_SerialPort may not have all devices, ignore errors
        }
    }

    public async Task<IReadOnlyList<string>> GetUsedComPortsAsync()
    {
        return await Task.Run(() =>
        {
            var usedPorts = new List<string>();

            try
            {
                // Get from COM Name Arbiter registry
                using var arbiterKey = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\COM Name Arbiter");
                if (arbiterKey != null)
                {
                    var comDb = arbiterKey.GetValue("ComDB") as byte[];
                    if (comDb != null)
                    {
                        for (int i = 0; i < comDb.Length * 8; i++)
                        {
                            int byteIndex = i / 8;
                            int bitIndex = i % 8;
                            if ((comDb[byteIndex] & (1 << bitIndex)) != 0)
                            {
                                usedPorts.Add($"COM{i + 1}");
                            }
                        }
                    }
                }

                // Also check currently enumerated devices
                var devices = DiscoverDevices();
                foreach (var device in devices)
                {
                    if (!string.IsNullOrEmpty(device.ComPort) && !usedPorts.Contains(device.ComPort))
                    {
                        usedPorts.Add(device.ComPort);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogEntry.Warning($"Failed to get used COM ports: {ex.Message}"));
            }

            return usedPorts;
        });
    }

    public async Task<IReadOnlyList<int>> GetAvailableComPortNumbersAsync(int startFrom = 1, int count = 20)
    {
        var usedPorts = await GetUsedComPortsAsync();
        var usedNumbers = usedPorts
            .Select(p => int.TryParse(p.Replace("COM", ""), out var n) ? n : 0)
            .Where(n => n > 0)
            .ToHashSet();

        var available = new List<int>();
        for (int i = startFrom; available.Count < count && i < 256; i++)
        {
            if (!usedNumbers.Contains(i))
            {
                available.Add(i);
            }
        }

        return available;
    }

    public void StartWatching()
    {
        lock (_watchLock)
        {
            if (_isWatching)
                return;

            try
            {
                // Watch for device arrivals
                _arrivalWatcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity' AND TargetInstance.ClassGuid='{4d36e978-e325-11ce-bfc1-08002be10318}'"));
                _arrivalWatcher.EventArrived += OnDeviceArrived;
                _arrivalWatcher.Start();

                // Watch for device removals
                _removalWatcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity' AND TargetInstance.ClassGuid='{4d36e978-e325-11ce-bfc1-08002be10318}'"));
                _removalWatcher.EventArrived += OnDeviceRemoved;
                _removalWatcher.Start();

                _isWatching = true;
                _logger.Log(LogEntry.Info("Device watching started"));
            }
            catch (Exception ex)
            {
                _logger.Log(LogEntry.Error($"Failed to start device watching: {ex.Message}"));
            }
        }
    }

    public void StopWatching()
    {
        lock (_watchLock)
        {
            if (!_isWatching)
                return;

            try
            {
                _arrivalWatcher?.Stop();
                _arrivalWatcher?.Dispose();
                _arrivalWatcher = null;

                _removalWatcher?.Stop();
                _removalWatcher?.Dispose();
                _removalWatcher = null;

                _isWatching = false;
                _logger.Log(LogEntry.Info("Device watching stopped"));
            }
            catch (Exception ex)
            {
                _logger.Log(LogEntry.Warning($"Error stopping device watching: {ex.Message}"));
            }
        }
    }

    private void OnDeviceArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var device = ParseDevice((ManagementObject)targetInstance);
            if (device != null)
            {
                _logger.Log(LogEntry.DeviceArrival(device.FriendlyName, device.ComPort));
                DeviceArrived?.Invoke(this, device);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Warning($"Error processing device arrival: {ex.Message}"));
        }
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var device = ParseDevice((ManagementObject)targetInstance);
            if (device != null)
            {
                _logger.Log(LogEntry.DeviceRemoval(device.FriendlyName, device.ComPort));
                DeviceRemoved?.Invoke(this, device);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Warning($"Error processing device removal: {ex.Message}"));
        }
    }

    public void Dispose()
    {
        StopWatching();
        GC.SuppressFinalize(this);
    }

    [GeneratedRegex(@"VID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidRegex();

    [GeneratedRegex(@"PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex PidRegex();

    [GeneratedRegex(@"COM\d+", RegexOptions.IgnoreCase)]
    private static partial Regex ComPortRegex();

    [GeneratedRegex(@"^\d+&[0-9A-Fa-f]+&\d+&\d+$")]
    private static partial Regex InstanceIdPattern();
}
