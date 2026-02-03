namespace StickyTerm.Api;

/// <summary>
/// Standard API response wrapper.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public T? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// COM port information for API consumers (e.g., AI assistants).
/// </summary>
public class PortInfo
{
    public string ComPort { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public string? Notes { get; set; }
    public string FriendlyName { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public string? Vid { get; set; }
    public string? Pid { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsConnected { get; set; }
    public string Stability { get; set; } = "Unknown";
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// Tracking rule information for API consumers.
/// </summary>
public class RuleInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool Enabled { get; set; }
    public string TargetComPort { get; set; } = string.Empty;
    public string? Vid { get; set; }
    public string? Pid { get; set; }
    public string? SerialNumber { get; set; }
    public string MatchType { get; set; } = string.Empty;
}

/// <summary>
/// Server status information.
/// </summary>
public class StatusInfo
{
    public string AppName { get; set; } = "StickyTerm";
    public string Version { get; set; } = "1.0.0";
    public int DeviceCount { get; set; }
    public int RuleCount { get; set; }
    public int TrackedPortCount { get; set; }
    public bool IsRunningAsAdmin { get; set; }
    public bool WatchModeEnabled { get; set; }
    public TimeSpan Uptime { get; set; }
}

/// <summary>
/// API discovery information.
/// </summary>
public class ApiInfo
{
    public string Name { get; set; } = "StickyTerm API";
    public string Version { get; set; } = "1.0";
    public string Description { get; set; } = "Query COM port information for AI coding assistants";
    public List<EndpointInfo> Endpoints { get; set; } = new();
    public string? OpenApiUrl { get; set; }
}

/// <summary>
/// Endpoint description for API discovery.
/// </summary>
public class EndpointInfo
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
