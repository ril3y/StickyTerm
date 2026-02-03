using System.Collections.ObjectModel;
using StickyTerm.Models;

namespace StickyTerm.Services;

/// <summary>
/// Rule service implementation.
/// </summary>
public class RuleService : IRuleService
{
    private readonly List<PortRule> _rules = [];
    private readonly IRegistryService _registryService;
    private readonly IDeviceManagerService _deviceManager;
    private readonly IPersistenceService _persistence;
    private readonly ILoggingService _logger;
    private readonly AppSettings _settings;

    public IReadOnlyList<PortRule> Rules => _rules.AsReadOnly();

    public RuleService(
        IRegistryService registryService,
        IDeviceManagerService deviceManager,
        IPersistenceService persistence,
        ILoggingService logger,
        AppSettings settings)
    {
        _registryService = registryService;
        _deviceManager = deviceManager;
        _persistence = persistence;
        _logger = logger;
        _settings = settings;
    }

    public void AddRule(PortRule rule)
    {
        rule.CreatedAt = DateTime.Now;
        rule.UpdatedAt = DateTime.Now;
        _rules.Add(rule);
        _logger.Log(LogEntry.Info($"Added rule: {rule.Name} -> {rule.TargetComPort}"));
    }

    public void UpdateRule(PortRule rule)
    {
        var existing = _rules.FirstOrDefault(r => r.Id == rule.Id);
        if (existing != null)
        {
            var index = _rules.IndexOf(existing);
            rule.UpdatedAt = DateTime.Now;
            _rules[index] = rule;
            _logger.Log(LogEntry.Info($"Updated rule: {rule.Name}"));
        }
    }

    public void RemoveRule(string ruleId)
    {
        var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule != null)
        {
            _rules.Remove(rule);
            _logger.Log(LogEntry.Info($"Removed rule: {rule.Name}"));
        }
    }

    public PortRule? FindMatchingRule(ComDevice device)
    {
        return _rules
            .Where(r => r.Enabled && r.Matches(device))
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.MatchType) // VidPidSerial has highest specificity
            .FirstOrDefault();
    }

    public IReadOnlyList<PortRule> FindAllMatchingRules(ComDevice device)
    {
        return _rules
            .Where(r => r.Matches(device))
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.MatchType)
            .ToList();
    }

    public async Task<ApplyResult> ApplyRuleAsync(ComDevice device, PortRule rule, bool dryRun = false)
    {
        if (device == null || rule == null)
            return ApplyResult.Failed(device!, rule!, "Device or rule is null");

        // Check if already on target port
        if (string.Equals(device.ComPort, rule.TargetComPort, StringComparison.OrdinalIgnoreCase))
        {
            return ApplyResult.NoChange(device, rule);
        }

        var oldPort = device.ComPort;
        var newPort = rule.TargetComPort;

        // Check settings
        if (_settings.DryRunMode || dryRun)
        {
            _logger.Log(LogEntry.Info($"[DRY RUN] Would change {device.FriendlyName} from {oldPort} to {newPort}"));
            return ApplyResult.DryRun(device, rule, oldPort, newPort);
        }

        // Apply the change
        try
        {
            // Backup current settings
            var backup = _registryService.BackupDeviceSettings(device.PnpDeviceId);

            // Set new port name
            var setResult = _registryService.SetPortName(device.PnpDeviceId, newPort);
            if (setResult == RegistrySetResult.AccessDenied)
            {
                return ApplyResult.PermissionDenied(device, rule);
            }
            if (setResult != RegistrySetResult.Success)
            {
                return ApplyResult.Failed(device, rule, $"Failed to set registry value for {device.FriendlyName}");
            }

            // Update rule statistics
            rule.LastAppliedAt = DateTime.Now;
            rule.ApplyCount++;

            // Log the change
            _logger.Log(LogEntry.PortChange(device.FriendlyName, oldPort, newPort, rule.Name));

            // Optionally restart device
            bool requiresRestart = true;
            if (_settings.AutoRestartDevice)
            {
                var restarted = await _deviceManager.RestartDeviceAsync(device.PnpDeviceId);
                if (restarted)
                {
                    requiresRestart = false;
                    _logger.Log(LogEntry.Info($"Device {device.FriendlyName} restarted successfully"));
                }
                else
                {
                    _logger.Log(LogEntry.Warning($"Failed to restart {device.FriendlyName}, manual replug may be required"));
                }
            }

            return ApplyResult.Succeeded(device, rule, oldPort, newPort, requiresRestart);
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to apply rule to {device.FriendlyName}: {ex.Message}", ex.ToString()));
            return ApplyResult.Failed(device, rule, ex.Message, ex);
        }
    }

    public async Task<IReadOnlyList<ApplyResult>> ApplyRulesToDevicesAsync(IEnumerable<ComDevice> devices, bool dryRun = false)
    {
        var results = new List<ApplyResult>();

        foreach (var device in devices)
        {
            var rule = FindMatchingRule(device);
            if (rule != null)
            {
                // Check for conflicts first
                if (HasConflict(rule.TargetComPort, devices, device))
                {
                    var conflictingDevice = GetDeviceUsingPort(rule.TargetComPort, devices);
                    results.Add(ApplyResult.Conflict(device, rule,
                        conflictingDevice?.FriendlyName ?? rule.TargetComPort));
                    continue;
                }

                var result = await ApplyRuleAsync(device, rule, dryRun);
                results.Add(result);
            }
        }

        return results;
    }

    public bool HasConflict(string targetPort, IEnumerable<ComDevice> currentDevices, ComDevice? excludeDevice = null)
    {
        // Check if any other device is using this port
        var conflict = currentDevices.Any(d =>
            d != excludeDevice &&
            string.Equals(d.ComPort, targetPort, StringComparison.OrdinalIgnoreCase));

        if (conflict)
            return true;

        // Check COM name arbiter
        var portNumber = int.TryParse(targetPort.Replace("COM", ""), out var n) ? n : 0;
        if (portNumber > 0)
        {
            return _registryService.IsPortInUse(portNumber);
        }

        return false;
    }

    public ComDevice? GetDeviceUsingPort(string comPort, IEnumerable<ComDevice> devices)
    {
        return devices.FirstOrDefault(d =>
            string.Equals(d.ComPort, comPort, StringComparison.OrdinalIgnoreCase));
    }

    public async Task LoadRulesAsync()
    {
        try
        {
            var rules = await _persistence.LoadRulesAsync();
            _rules.Clear();
            _rules.AddRange(rules);
            _logger.Log(LogEntry.Info($"Loaded {rules.Count} rules"));
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to load rules: {ex.Message}"));
        }
    }

    public async Task SaveRulesAsync()
    {
        try
        {
            await _persistence.SaveRulesAsync(_rules);
            _logger.Log(LogEntry.Info($"Saved {_rules.Count} rules"));
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Failed to save rules: {ex.Message}"));
        }
    }
}
