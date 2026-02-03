using StickyTerm.Models;
using StickyTerm.Services;
using Moq;

namespace StickyTerm.Tests.Services;

public class RuleServiceTests
{
    private readonly Mock<IRegistryService> _registryServiceMock;
    private readonly Mock<IDeviceManagerService> _deviceManagerMock;
    private readonly Mock<IPersistenceService> _persistenceMock;
    private readonly Mock<ILoggingService> _loggerMock;
    private readonly AppSettings _settings;
    private readonly RuleService _ruleService;

    public RuleServiceTests()
    {
        _registryServiceMock = new Mock<IRegistryService>();
        _deviceManagerMock = new Mock<IDeviceManagerService>();
        _persistenceMock = new Mock<IPersistenceService>();
        _loggerMock = new Mock<ILoggingService>();
        _settings = new AppSettings();

        _ruleService = new RuleService(
            _registryServiceMock.Object,
            _deviceManagerMock.Object,
            _persistenceMock.Object,
            _loggerMock.Object,
            _settings);
    }

    #region AddRule Tests

    [Fact]
    public void AddRule_AddsRuleToList()
    {
        var rule = new PortRule { Name = "Test Rule" };

        _ruleService.AddRule(rule);

        Assert.Single(_ruleService.Rules);
        Assert.Equal("Test Rule", _ruleService.Rules[0].Name);
    }

    [Fact]
    public void AddRule_SetsTimestamps()
    {
        var rule = new PortRule { Name = "Test Rule" };
        var beforeAdd = DateTime.Now;

        _ruleService.AddRule(rule);

        Assert.True(rule.CreatedAt >= beforeAdd);
        Assert.True(rule.UpdatedAt >= beforeAdd);
    }

    #endregion

    #region RemoveRule Tests

    [Fact]
    public void RemoveRule_RemovesExistingRule()
    {
        var rule = new PortRule { Id = "test-id", Name = "Test Rule" };
        _ruleService.AddRule(rule);

        _ruleService.RemoveRule("test-id");

        Assert.Empty(_ruleService.Rules);
    }

    [Fact]
    public void RemoveRule_NonExistentRule_DoesNothing()
    {
        var rule = new PortRule { Id = "test-id", Name = "Test Rule" };
        _ruleService.AddRule(rule);

        _ruleService.RemoveRule("non-existent-id");

        Assert.Single(_ruleService.Rules);
    }

    #endregion

    #region UpdateRule Tests

    [Fact]
    public void UpdateRule_UpdatesExistingRule()
    {
        var rule = new PortRule { Id = "test-id", Name = "Original Name" };
        _ruleService.AddRule(rule);

        var updatedRule = new PortRule { Id = "test-id", Name = "Updated Name" };
        _ruleService.UpdateRule(updatedRule);

        Assert.Single(_ruleService.Rules);
        Assert.Equal("Updated Name", _ruleService.Rules[0].Name);
    }

    [Fact]
    public void UpdateRule_UpdatesTimestamp()
    {
        var rule = new PortRule { Id = "test-id", Name = "Original Name" };
        _ruleService.AddRule(rule);
        var originalUpdateTime = rule.UpdatedAt;

        // Small delay to ensure timestamp changes
        Thread.Sleep(10);

        var updatedRule = new PortRule { Id = "test-id", Name = "Updated Name" };
        _ruleService.UpdateRule(updatedRule);

        Assert.True(_ruleService.Rules[0].UpdatedAt > originalUpdateTime);
    }

    #endregion

    #region FindMatchingRule Tests

    [Fact]
    public void FindMatchingRule_ReturnsHighestPriorityMatch()
    {
        var rule1 = new PortRule
        {
            Id = "rule1",
            Enabled = true,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001",
            Priority = 50
        };
        var rule2 = new PortRule
        {
            Id = "rule2",
            Enabled = true,
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345",
            Priority = 100
        };

        _ruleService.AddRule(rule1);
        _ruleService.AddRule(rule2);

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        var match = _ruleService.FindMatchingRule(device);

        Assert.NotNull(match);
        Assert.Equal("rule2", match.Id);
    }

    [Fact]
    public void FindMatchingRule_NoMatch_ReturnsNull()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001"
        };
        _ruleService.AddRule(rule);

        var device = new ComDevice
        {
            Vid = "10C4",
            Pid = "EA60"
        };

        var match = _ruleService.FindMatchingRule(device);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatchingRule_DisabledRule_ReturnsNull()
    {
        var rule = new PortRule
        {
            Enabled = false,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001"
        };
        _ruleService.AddRule(rule);

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001"
        };

        var match = _ruleService.FindMatchingRule(device);

        Assert.Null(match);
    }

    #endregion

    #region FindAllMatchingRules Tests

    [Fact]
    public void FindAllMatchingRules_ReturnsAllMatches()
    {
        var rule1 = new PortRule
        {
            Id = "rule1",
            Enabled = true,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001",
            Priority = 50
        };
        var rule2 = new PortRule
        {
            Id = "rule2",
            Enabled = true,
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345",
            Priority = 100
        };

        _ruleService.AddRule(rule1);
        _ruleService.AddRule(rule2);

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        var matches = _ruleService.FindAllMatchingRules(device);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void FindAllMatchingRules_IncludesDisabledRules()
    {
        var rule = new PortRule
        {
            Enabled = false,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001"
        };
        _ruleService.AddRule(rule);

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001"
        };

        // FindAllMatchingRules still calls Matches() which checks Enabled
        var matches = _ruleService.FindAllMatchingRules(device);

        // Should be empty because disabled rules don't match
        Assert.Empty(matches);
    }

    #endregion

    #region HasConflict Tests

    [Fact]
    public void HasConflict_PortInUseByOtherDevice_ReturnsTrue()
    {
        var device1 = new ComDevice { ComPort = "COM10", PnpDeviceId = "device1" };
        var device2 = new ComDevice { ComPort = "COM5", PnpDeviceId = "device2" };
        var devices = new[] { device1, device2 };

        var hasConflict = _ruleService.HasConflict("COM10", devices, device2);

        Assert.True(hasConflict);
    }

    [Fact]
    public void HasConflict_PortInUseBySameDevice_ReturnsFalse()
    {
        var device = new ComDevice { ComPort = "COM10", PnpDeviceId = "device1" };
        var devices = new[] { device };

        var hasConflict = _ruleService.HasConflict("COM10", devices, device);

        Assert.False(hasConflict);
    }

    [Fact]
    public void HasConflict_PortNotInUse_ReturnsFalse()
    {
        _registryServiceMock.Setup(r => r.IsPortInUse(20)).Returns(false);

        var device = new ComDevice { ComPort = "COM5" };
        var devices = new[] { device };

        var hasConflict = _ruleService.HasConflict("COM20", devices, device);

        Assert.False(hasConflict);
    }

    [Fact]
    public void HasConflict_PortInArbiter_ReturnsTrue()
    {
        _registryServiceMock.Setup(r => r.IsPortInUse(20)).Returns(true);

        var devices = Array.Empty<ComDevice>();

        var hasConflict = _ruleService.HasConflict("COM20", devices);

        Assert.True(hasConflict);
    }

    #endregion

    #region GetDeviceUsingPort Tests

    [Fact]
    public void GetDeviceUsingPort_PortInUse_ReturnsDevice()
    {
        var device1 = new ComDevice { ComPort = "COM10", FriendlyName = "Device 1" };
        var device2 = new ComDevice { ComPort = "COM5", FriendlyName = "Device 2" };
        var devices = new[] { device1, device2 };

        var result = _ruleService.GetDeviceUsingPort("COM10", devices);

        Assert.NotNull(result);
        Assert.Equal("Device 1", result.FriendlyName);
    }

    [Fact]
    public void GetDeviceUsingPort_PortNotInUse_ReturnsNull()
    {
        var device = new ComDevice { ComPort = "COM5" };
        var devices = new[] { device };

        var result = _ruleService.GetDeviceUsingPort("COM10", devices);

        Assert.Null(result);
    }

    [Fact]
    public void GetDeviceUsingPort_CaseInsensitive()
    {
        var device = new ComDevice { ComPort = "COM10", FriendlyName = "Device" };
        var devices = new[] { device };

        var result = _ruleService.GetDeviceUsingPort("com10", devices);

        Assert.NotNull(result);
    }

    #endregion

    #region ApplyRuleAsync Tests

    [Fact]
    public async Task ApplyRuleAsync_AlreadyOnTargetPort_ReturnsNoChange()
    {
        var device = new ComDevice { ComPort = "COM10", PnpDeviceId = "device1" };
        var rule = new PortRule { TargetComPort = "COM10" };

        var result = await _ruleService.ApplyRuleAsync(device, rule);

        Assert.True(result.Success);
        Assert.False(result.RequiresRestart);
        Assert.Contains("already on", result.Message);
    }

    [Fact]
    public async Task ApplyRuleAsync_DryRunMode_DoesNotModify()
    {
        _settings.DryRunMode = true;
        var device = new ComDevice { ComPort = "COM5", PnpDeviceId = "device1" };
        var rule = new PortRule { TargetComPort = "COM10" };

        var result = await _ruleService.ApplyRuleAsync(device, rule);

        Assert.True(result.Success);
        Assert.Contains("DRY RUN", result.Message);
        _registryServiceMock.Verify(r => r.SetPortName(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ApplyRuleAsync_DryRunParameter_DoesNotModify()
    {
        var device = new ComDevice { ComPort = "COM5", PnpDeviceId = "device1" };
        var rule = new PortRule { TargetComPort = "COM10" };

        var result = await _ruleService.ApplyRuleAsync(device, rule, dryRun: true);

        Assert.True(result.Success);
        Assert.Contains("DRY RUN", result.Message);
    }

    [Fact]
    public async Task ApplyRuleAsync_Success_UpdatesRuleStatistics()
    {
        _registryServiceMock.Setup(r => r.SetPortName(It.IsAny<string>(), "COM10", false)).Returns(RegistrySetResult.Success);

        var device = new ComDevice { ComPort = "COM5", PnpDeviceId = "device1", FriendlyName = "Test Device" };
        var rule = new PortRule { TargetComPort = "COM10", ApplyCount = 0 };

        var result = await _ruleService.ApplyRuleAsync(device, rule);

        Assert.True(result.Success);
        Assert.Equal(1, rule.ApplyCount);
        Assert.NotNull(rule.LastAppliedAt);
    }

    [Fact]
    public async Task ApplyRuleAsync_RegistryFails_ReturnsFailure()
    {
        _registryServiceMock.Setup(r => r.SetPortName(It.IsAny<string>(), It.IsAny<string>(), false)).Returns(RegistrySetResult.Failed);

        var device = new ComDevice { ComPort = "COM5", PnpDeviceId = "device1", FriendlyName = "Test Device" };
        var rule = new PortRule { TargetComPort = "COM10" };

        var result = await _ruleService.ApplyRuleAsync(device, rule);

        Assert.False(result.Success);
        Assert.Contains("Failed to set registry", result.Message);
    }

    [Fact]
    public async Task ApplyRuleAsync_AutoRestart_CallsDeviceManager()
    {
        _settings.AutoRestartDevice = true;
        _registryServiceMock.Setup(r => r.SetPortName(It.IsAny<string>(), "COM10", false)).Returns(RegistrySetResult.Success);
        _deviceManagerMock.Setup(d => d.RestartDeviceAsync(It.IsAny<string>())).ReturnsAsync(true);

        var device = new ComDevice { ComPort = "COM5", PnpDeviceId = "device1", FriendlyName = "Test Device" };
        var rule = new PortRule { TargetComPort = "COM10" };

        var result = await _ruleService.ApplyRuleAsync(device, rule);

        Assert.True(result.Success);
        Assert.False(result.RequiresRestart);
        _deviceManagerMock.Verify(d => d.RestartDeviceAsync("device1"), Times.Once);
    }

    #endregion

    #region ApplyRulesToDevicesAsync Tests

    [Fact]
    public async Task ApplyRulesToDevicesAsync_AppliesMatchingRules()
    {
        _registryServiceMock.Setup(r => r.SetPortName(It.IsAny<string>(), It.IsAny<string>(), false)).Returns(RegistrySetResult.Success);
        _registryServiceMock.Setup(r => r.IsPortInUse(It.IsAny<int>())).Returns(false);

        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001",
            TargetComPort = "COM10"
        };
        _ruleService.AddRule(rule);

        var device = new ComDevice
        {
            ComPort = "COM5",
            PnpDeviceId = "device1",
            FriendlyName = "Test Device",
            Vid = "0403",
            Pid = "6001"
        };

        var results = await _ruleService.ApplyRulesToDevicesAsync(new[] { device });

        Assert.Single(results);
        Assert.True(results[0].Success);
    }

    [Fact]
    public async Task ApplyRulesToDevicesAsync_SkipsConflictingPorts()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001",
            TargetComPort = "COM10"
        };
        _ruleService.AddRule(rule);

        var device1 = new ComDevice { ComPort = "COM10", PnpDeviceId = "device1", FriendlyName = "Existing Device" };
        var device2 = new ComDevice
        {
            ComPort = "COM5",
            PnpDeviceId = "device2",
            FriendlyName = "New Device",
            Vid = "0403",
            Pid = "6001"
        };

        var results = await _ruleService.ApplyRulesToDevicesAsync(new[] { device1, device2 });

        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Contains("already in use", results[0].Message);
    }

    #endregion

    #region LoadRulesAsync Tests

    [Fact]
    public async Task LoadRulesAsync_LoadsFromPersistence()
    {
        var rules = new List<PortRule>
        {
            new PortRule { Id = "rule1", Name = "Rule 1" },
            new PortRule { Id = "rule2", Name = "Rule 2" }
        };
        _persistenceMock.Setup(p => p.LoadRulesAsync()).ReturnsAsync(rules);

        await _ruleService.LoadRulesAsync();

        Assert.Equal(2, _ruleService.Rules.Count);
    }

    [Fact]
    public async Task LoadRulesAsync_ClearsExistingRules()
    {
        _ruleService.AddRule(new PortRule { Name = "Existing Rule" });

        var rules = new List<PortRule> { new PortRule { Name = "New Rule" } };
        _persistenceMock.Setup(p => p.LoadRulesAsync()).ReturnsAsync(rules);

        await _ruleService.LoadRulesAsync();

        Assert.Single(_ruleService.Rules);
        Assert.Equal("New Rule", _ruleService.Rules[0].Name);
    }

    #endregion

    #region SaveRulesAsync Tests

    [Fact]
    public async Task SaveRulesAsync_SavesToPersistence()
    {
        _ruleService.AddRule(new PortRule { Name = "Rule 1" });
        _ruleService.AddRule(new PortRule { Name = "Rule 2" });

        await _ruleService.SaveRulesAsync();

        _persistenceMock.Verify(p => p.SaveRulesAsync(It.Is<IEnumerable<PortRule>>(r => r.Count() == 2)), Times.Once);
    }

    #endregion
}
