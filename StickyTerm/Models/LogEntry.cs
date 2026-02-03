namespace StickyTerm.Models;

/// <summary>
/// Represents a log entry for COM port changes.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public LogCategory Category { get; set; } = LogCategory.General;
    public string Message { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? OldComPort { get; set; }
    public string? NewComPort { get; set; }
    public string? RuleName { get; set; }
    public string? Details { get; set; }
    public bool Success { get; set; } = true;

    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");

    public string Summary
    {
        get
        {
            if (!string.IsNullOrEmpty(OldComPort) && !string.IsNullOrEmpty(NewComPort))
                return $"{DeviceName}: {OldComPort} -> {NewComPort}";
            return Message;
        }
    }

    public static LogEntry Info(string message, string? details = null)
    {
        return new LogEntry { Level = LogLevel.Info, Message = message, Details = details };
    }

    public static LogEntry Warning(string message, string? details = null)
    {
        return new LogEntry { Level = LogLevel.Warning, Message = message, Details = details };
    }

    public static LogEntry Error(string message, string? details = null)
    {
        return new LogEntry { Level = LogLevel.Error, Message = message, Details = details, Success = false };
    }

    public static LogEntry PortChange(string deviceName, string oldPort, string newPort, string? ruleName = null)
    {
        return new LogEntry
        {
            Level = LogLevel.Info,
            Category = LogCategory.PortChange,
            Message = $"Changed COM port for {deviceName}",
            DeviceName = deviceName,
            OldComPort = oldPort,
            NewComPort = newPort,
            RuleName = ruleName
        };
    }

    public static LogEntry DeviceArrival(string deviceName, string comPort)
    {
        return new LogEntry
        {
            Level = LogLevel.Info,
            Category = LogCategory.DeviceEvent,
            Message = $"Device connected: {deviceName} on {comPort}",
            DeviceName = deviceName,
            NewComPort = comPort
        };
    }

    public static LogEntry DeviceRemoval(string deviceName, string comPort)
    {
        return new LogEntry
        {
            Level = LogLevel.Info,
            Category = LogCategory.DeviceEvent,
            Message = $"Device disconnected: {deviceName} from {comPort}",
            DeviceName = deviceName,
            OldComPort = comPort
        };
    }
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public enum LogCategory
{
    General,
    PortChange,
    DeviceEvent,
    RuleApplied,
    Settings,
    System
}
