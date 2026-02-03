using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using StickyTerm.Api;
using StickyTerm.Helpers;
using StickyTerm.Models;

namespace StickyTerm.Services;

/// <summary>
/// HTTP API server using HttpListener for AI assistants to query COM port information.
/// </summary>
public class ApiServerService : IApiServerService
{
    private readonly AppSettings _settings;
    private readonly IRuleService _ruleService;
    private readonly ILoggingService _logger;
    private readonly Func<IEnumerable<ComDevice>> _getDevices;
    private Func<Task>? _rescanCallback;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private DateTime _startTime;
    private DateTime _lastRescan;
    private static readonly TimeSpan RescanCooldown = TimeSpan.FromSeconds(5);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public bool IsRunning => _listener?.IsListening ?? false;
    public string? ListeningUrl { get; private set; }

    public event EventHandler<bool>? StatusChanged;

    public ApiServerService(
        AppSettings settings,
        IRuleService ruleService,
        ILoggingService logger,
        Func<IEnumerable<ComDevice>> getDevices)
    {
        _settings = settings;
        _ruleService = ruleService;
        _logger = logger;
        _getDevices = getDevices;
    }

    public void SetRescanCallback(Func<Task> rescanCallback)
    {
        _rescanCallback = rescanCallback;
    }

    public Task StartAsync()
    {
        if (IsRunning || !_settings.ApiEnabled)
            return Task.CompletedTask;

        try
        {
            _listener = new HttpListener();

            var host = _settings.ApiLocalhostOnly ? "127.0.0.1" : "+";
            var prefix = $"http://{host}:{_settings.ApiPort}/";

            _listener.Prefixes.Add(prefix);
            _listener.Start();

            _cts = new CancellationTokenSource();
            _startTime = DateTime.UtcNow;

            // Set the listening URL - use machine IP if not localhost-only
            if (_settings.ApiLocalhostOnly)
            {
                ListeningUrl = $"http://localhost:{_settings.ApiPort}";
            }
            else
            {
                var ip = GetLocalIPAddress();
                ListeningUrl = $"http://{ip}:{_settings.ApiPort}";
            }

            _listenerTask = Task.Run(() => ListenAsync(_cts.Token));

            _logger.Log(LogEntry.Info($"API server started on {ListeningUrl}"));
            StatusChanged?.Invoke(this, true);

            return Task.CompletedTask;
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            _logger.Log(LogEntry.Error(
                "Failed to start API server: Access denied. " +
                "Run as administrator or use localhost-only mode."));
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to start API server: {ex.Message}"));
            throw;
        }
    }

    private static string GetLocalIPAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "localhost";
        }
        catch
        {
            return "localhost";
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        _cts?.Cancel();
        _listener?.Stop();

        if (_listenerTask != null)
        {
            try { await _listenerTask; }
            catch { }
        }

        _listener?.Close();
        _listener = null;
        ListeningUrl = null;

        _logger.Log(LogEntry.Info("API server stopped"));
        StatusChanged?.Invoke(this, false);
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Log(LogEntry.Error($"API error: {ex.Message}"));
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS headers for browser-based tools
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            response.Close();
            return;
        }

        try
        {
            var path = request.Url?.AbsolutePath.ToLowerInvariant().TrimEnd('/') ?? "/";

            // Rescan devices for data endpoints (with cooldown to prevent hammering)
            if (path is "/api/ports" or "/api/devices" or "/api/status")
            {
                await TryRescanDevicesAsync();
            }

            object? result = path switch
            {
                "/api/ports" => GetPorts(),
                "/api/devices" => GetDevices(),
                "/api/rules" => GetRules(),
                "/api/status" => GetStatus(),
                "/openapi.json" or "/api/openapi.json" or "/swagger.json" => GetOpenApiSpec(),
                "/" or "/api" or "" => GetApiInfo(),
                _ => null
            };

            if (result == null)
            {
                await SendErrorAsync(response, 404, $"Endpoint not found: {path}");
                return;
            }

            await SendJsonAsync(response, result);
        }
        catch (Exception ex)
        {
            await SendErrorAsync(response, 500, ex.Message);
        }
    }

    private async Task TryRescanDevicesAsync()
    {
        if (_rescanCallback == null)
            return;

        // Only rescan if cooldown has passed
        if (DateTime.UtcNow - _lastRescan < RescanCooldown)
            return;

        try
        {
            _lastRescan = DateTime.UtcNow;
            await _rescanCallback();
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Warning($"API rescan failed: {ex.Message}"));
        }
    }

    private object GetPorts()
    {
        var devices = _getDevices();
        var ports = devices
            .Where(d => d.IsPresent)
            .Select(d => new PortInfo
            {
                ComPort = d.ComPort,
                Alias = d.UserAlias,
                Notes = GetNotesForDevice(d),
                FriendlyName = d.FriendlyName,
                DeviceType = d.DeviceType,
                Vid = d.Vid,
                Pid = d.Pid,
                SerialNumber = d.SerialNumber,
                IsConnected = d.IsPresent,
                Stability = d.Stability.ToString(),
                LastSeen = d.LastSeen
            })
            .OrderBy(p => p.ComPort)
            .ToList();

        return new ApiResponse<List<PortInfo>> { Data = ports };
    }

    private object GetDevices()
    {
        var devices = _getDevices();
        var ports = devices.Select(d => new PortInfo
        {
            ComPort = d.ComPort,
            Alias = d.UserAlias,
            Notes = GetNotesForDevice(d),
            FriendlyName = d.FriendlyName,
            DeviceType = d.DeviceType,
            Vid = d.Vid,
            Pid = d.Pid,
            SerialNumber = d.SerialNumber,
            IsConnected = d.IsPresent,
            Stability = d.Stability.ToString(),
            LastSeen = d.LastSeen
        })
        .OrderBy(p => p.ComPort)
        .ToList();

        return new ApiResponse<List<PortInfo>> { Data = ports };
    }

    private string? GetNotesForDevice(ComDevice device)
    {
        // Find matching rule and return its notes
        var rule = _ruleService.FindMatchingRule(device);
        return rule?.Notes;
    }

    private object GetRules()
    {
        var rules = _ruleService.Rules.Select(r => new RuleInfo
        {
            Id = r.Id,
            Name = r.Name,
            Notes = r.Notes,
            Enabled = r.Enabled,
            TargetComPort = r.TargetComPort,
            Vid = r.Vid,
            Pid = r.Pid,
            SerialNumber = r.SerialNumber,
            MatchType = r.MatchType.ToString()
        }).ToList();

        return new ApiResponse<List<RuleInfo>> { Data = rules };
    }

    private object GetStatus()
    {
        var devices = _getDevices();
        return new ApiResponse<StatusInfo>
        {
            Data = new StatusInfo
            {
                AppName = "StickyTerm",
                Version = "1.0.0",
                DeviceCount = devices.Count(),
                RuleCount = _ruleService.Rules.Count,
                TrackedPortCount = _ruleService.Rules.Count(r => r.Enabled),
                IsRunningAsAdmin = ElevationHelper.IsRunningAsAdmin(),
                WatchModeEnabled = _settings.WatchModeEnabled,
                Uptime = DateTime.UtcNow - _startTime
            }
        };
    }

    private object GetApiInfo()
    {
        return new ApiInfo
        {
            Name = "StickyTerm API",
            Version = "1.0",
            Description = "Query COM port information for AI coding assistants. Use /api/ports to get tracked ports with their aliases and notes. OpenAPI spec available at /openapi.json",
            Endpoints = new List<EndpointInfo>
            {
                new() { Method = "GET", Path = "/api/ports", Description = "List connected COM ports with aliases and notes (recommended for AI assistants)" },
                new() { Method = "GET", Path = "/api/devices", Description = "List all known devices (including disconnected)" },
                new() { Method = "GET", Path = "/api/rules", Description = "List port tracking rules" },
                new() { Method = "GET", Path = "/api/status", Description = "Server status and health check" },
                new() { Method = "GET", Path = "/openapi.json", Description = "OpenAPI 3.0 specification (also at /api/openapi.json)" }
            },
            OpenApiUrl = $"{ListeningUrl}/openapi.json"
        };
    }

    private object GetOpenApiSpec()
    {
        // Helper to create $ref objects (since C# @ref becomes "ref" not "$ref")
        Dictionary<string, string> Ref(string path) => new() { ["$ref"] = path };

        return new Dictionary<string, object>
        {
            ["openapi"] = "3.0.3",
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "StickyTerm API",
                ["description"] = "Query COM port information for AI coding assistants. StickyTerm manages persistent COM port assignments for USB serial devices.",
                ["version"] = "1.0.0",
                ["contact"] = new Dictionary<string, string> { ["name"] = "StickyTerm" }
            },
            ["servers"] = new[]
            {
                new Dictionary<string, string> { ["url"] = ListeningUrl ?? "http://localhost:27182", ["description"] = "Local StickyTerm instance" }
            },
            ["paths"] = new Dictionary<string, object>
            {
                ["/api/ports"] = new Dictionary<string, object>
                {
                    ["get"] = new Dictionary<string, object>
                    {
                        ["summary"] = "List connected COM ports",
                        ["description"] = "Returns all currently connected COM ports with their aliases, notes, and device information. Recommended endpoint for AI assistants.",
                        ["operationId"] = "getPorts",
                        ["tags"] = new[] { "Ports" },
                        ["responses"] = new Dictionary<string, object>
                        {
                            ["200"] = new Dictionary<string, object>
                            {
                                ["description"] = "Successful response",
                                ["content"] = new Dictionary<string, object>
                                {
                                    ["application/json"] = new Dictionary<string, object>
                                    {
                                        ["schema"] = Ref("#/components/schemas/PortListResponse")
                                    }
                                }
                            }
                        }
                    }
                },
                ["/api/devices"] = new Dictionary<string, object>
                {
                    ["get"] = new Dictionary<string, object>
                    {
                        ["summary"] = "List all known devices",
                        ["description"] = "Returns all known devices including disconnected ones that were previously seen.",
                        ["operationId"] = "getDevices",
                        ["tags"] = new[] { "Devices" },
                        ["responses"] = new Dictionary<string, object>
                        {
                            ["200"] = new Dictionary<string, object>
                            {
                                ["description"] = "Successful response",
                                ["content"] = new Dictionary<string, object>
                                {
                                    ["application/json"] = new Dictionary<string, object>
                                    {
                                        ["schema"] = Ref("#/components/schemas/PortListResponse")
                                    }
                                }
                            }
                        }
                    }
                },
                ["/api/rules"] = new Dictionary<string, object>
                {
                    ["get"] = new Dictionary<string, object>
                    {
                        ["summary"] = "List port tracking rules",
                        ["description"] = "Returns all configured port tracking rules that assign devices to specific COM ports.",
                        ["operationId"] = "getRules",
                        ["tags"] = new[] { "Rules" },
                        ["responses"] = new Dictionary<string, object>
                        {
                            ["200"] = new Dictionary<string, object>
                            {
                                ["description"] = "Successful response",
                                ["content"] = new Dictionary<string, object>
                                {
                                    ["application/json"] = new Dictionary<string, object>
                                    {
                                        ["schema"] = Ref("#/components/schemas/RuleListResponse")
                                    }
                                }
                            }
                        }
                    }
                },
                ["/api/status"] = new Dictionary<string, object>
                {
                    ["get"] = new Dictionary<string, object>
                    {
                        ["summary"] = "Server status",
                        ["description"] = "Returns server health and status information.",
                        ["operationId"] = "getStatus",
                        ["tags"] = new[] { "Status" },
                        ["responses"] = new Dictionary<string, object>
                        {
                            ["200"] = new Dictionary<string, object>
                            {
                                ["description"] = "Successful response",
                                ["content"] = new Dictionary<string, object>
                                {
                                    ["application/json"] = new Dictionary<string, object>
                                    {
                                        ["schema"] = Ref("#/components/schemas/StatusResponse")
                                    }
                                }
                            }
                        }
                    }
                }
            },
            ["components"] = new Dictionary<string, object>
            {
                ["schemas"] = new Dictionary<string, object>
                {
                    ["PortInfo"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["description"] = "Information about a COM port device",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["comPort"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "COM port name (e.g., COM3)", ["example"] = "COM3" },
                            ["alias"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "User-defined friendly name for the device", ["example"] = "Arduino Uno" },
                            ["notes"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "Notes from the tracking rule", ["example"] = "Main development board" },
                            ["friendlyName"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Windows device name", ["example"] = "USB Serial Device (COM3)" },
                            ["deviceType"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "Detected chip family", ["example"] = "FTDI" },
                            ["vid"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "USB Vendor ID", ["example"] = "0403" },
                            ["pid"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "USB Product ID", ["example"] = "6001" },
                            ["serialNumber"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "USB serial number for unique identification", ["example"] = "A50285BI" },
                            ["isConnected"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether the device is currently connected" },
                            ["stability"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tracking stability (Stable if has serial number)", ["example"] = "Stable" },
                            ["lastSeen"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time", ["description"] = "When the device was last detected" }
                        }
                    },
                    ["RuleInfo"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["description"] = "A port tracking rule",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["id"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Unique rule identifier" },
                            ["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Rule display name", ["example"] = "Track Arduino Uno" },
                            ["notes"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "User notes about the rule" },
                            ["enabled"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether the rule is active" },
                            ["targetComPort"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Target COM port to assign", ["example"] = "COM3" },
                            ["vid"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "USB Vendor ID to match" },
                            ["pid"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "USB Product ID to match" },
                            ["serialNumber"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true, ["description"] = "USB serial number to match" },
                            ["matchType"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Rule matching strategy", ["example"] = "VidPidSerial" }
                        }
                    },
                    ["StatusInfo"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["description"] = "Server status information",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["appName"] = new Dictionary<string, object> { ["type"] = "string", ["example"] = "StickyTerm" },
                            ["version"] = new Dictionary<string, object> { ["type"] = "string", ["example"] = "1.0.0" },
                            ["deviceCount"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Number of known devices" },
                            ["ruleCount"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Total number of rules" },
                            ["trackedPortCount"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Number of active tracking rules" },
                            ["isRunningAsAdmin"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether app has admin privileges" },
                            ["watchModeEnabled"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether auto-apply on device connect is enabled" },
                            ["uptime"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Server uptime duration" }
                        }
                    },
                    ["PortListResponse"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["success"] = new Dictionary<string, object> { ["type"] = "boolean" },
                            ["error"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true },
                            ["data"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = Ref("#/components/schemas/PortInfo") },
                            ["timestamp"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" }
                        }
                    },
                    ["RuleListResponse"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["success"] = new Dictionary<string, object> { ["type"] = "boolean" },
                            ["error"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true },
                            ["data"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = Ref("#/components/schemas/RuleInfo") },
                            ["timestamp"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" }
                        }
                    },
                    ["StatusResponse"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["success"] = new Dictionary<string, object> { ["type"] = "boolean" },
                            ["error"] = new Dictionary<string, object> { ["type"] = "string", ["nullable"] = true },
                            ["data"] = Ref("#/components/schemas/StatusInfo"),
                            ["timestamp"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" }
                        }
                    }
                }
            }
        };
    }

    private async Task SendJsonAsync(HttpListenerResponse response, object data)
    {
        response.ContentType = "application/json; charset=utf-8";
        response.StatusCode = 200;

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);

        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private async Task SendErrorAsync(HttpListenerResponse response, int statusCode, string message)
    {
        response.ContentType = "application/json; charset=utf-8";
        response.StatusCode = statusCode;

        var error = new ApiResponse<object> { Success = false, Error = message };
        var json = JsonSerializer.Serialize(error, _jsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);

        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
    }
}
