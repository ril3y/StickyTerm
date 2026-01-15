using CommunityToolkit.Mvvm.ComponentModel;

namespace ComPortManager.Models;

/// <summary>
/// Represents a COM port device discovered on the system.
/// </summary>
public partial class ComDevice : ObservableObject
{
    [ObservableProperty]
    private string _comPort = string.Empty;

    [ObservableProperty]
    private string _friendlyName = string.Empty;

    [ObservableProperty]
    private string _deviceType = string.Empty;

    [ObservableProperty]
    private string _vid = string.Empty;

    [ObservableProperty]
    private string _pid = string.Empty;

    [ObservableProperty]
    private string _serialNumber = string.Empty;

    [ObservableProperty]
    private string _pnpDeviceId = string.Empty;

    [ObservableProperty]
    private string _driver = string.Empty;

    [ObservableProperty]
    private string _manufacturer = string.Empty;

    [ObservableProperty]
    private string _instancePath = string.Empty;

    [ObservableProperty]
    private string _deviceClass = string.Empty;

    [ObservableProperty]
    private bool _isPresent = true;

    [ObservableProperty]
    private bool _hasSerialNumber;

    [ObservableProperty]
    private string _registryKeyPath = string.Empty;

    [ObservableProperty]
    private DateTime _lastSeen = DateTime.Now;

    /// <summary>
    /// Gets the display name combining COM port and friendly name.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(ComPort)
        ? FriendlyName
        : $"{ComPort} - {FriendlyName}";

    /// <summary>
    /// Gets a unique identifier for the device based on available information.
    /// </summary>
    public string UniqueIdentifier
    {
        get
        {
            if (!string.IsNullOrEmpty(Vid) && !string.IsNullOrEmpty(Pid) && !string.IsNullOrEmpty(SerialNumber))
                return $"VID_{Vid}&PID_{Pid}&SN_{SerialNumber}";
            if (!string.IsNullOrEmpty(PnpDeviceId))
                return PnpDeviceId;
            return FriendlyName;
        }
    }

    /// <summary>
    /// Gets the stability rating based on device identification.
    /// </summary>
    public DeviceStability Stability
    {
        get
        {
            if (!string.IsNullOrEmpty(SerialNumber))
                return DeviceStability.Stable;
            if (!string.IsNullOrEmpty(Vid) && !string.IsNullOrEmpty(Pid))
                return DeviceStability.Unstable;
            return DeviceStability.Unknown;
        }
    }

    /// <summary>
    /// Creates a clone of this device.
    /// </summary>
    public ComDevice Clone()
    {
        return new ComDevice
        {
            ComPort = ComPort,
            FriendlyName = FriendlyName,
            DeviceType = DeviceType,
            Vid = Vid,
            Pid = Pid,
            SerialNumber = SerialNumber,
            PnpDeviceId = PnpDeviceId,
            Driver = Driver,
            Manufacturer = Manufacturer,
            InstancePath = InstancePath,
            DeviceClass = DeviceClass,
            IsPresent = IsPresent,
            HasSerialNumber = HasSerialNumber,
            RegistryKeyPath = RegistryKeyPath,
            LastSeen = LastSeen
        };
    }
}

/// <summary>
/// Indicates the stability of COM port assignment for a device.
/// </summary>
public enum DeviceStability
{
    /// <summary>Device has serial number, COM assignment should be stable.</summary>
    Stable,
    /// <summary>Device lacks serial number, COM assignment may change.</summary>
    Unstable,
    /// <summary>Cannot determine stability.</summary>
    Unknown
}
