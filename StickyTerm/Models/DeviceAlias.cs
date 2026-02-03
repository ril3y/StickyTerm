namespace StickyTerm.Models;

/// <summary>
/// Represents a user-defined alias/note for a COM port device.
/// Matched by VID+PID+SerialNumber for persistence across sessions.
/// </summary>
public class DeviceAlias
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Vendor ID for matching (e.g., "1A86")
    /// </summary>
    public string? Vid { get; set; }

    /// <summary>
    /// Product ID for matching (e.g., "7523")
    /// </summary>
    public string? Pid { get; set; }

    /// <summary>
    /// Serial number for matching (optional, for more specific matching)
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// User-defined friendly name (e.g., "Meshtastic 1", "GPS Module")
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Checks if this alias matches a device based on VID/PID/Serial.
    /// </summary>
    public bool Matches(string? vid, string? pid, string? serialNumber)
    {
        // Must match VID and PID
        if (!string.Equals(Vid, vid, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Pid, pid, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // If alias has serial number, device must match it
        if (!string.IsNullOrEmpty(SerialNumber))
        {
            return string.Equals(SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase);
        }

        // VID/PID match, no serial requirement
        return true;
    }
}
