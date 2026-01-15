using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ComPortManager.Models;

/// <summary>
/// Represents a rule for binding a device to a specific COM port.
/// </summary>
public partial class PortRule : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private RuleMatchType _matchType = RuleMatchType.VidPidSerial;

    [ObservableProperty]
    private string _vid = string.Empty;

    [ObservableProperty]
    private string _pid = string.Empty;

    [ObservableProperty]
    private string _serialNumber = string.Empty;

    [ObservableProperty]
    private string _instanceIdPattern = string.Empty;

    [ObservableProperty]
    private string _friendlyNamePattern = string.Empty;

    [ObservableProperty]
    private string _targetComPort = string.Empty;

    [ObservableProperty]
    private int _priority = 100;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.Now;

    [ObservableProperty]
    private DateTime? _lastAppliedAt;

    [ObservableProperty]
    private int _applyCount;

    /// <summary>
    /// Gets the target COM port number (e.g., 15 from "COM15").
    /// </summary>
    [JsonIgnore]
    public int TargetPortNumber
    {
        get
        {
            if (string.IsNullOrEmpty(TargetComPort))
                return 0;
            var numStr = TargetComPort.Replace("COM", "", StringComparison.OrdinalIgnoreCase);
            return int.TryParse(numStr, out var num) ? num : 0;
        }
    }

    /// <summary>
    /// Gets a human-readable description of the match criteria.
    /// </summary>
    [JsonIgnore]
    public string MatchDescription
    {
        get
        {
            return MatchType switch
            {
                RuleMatchType.VidPidSerial => $"VID:{Vid} PID:{Pid} Serial:{SerialNumber}",
                RuleMatchType.VidPid => $"VID:{Vid} PID:{Pid}",
                RuleMatchType.InstanceId => $"Instance: {InstanceIdPattern}",
                RuleMatchType.FriendlyName => $"Name: {FriendlyNamePattern}",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Checks if this rule matches the given device.
    /// </summary>
    public bool Matches(ComDevice device)
    {
        if (!Enabled)
            return false;

        return MatchType switch
        {
            RuleMatchType.VidPidSerial => MatchesVidPidSerial(device),
            RuleMatchType.VidPid => MatchesVidPid(device),
            RuleMatchType.InstanceId => MatchesInstanceId(device),
            RuleMatchType.FriendlyName => MatchesFriendlyName(device),
            _ => false
        };
    }

    private bool MatchesVidPidSerial(ComDevice device)
    {
        return !string.IsNullOrEmpty(Vid) &&
               !string.IsNullOrEmpty(Pid) &&
               !string.IsNullOrEmpty(SerialNumber) &&
               string.Equals(Vid, device.Vid, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Pid, device.Pid, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(SerialNumber, device.SerialNumber, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesVidPid(ComDevice device)
    {
        return !string.IsNullOrEmpty(Vid) &&
               !string.IsNullOrEmpty(Pid) &&
               string.Equals(Vid, device.Vid, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Pid, device.Pid, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesInstanceId(ComDevice device)
    {
        if (string.IsNullOrEmpty(InstanceIdPattern))
            return false;

        // Support wildcard matching
        if (InstanceIdPattern.Contains('*'))
        {
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(InstanceIdPattern)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                device.PnpDeviceId ?? string.Empty,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return string.Equals(InstanceIdPattern, device.PnpDeviceId, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesFriendlyName(ComDevice device)
    {
        if (string.IsNullOrEmpty(FriendlyNamePattern))
            return false;

        // Support wildcard matching
        if (FriendlyNamePattern.Contains('*'))
        {
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(FriendlyNamePattern)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                device.FriendlyName ?? string.Empty,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return device.FriendlyName?.Contains(FriendlyNamePattern, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// Creates a rule from a device with default settings.
    /// </summary>
    public static PortRule FromDevice(ComDevice device, string targetPort)
    {
        var rule = new PortRule
        {
            Name = $"Rule for {device.FriendlyName}",
            TargetComPort = targetPort,
            Vid = device.Vid,
            Pid = device.Pid
        };

        if (!string.IsNullOrEmpty(device.SerialNumber))
        {
            rule.MatchType = RuleMatchType.VidPidSerial;
            rule.SerialNumber = device.SerialNumber;
            rule.Priority = 100; // Highest priority for exact match
        }
        else
        {
            rule.MatchType = RuleMatchType.VidPid;
            rule.Priority = 50; // Lower priority for VID/PID only
        }

        return rule;
    }

    /// <summary>
    /// Creates a clone of this rule.
    /// </summary>
    public PortRule Clone()
    {
        return new PortRule
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            MatchType = MatchType,
            Vid = Vid,
            Pid = Pid,
            SerialNumber = SerialNumber,
            InstanceIdPattern = InstanceIdPattern,
            FriendlyNamePattern = FriendlyNamePattern,
            TargetComPort = TargetComPort,
            Priority = Priority,
            Notes = Notes,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            LastAppliedAt = LastAppliedAt,
            ApplyCount = ApplyCount
        };
    }
}

/// <summary>
/// Specifies how a rule matches devices.
/// </summary>
public enum RuleMatchType
{
    /// <summary>Match by VID, PID, and Serial Number (most specific).</summary>
    VidPidSerial,
    /// <summary>Match by VID and PID only.</summary>
    VidPid,
    /// <summary>Match by PNP Instance ID pattern.</summary>
    InstanceId,
    /// <summary>Match by friendly name pattern.</summary>
    FriendlyName
}
