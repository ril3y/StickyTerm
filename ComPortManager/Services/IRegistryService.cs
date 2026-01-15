using ComPortManager.Models;

namespace ComPortManager.Services;

/// <summary>
/// Service for reading and writing COM port registry settings.
/// </summary>
public interface IRegistryService
{
    /// <summary>
    /// Gets the current COM port name for a device.
    /// </summary>
    string? GetPortName(string pnpDeviceId);

    /// <summary>
    /// Sets the COM port name for a device.
    /// </summary>
    bool SetPortName(string pnpDeviceId, string portName, bool dryRun = false);

    /// <summary>
    /// Checks if a COM port is registered in the COM Name Arbiter.
    /// </summary>
    bool IsPortInUse(int portNumber);

    /// <summary>
    /// Gets all registered COM port numbers from the arbiter.
    /// </summary>
    IReadOnlyList<int> GetRegisteredPorts();

    /// <summary>
    /// Backs up device registry settings before modification.
    /// </summary>
    string? BackupDeviceSettings(string pnpDeviceId);

    /// <summary>
    /// Restores device registry settings from backup.
    /// </summary>
    bool RestoreDeviceSettings(string pnpDeviceId, string backupData);
}
