namespace StickyTerm.Models;

/// <summary>
/// Result of applying a rule to a device.
/// </summary>
public class ApplyResult
{
    public bool Success { get; set; }
    public bool AccessDenied { get; set; }
    public string Message { get; set; } = string.Empty;
    public ComDevice? Device { get; set; }
    public PortRule? Rule { get; set; }
    public string? OldComPort { get; set; }
    public string? NewComPort { get; set; }
    public bool RequiresRestart { get; set; }
    public Exception? Exception { get; set; }

    public static ApplyResult Succeeded(ComDevice device, PortRule rule, string oldPort, string newPort, bool requiresRestart = true)
    {
        return new ApplyResult
        {
            Success = true,
            Message = $"Successfully changed {device.FriendlyName} from {oldPort} to {newPort}",
            Device = device,
            Rule = rule,
            OldComPort = oldPort,
            NewComPort = newPort,
            RequiresRestart = requiresRestart
        };
    }

    public static ApplyResult Failed(ComDevice device, PortRule rule, string message, Exception? ex = null)
    {
        return new ApplyResult
        {
            Success = false,
            Message = message,
            Device = device,
            Rule = rule,
            Exception = ex
        };
    }

    public static ApplyResult NoChange(ComDevice device, PortRule rule)
    {
        return new ApplyResult
        {
            Success = true,
            Message = $"{device.FriendlyName} already on {rule.TargetComPort}",
            Device = device,
            Rule = rule,
            OldComPort = device.ComPort,
            NewComPort = rule.TargetComPort,
            RequiresRestart = false
        };
    }

    public static ApplyResult Conflict(ComDevice device, PortRule rule, string conflictingDevice)
    {
        return new ApplyResult
        {
            Success = false,
            Message = $"Cannot assign {rule.TargetComPort} - already in use by {conflictingDevice}",
            Device = device,
            Rule = rule
        };
    }

    public static ApplyResult DryRun(ComDevice device, PortRule rule, string oldPort, string newPort)
    {
        return new ApplyResult
        {
            Success = true,
            Message = $"[DRY RUN] Would change {device.FriendlyName} from {oldPort} to {newPort}",
            Device = device,
            Rule = rule,
            OldComPort = oldPort,
            NewComPort = newPort,
            RequiresRestart = false
        };
    }

    public static ApplyResult PermissionDenied(ComDevice device, PortRule rule)
    {
        return new ApplyResult
        {
            Success = false,
            AccessDenied = true,
            Message = "Administrator privileges required to change COM port assignments",
            Device = device,
            Rule = rule
        };
    }
}
