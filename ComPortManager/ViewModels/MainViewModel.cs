using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComPortManager.Models;
using ComPortManager.Services;
using ComPortManager.Views;

namespace ComPortManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDeviceEnumerationService _deviceService;
    private readonly IRuleService _ruleService;
    private readonly ILoggingService _logger;
    private readonly IPersistenceService _persistence;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private ObservableCollection<ComDevice> _devices = [];

    [ObservableProperty]
    private ComDevice? _selectedDevice;

    [ObservableProperty]
    private ObservableCollection<PortRule> _rules = [];

    [ObservableProperty]
    private PortRule? _selectedRule;

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logEntries = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isWatchModeEnabled;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _currentTab;

    [ObservableProperty]
    private ObservableCollection<int> _availableComPorts = [];

    [ObservableProperty]
    private int _selectedTargetPort;

    [ObservableProperty]
    private bool _isDarkMode;

    public MainViewModel(
        IDeviceEnumerationService deviceService,
        IRuleService ruleService,
        ILoggingService logger,
        IPersistenceService persistence,
        AppSettings settings)
    {
        _deviceService = deviceService;
        _ruleService = ruleService;
        _logger = logger;
        _persistence = persistence;
        _settings = settings;

        // Subscribe to log events
        _logger.EntryAdded += OnLogEntryAdded;

        // Subscribe to device events
        _deviceService.DeviceArrived += OnDeviceArrived;
        _deviceService.DeviceRemoved += OnDeviceRemoved;

        // Follow system theme by default, or use saved preference
        if (string.IsNullOrEmpty(_settings.Theme))
        {
            // No saved preference - follow system theme
            ThemeManager.Instance.SetSystemTheme();
            IsDarkMode = ThemeManager.Instance.IsDarkMode;
        }
        else
        {
            // Use saved preference
            IsDarkMode = _settings.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            ThemeManager.Instance.SetTheme(IsDarkMode);
        }
    }

    public async Task InitializeAsync()
    {
        await _ruleService.LoadRulesAsync();
        RefreshRules();

        await ScanDevicesAsync();
        await RefreshAvailablePortsAsync();

        // Always apply rules on startup
        await ApplyAllRulesAsync();

        // Always enable watch mode - this is the core functionality
        IsWatchModeEnabled = true;
        _deviceService.StartWatching();

        _logger.Log(LogEntry.Info("Application initialized - Watch mode active"));
    }

    [RelayCommand]
    private async Task ToggleDarkModeAsync()
    {
        IsDarkMode = !IsDarkMode;
        ThemeManager.Instance.SetTheme(IsDarkMode);

        _settings.Theme = IsDarkMode ? "Dark" : "Light";
        await _persistence.SaveSettingsAsync(_settings);

        _logger.Log(LogEntry.Info($"Theme changed to {_settings.Theme}"));
    }

    [RelayCommand]
    private async Task ScanDevicesAsync()
    {
        if (IsScanning)
            return;

        IsScanning = true;
        StatusMessage = "Scanning for devices...";

        try
        {
            var devices = await _deviceService.DiscoverDevicesAsync();
            Devices.Clear();

            foreach (var device in devices)
            {
                Devices.Add(device);
            }

            StatusMessage = $"Found {devices.Count} COM port device(s)";
            _logger.Log(LogEntry.Info($"Scan completed: {devices.Count} devices found"));

            await RefreshAvailablePortsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
            _logger.Log(LogEntry.Error($"Device scan failed: {ex.Message}"));
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ApplyAllRulesAsync()
    {
        if (Devices.Count == 0)
        {
            StatusMessage = "No devices to apply rules to";
            return;
        }

        StatusMessage = "Applying rules...";

        try
        {
            var results = await _ruleService.ApplyRulesToDevicesAsync(Devices, _settings.DryRunMode);

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count(r => !r.Success);
            var noChangeCount = results.Count(r => r.Success && !r.RequiresRestart && r.OldComPort == r.NewComPort);

            StatusMessage = $"Applied: {successCount} succeeded, {failCount} failed";

            if (results.Any(r => r.RequiresRestart))
            {
                StatusMessage += " - Device replug may be required";
            }

            // Rescan to see updated ports
            if (successCount > 0)
            {
                await Task.Delay(500);
                await ScanDevicesAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apply failed: {ex.Message}";
            _logger.Log(LogEntry.Error($"Failed to apply rules: {ex.Message}"));
        }
    }

    [RelayCommand]
    private async Task ApplySelectedRuleAsync()
    {
        if (SelectedDevice == null || SelectedRule == null)
        {
            StatusMessage = "Select a device and rule first";
            return;
        }

        // Check for conflicts
        if (_ruleService.HasConflict(SelectedRule.TargetComPort, Devices, SelectedDevice))
        {
            var conflicting = _ruleService.GetDeviceUsingPort(SelectedRule.TargetComPort, Devices);
            var result = ThemedMessageBox.Show(
                $"Port {SelectedRule.TargetComPort} is already in use by {conflicting?.FriendlyName ?? "another device"}. Apply anyway?",
                "Port Conflict",
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
                return;
        }

        StatusMessage = "Applying rule...";

        try
        {
            var applyResult = await _ruleService.ApplyRuleAsync(SelectedDevice, SelectedRule, _settings.DryRunMode);

            if (applyResult.Success)
            {
                StatusMessage = applyResult.Message;
                if (applyResult.RequiresRestart)
                {
                    StatusMessage += " - Replug device to see change";
                }
            }
            else
            {
                StatusMessage = $"Failed: {applyResult.Message}";
            }

            await _ruleService.SaveRulesAsync();
            await ScanDevicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apply failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleWatchMode()
    {
        IsWatchModeEnabled = !IsWatchModeEnabled;

        if (IsWatchModeEnabled)
        {
            _deviceService.StartWatching();
            StatusMessage = "Watch mode enabled - monitoring for device changes";
            _logger.Log(LogEntry.Info("Watch mode enabled"));
        }
        else
        {
            _deviceService.StopWatching();
            StatusMessage = "Watch mode disabled";
            _logger.Log(LogEntry.Info("Watch mode disabled"));
        }
    }

    [RelayCommand]
    private void UseCurrentPort()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Select a device first";
            return;
        }

        if (string.IsNullOrEmpty(SelectedDevice.ComPort))
        {
            StatusMessage = "Device has no current port";
            return;
        }

        // Extract port number from "COM3" -> 3
        var portStr = SelectedDevice.ComPort.Replace("COM", "", StringComparison.OrdinalIgnoreCase);
        if (int.TryParse(portStr, out int portNum))
        {
            SelectedTargetPort = portNum;
            StatusMessage = $"Target port set to {SelectedDevice.ComPort}";
        }
        else
        {
            StatusMessage = $"Could not parse port number from {SelectedDevice.ComPort}";
        }
    }

    /// <summary>
    /// Gets whether the selected device is already being tracked by a rule.
    /// </summary>
    public bool IsSelectedDeviceTracked => GetTrackingRuleForDevice(SelectedDevice) != null;

    /// <summary>
    /// Gets the tracking rule for a device, if one exists.
    /// </summary>
    private PortRule? GetTrackingRuleForDevice(ComDevice? device)
    {
        if (device == null || string.IsNullOrEmpty(device.Vid) || string.IsNullOrEmpty(device.Pid))
            return null;

        return Rules.FirstOrDefault(r =>
            r.Vid == device.Vid &&
            r.Pid == device.Pid &&
            (string.IsNullOrEmpty(device.SerialNumber) || r.SerialNumber == device.SerialNumber));
    }

    partial void OnSelectedDeviceChanged(ComDevice? value)
    {
        OnPropertyChanged(nameof(IsSelectedDeviceTracked));
    }

    [RelayCommand]
    private async Task TrackThisPortAsync()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Select a device first";
            return;
        }

        if (string.IsNullOrEmpty(SelectedDevice.ComPort))
        {
            StatusMessage = "Device has no current port";
            return;
        }

        if (string.IsNullOrEmpty(SelectedDevice.Vid) || string.IsNullOrEmpty(SelectedDevice.Pid))
        {
            StatusMessage = "Device has no VID/PID - cannot create stable rule";
            return;
        }

        var targetPort = SelectedDevice.ComPort;

        // Check if rule already exists for this device
        var existingRule = GetTrackingRuleForDevice(SelectedDevice);

        if (existingRule != null)
        {
            // Update existing rule's target port
            existingRule.TargetComPort = targetPort;
            await _ruleService.SaveRulesAsync();
            RefreshRules();
            OnPropertyChanged(nameof(IsSelectedDeviceTracked));

            ThemedMessageBox.Show(
                $"Updated tracking rule:\n\n" +
                $"Device: {SelectedDevice.FriendlyName}\n" +
                $"VID: {SelectedDevice.Vid}  PID: {SelectedDevice.Pid}\n" +
                $"Serial: {(string.IsNullOrEmpty(SelectedDevice.SerialNumber) ? "(none)" : SelectedDevice.SerialNumber)}\n\n" +
                $"This device will now always be assigned to {targetPort}.",
                "Port Tracking Updated");

            StatusMessage = $"Updated rule -> {targetPort}";
            return;
        }

        // Create new rule using VID/PID (and serial if available)
        var rule = PortRule.FromDevice(SelectedDevice, targetPort);
        rule.Name = $"Track {SelectedDevice.FriendlyName}";

        _ruleService.AddRule(rule);
        await _ruleService.SaveRulesAsync();

        RefreshRules();
        SelectedRule = Rules.FirstOrDefault(r => r.Id == rule.Id);
        OnPropertyChanged(nameof(IsSelectedDeviceTracked));

        // Auto-enable watch mode so tracking works automatically
        if (!IsWatchModeEnabled)
        {
            IsWatchModeEnabled = true;
            _deviceService.StartWatching();
            _logger.Log(LogEntry.Info("Watch mode auto-enabled for port tracking"));
        }

        ThemedMessageBox.Show(
            $"Port tracking enabled!\n\n" +
            $"Device: {SelectedDevice.FriendlyName}\n" +
            $"VID: {SelectedDevice.Vid}  PID: {SelectedDevice.Pid}\n" +
            $"Serial: {(string.IsNullOrEmpty(SelectedDevice.SerialNumber) ? "(none)" : SelectedDevice.SerialNumber)}\n\n" +
            $"This device will now always be assigned to {targetPort}.\n\n" +
            $"How it works:\n" +
            $"• Watch Mode has been enabled automatically\n" +
            $"• When you unplug and replug this device (even to a different USB port),\n" +
            $"  it will automatically be reassigned to {targetPort}",
            "Port Tracking Enabled");

        StatusMessage = $"Tracking {targetPort} - Watch Mode active";
        _logger.Log(LogEntry.Info($"Created tracking rule for {SelectedDevice.FriendlyName} -> {targetPort}"));
    }

    [RelayCommand]
    private async Task UntrackThisPortAsync()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Select a device first";
            return;
        }

        var existingRule = GetTrackingRuleForDevice(SelectedDevice);
        if (existingRule == null)
        {
            StatusMessage = "Device is not being tracked";
            return;
        }

        var result = ThemedMessageBox.Show(
            $"Stop tracking this device?\n\n" +
            $"Device: {SelectedDevice.FriendlyName}\n" +
            $"Currently assigned to: {existingRule.TargetComPort}\n\n" +
            $"The device will be assigned a new COM port by Windows next time it's connected.",
            "Stop Tracking",
            MessageBoxButton.YesNo);

        if (result != MessageBoxResult.Yes)
            return;

        _ruleService.RemoveRule(existingRule.Id);
        await _ruleService.SaveRulesAsync();

        RefreshRules();
        OnPropertyChanged(nameof(IsSelectedDeviceTracked));

        StatusMessage = $"Stopped tracking {SelectedDevice.FriendlyName}";
        _logger.Log(LogEntry.Info($"Removed tracking rule for {SelectedDevice.FriendlyName}"));
    }

    [RelayCommand]
    private async Task CreateRuleFromDeviceAsync()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Select a device first";
            return;
        }

        if (SelectedTargetPort <= 0)
        {
            StatusMessage = "Select a target COM port";
            return;
        }

        var targetPort = $"COM{SelectedTargetPort}";

        // Check for conflicts
        if (_ruleService.HasConflict(targetPort, Devices, SelectedDevice))
        {
            var conflicting = _ruleService.GetDeviceUsingPort(targetPort, Devices);
            StatusMessage = $"Port {targetPort} is in use by {conflicting?.FriendlyName}";
            return;
        }

        // Warn about missing serial
        if (!SelectedDevice.HasSerialNumber && _settings.ShowWarningsForMissingSerials)
        {
            var result = ThemedMessageBox.Show(
                $"Device '{SelectedDevice.FriendlyName}' has no serial number. " +
                "The COM port binding may not be stable if multiple identical devices are connected.\n\n" +
                "Create rule anyway?",
                "Missing Serial Number",
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
                return;
        }

        var rule = PortRule.FromDevice(SelectedDevice, targetPort);
        _ruleService.AddRule(rule);
        await _ruleService.SaveRulesAsync();

        RefreshRules();
        SelectedRule = Rules.FirstOrDefault(r => r.Id == rule.Id);

        StatusMessage = $"Created rule: {rule.Name}";
        _logger.Log(LogEntry.Info($"Created rule '{rule.Name}' for {SelectedDevice.FriendlyName} -> {targetPort}"));
    }

    [RelayCommand]
    private async Task DeleteRuleAsync()
    {
        if (SelectedRule == null)
        {
            StatusMessage = "Select a rule to delete";
            return;
        }

        var result = ThemedMessageBox.Show(
            $"Delete rule '{SelectedRule.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo);

        if (result != MessageBoxResult.Yes)
            return;

        var ruleName = SelectedRule.Name;
        _ruleService.RemoveRule(SelectedRule.Id);
        await _ruleService.SaveRulesAsync();

        RefreshRules();
        SelectedRule = null;

        StatusMessage = $"Deleted rule: {ruleName}";
    }

    [RelayCommand]
    private async Task ExportRulesAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = $"ComPortManager_Rules_{DateTime.Now:yyyyMMdd}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _persistence.ExportRulesAsync(dialog.FileName, _ruleService.Rules);
                StatusMessage = $"Exported {_ruleService.Rules.Count} rules";
                _logger.Log(LogEntry.Info($"Exported rules to {dialog.FileName}"));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task ImportRulesAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var importedRules = await _persistence.ImportRulesAsync(dialog.FileName);

                foreach (var rule in importedRules)
                {
                    // Generate new ID to avoid conflicts
                    rule.Id = Guid.NewGuid().ToString();
                    _ruleService.AddRule(rule);
                }

                await _ruleService.SaveRulesAsync();
                RefreshRules();

                StatusMessage = $"Imported {importedRules.Count} rules";
                _logger.Log(LogEntry.Info($"Imported {importedRules.Count} rules from {dialog.FileName}"));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task ExportLogsAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Log Files (*.log)|*.log|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = ".log",
            FileName = $"ComPortManager_Log_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _logger.ExportAsync(dialog.FileName);
                StatusMessage = "Logs exported successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _logger.Clear();
        LogEntries.Clear();
        StatusMessage = "Logs cleared";
    }

    [RelayCommand]
    private void CopyDeviceInfo()
    {
        if (SelectedDevice == null)
            return;

        var info = $"Device: {SelectedDevice.FriendlyName}\n" +
                   $"COM Port: {SelectedDevice.ComPort}\n" +
                   $"VID: {SelectedDevice.Vid}\n" +
                   $"PID: {SelectedDevice.Pid}\n" +
                   $"Serial: {SelectedDevice.SerialNumber}\n" +
                   $"PnP Device ID: {SelectedDevice.PnpDeviceId}\n" +
                   $"Device Type: {SelectedDevice.DeviceType}\n" +
                   $"Driver: {SelectedDevice.Driver}";

        Clipboard.SetText(info);
        StatusMessage = "Device info copied to clipboard";
    }

    private async Task RefreshAvailablePortsAsync()
    {
        var available = await _deviceService.GetAvailableComPortNumbersAsync(
            _settings.PreferredStartingComPort, 30);

        AvailableComPorts.Clear();
        foreach (var port in available)
        {
            AvailableComPorts.Add(port);
        }

        if (AvailableComPorts.Count > 0)
        {
            SelectedTargetPort = AvailableComPorts[0];
        }
    }

    private void RefreshRules()
    {
        Rules.Clear();
        foreach (var rule in _ruleService.Rules.OrderByDescending(r => r.Priority))
        {
            Rules.Add(rule);
        }
    }

    private void OnLogEntryAdded(object? sender, LogEntry entry)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            LogEntries.Insert(0, entry);

            // Keep only recent entries in UI
            while (LogEntries.Count > 500)
            {
                LogEntries.RemoveAt(LogEntries.Count - 1);
            }
        });
    }

    private async void OnDeviceArrived(object? sender, ComDevice device)
    {
        await Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            Devices.Add(device);
            StatusMessage = $"Device connected: {device.FriendlyName}";

            // Auto-apply rules in watch mode
            if (IsWatchModeEnabled)
            {
                var rule = _ruleService.FindMatchingRule(device);
                if (rule != null)
                {
                    var result = await _ruleService.ApplyRuleAsync(device, rule, _settings.DryRunMode);
                    if (result.Success && result.RequiresRestart)
                    {
                        StatusMessage = $"Rule applied to {device.FriendlyName} - replug device";
                    }
                }
            }
        })!;
    }

    private void OnDeviceRemoved(object? sender, ComDevice device)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var existing = Devices.FirstOrDefault(d => d.PnpDeviceId == device.PnpDeviceId);
            if (existing != null)
            {
                Devices.Remove(existing);
            }
            StatusMessage = $"Device disconnected: {device.FriendlyName}";
        });
    }

    public IEnumerable<ComDevice> FilteredDevices
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return Devices;

            var search = SearchText.ToLowerInvariant();
            return Devices.Where(d =>
                d.FriendlyName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.ComPort.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.Vid.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.Pid.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.SerialNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceType.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredDevices));
    }
}
