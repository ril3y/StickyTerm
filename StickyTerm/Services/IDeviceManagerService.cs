using StickyTerm.Models;

namespace StickyTerm.Services;

/// <summary>
/// Service for managing device state (enable/disable/restart).
/// </summary>
public interface IDeviceManagerService
{
    /// <summary>
    /// Disables a device.
    /// </summary>
    Task<bool> DisableDeviceAsync(string pnpDeviceId);

    /// <summary>
    /// Enables a device.
    /// </summary>
    Task<bool> EnableDeviceAsync(string pnpDeviceId);

    /// <summary>
    /// Restarts a device by disabling then enabling it.
    /// </summary>
    Task<bool> RestartDeviceAsync(string pnpDeviceId);

    /// <summary>
    /// Checks if a device is currently enabled.
    /// </summary>
    Task<bool> IsDeviceEnabledAsync(string pnpDeviceId);
}
