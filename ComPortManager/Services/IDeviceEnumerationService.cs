using ComPortManager.Models;

namespace ComPortManager.Services;

/// <summary>
/// Service for enumerating COM port devices on the system.
/// </summary>
public interface IDeviceEnumerationService
{
    /// <summary>
    /// Discovers all COM port devices currently present on the system.
    /// </summary>
    Task<IReadOnlyList<ComDevice>> DiscoverDevicesAsync();

    /// <summary>
    /// Gets a list of COM ports that are currently in use.
    /// </summary>
    Task<IReadOnlyList<string>> GetUsedComPortsAsync();

    /// <summary>
    /// Gets a list of available (unused) COM port numbers.
    /// </summary>
    Task<IReadOnlyList<int>> GetAvailableComPortNumbersAsync(int startFrom = 1, int count = 20);

    /// <summary>
    /// Raised when a device is connected.
    /// </summary>
    event EventHandler<ComDevice>? DeviceArrived;

    /// <summary>
    /// Raised when a device is disconnected.
    /// </summary>
    event EventHandler<ComDevice>? DeviceRemoved;

    /// <summary>
    /// Starts monitoring for device changes.
    /// </summary>
    void StartWatching();

    /// <summary>
    /// Stops monitoring for device changes.
    /// </summary>
    void StopWatching();
}
