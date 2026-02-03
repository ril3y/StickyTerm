using StickyTerm.Models;

namespace StickyTerm.Services;

/// <summary>
/// Service for managing and applying port mapping rules.
/// </summary>
public interface IRuleService
{
    /// <summary>
    /// Gets all configured rules.
    /// </summary>
    IReadOnlyList<PortRule> Rules { get; }

    /// <summary>
    /// Adds a new rule.
    /// </summary>
    void AddRule(PortRule rule);

    /// <summary>
    /// Updates an existing rule.
    /// </summary>
    void UpdateRule(PortRule rule);

    /// <summary>
    /// Removes a rule by ID.
    /// </summary>
    void RemoveRule(string ruleId);

    /// <summary>
    /// Finds the best matching rule for a device.
    /// </summary>
    PortRule? FindMatchingRule(ComDevice device);

    /// <summary>
    /// Finds all rules matching a device.
    /// </summary>
    IReadOnlyList<PortRule> FindAllMatchingRules(ComDevice device);

    /// <summary>
    /// Applies a rule to a device.
    /// </summary>
    Task<ApplyResult> ApplyRuleAsync(ComDevice device, PortRule rule, bool dryRun = false);

    /// <summary>
    /// Applies matching rules to all provided devices.
    /// </summary>
    Task<IReadOnlyList<ApplyResult>> ApplyRulesToDevicesAsync(IEnumerable<ComDevice> devices, bool dryRun = false);

    /// <summary>
    /// Checks for port conflicts.
    /// </summary>
    bool HasConflict(string targetPort, IEnumerable<ComDevice> currentDevices, ComDevice? excludeDevice = null);

    /// <summary>
    /// Gets the device currently using a specific COM port.
    /// </summary>
    ComDevice? GetDeviceUsingPort(string comPort, IEnumerable<ComDevice> devices);

    /// <summary>
    /// Loads rules from persistence.
    /// </summary>
    Task LoadRulesAsync();

    /// <summary>
    /// Saves rules to persistence.
    /// </summary>
    Task SaveRulesAsync();
}
