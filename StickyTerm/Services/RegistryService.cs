using System.Text.Json;
using StickyTerm.Models;
using Microsoft.Win32;

namespace StickyTerm.Services;

/// <summary>
/// Registry service implementation for COM port settings.
/// </summary>
public class RegistryService : IRegistryService
{
    private readonly ILoggingService _logger;
    private const string EnumBasePath = @"SYSTEM\CurrentControlSet\Enum";
    private const string ComArbiterPath = @"SYSTEM\CurrentControlSet\Control\COM Name Arbiter";

    public RegistryService(ILoggingService logger)
    {
        _logger = logger;
    }

    public string? GetPortName(string pnpDeviceId)
    {
        if (string.IsNullOrEmpty(pnpDeviceId))
            return null;

        try
        {
            var keyPath = $@"{EnumBasePath}\{pnpDeviceId}\Device Parameters";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue("PortName")?.ToString();
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Warning($"Failed to read PortName for {pnpDeviceId}: {ex.Message}"));
            return null;
        }
    }

    public RegistrySetResult SetPortName(string pnpDeviceId, string portName, bool dryRun = false)
    {
        if (string.IsNullOrEmpty(pnpDeviceId) || string.IsNullOrEmpty(portName))
        {
            _logger.Log(LogEntry.Error($"SetPortName called with invalid parameters: pnpDeviceId={pnpDeviceId}, portName={portName}"));
            return RegistrySetResult.Failed;
        }

        var keyPath = $@"{EnumBasePath}\{pnpDeviceId}\Device Parameters";
        _logger.Log(LogEntry.Info($"Attempting to set PortName={portName} at HKLM\\{keyPath}"));

        if (dryRun)
        {
            _logger.Log(LogEntry.Info($"[DRY RUN] Would set PortName={portName} at {keyPath}"));
            return RegistrySetResult.Success;
        }

        try
        {
            // First try to open read-only to check if key exists
            using var readKey = Registry.LocalMachine.OpenSubKey(keyPath, writable: false);
            if (readKey == null)
            {
                _logger.Log(LogEntry.Error($"Registry key not found: HKLM\\{keyPath}"));
                return RegistrySetResult.KeyNotFound;
            }

            var oldValue = readKey.GetValue("PortName")?.ToString();
            _logger.Log(LogEntry.Info($"Current PortName value: {oldValue ?? "(null)"}"));

            // Now try to open writable
            using var writeKey = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
            if (writeKey == null)
            {
                _logger.Log(LogEntry.Error($"Cannot open registry key for writing: HKLM\\{keyPath}"));
                return RegistrySetResult.AccessDenied;
            }

            writeKey.SetValue("PortName", portName, RegistryValueKind.String);

            _logger.Log(LogEntry.Info(
                $"Successfully set PortName={portName} (was {oldValue}) at {keyPath}",
                $"Full path: HKLM\\{keyPath}"));

            return RegistrySetResult.Success;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log(LogEntry.Error(
                $"Access denied setting PortName for {pnpDeviceId}. Administrator privileges required.",
                ex.ToString()));
            return RegistrySetResult.AccessDenied;
        }
        catch (System.Security.SecurityException ex)
        {
            _logger.Log(LogEntry.Error(
                $"Security exception setting PortName for {pnpDeviceId}. Administrator privileges required.",
                ex.ToString()));
            return RegistrySetResult.AccessDenied;
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to set PortName for {pnpDeviceId}: {ex.GetType().Name} - {ex.Message}", ex.ToString()));
            return RegistrySetResult.Failed;
        }
    }

    public bool IsPortInUse(int portNumber)
    {
        if (portNumber < 1 || portNumber > 255)
            return false;

        try
        {
            using var arbiterKey = Registry.LocalMachine.OpenSubKey(ComArbiterPath);
            if (arbiterKey == null)
                return false;

            var comDb = arbiterKey.GetValue("ComDB") as byte[];
            if (comDb == null)
                return false;

            // Port numbers are 1-based, but array is 0-based
            int index = portNumber - 1;
            int byteIndex = index / 8;
            int bitIndex = index % 8;

            if (byteIndex >= comDb.Length)
                return false;

            return (comDb[byteIndex] & (1 << bitIndex)) != 0;
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Warning($"Failed to check COM port arbiter: {ex.Message}"));
            return false;
        }
    }

    public IReadOnlyList<int> GetRegisteredPorts()
    {
        var ports = new List<int>();

        try
        {
            using var arbiterKey = Registry.LocalMachine.OpenSubKey(ComArbiterPath);
            if (arbiterKey == null)
                return ports;

            var comDb = arbiterKey.GetValue("ComDB") as byte[];
            if (comDb == null)
                return ports;

            for (int i = 0; i < comDb.Length * 8 && i < 255; i++)
            {
                int byteIndex = i / 8;
                int bitIndex = i % 8;
                if ((comDb[byteIndex] & (1 << bitIndex)) != 0)
                {
                    ports.Add(i + 1); // Port numbers are 1-based
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Warning($"Failed to read COM port arbiter: {ex.Message}"));
        }

        return ports;
    }

    public string? BackupDeviceSettings(string pnpDeviceId)
    {
        if (string.IsNullOrEmpty(pnpDeviceId))
            return null;

        try
        {
            var keyPath = $@"{EnumBasePath}\{pnpDeviceId}\Device Parameters";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key == null)
                return null;

            var backup = new Dictionary<string, object?>();
            foreach (var valueName in key.GetValueNames())
            {
                backup[valueName] = key.GetValue(valueName);
            }

            var json = JsonSerializer.Serialize(new
            {
                PnpDeviceId = pnpDeviceId,
                KeyPath = keyPath,
                Timestamp = DateTime.Now,
                Values = backup
            }, new JsonSerializerOptions { WriteIndented = true });

            _logger.Log(LogEntry.Info($"Created backup for {pnpDeviceId}"));
            return json;
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to backup settings for {pnpDeviceId}: {ex.Message}"));
            return null;
        }
    }

    public bool RestoreDeviceSettings(string pnpDeviceId, string backupData)
    {
        if (string.IsNullOrEmpty(pnpDeviceId) || string.IsNullOrEmpty(backupData))
            return false;

        try
        {
            var backup = JsonSerializer.Deserialize<JsonElement>(backupData);
            var keyPath = $@"{EnumBasePath}\{pnpDeviceId}\Device Parameters";

            using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
            if (key == null)
            {
                _logger.Log(LogEntry.Error($"Registry key not found for restore: {keyPath}"));
                return false;
            }

            if (backup.TryGetProperty("Values", out var values))
            {
                foreach (var prop in values.EnumerateObject())
                {
                    object? value = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => (object)prop.Value.GetInt32(),
                        _ => prop.Value.ToString()
                    };
                    if (value != null)
                    {
                        key.SetValue(prop.Name, value);
                    }
                }
            }

            _logger.Log(LogEntry.Info($"Restored settings for {pnpDeviceId}"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to restore settings for {pnpDeviceId}: {ex.Message}"));
            return false;
        }
    }
}
