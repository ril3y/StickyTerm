using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickyTerm.Helpers;
using StickyTerm.Models;
using StickyTerm.Services;
using StickyTerm.Views;

namespace StickyTerm.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDeviceEnumerationService _deviceService;
    private readonly IRuleService _ruleService;
    private readonly ILoggingService _logger;
    private readonly IPersistenceService _persistence;
    private readonly IStartupService? _startupService;
    private IApiServerService? _apiServerService;
    private readonly AppSettings _settings;
    private List<DeviceAlias> _aliases = [];

    /// <summary>
    /// Gets the serial terminal ViewModel.
    /// </summary>
    public SerialTerminalViewModel Terminal { get; }

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

    /// <summary>
    /// Gets whether the application is running with administrator privileges.
    /// </summary>
    public bool IsRunningAsAdmin => ElevationHelper.IsRunningAsAdmin();

    /// <summary>
    /// Gets the application version.
    /// </summary>
    public string AppVersion => System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>
    /// Gets or sets whether to minimize to system tray.
    /// </summary>
    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set
        {
            if (_settings.MinimizeToTray != value)
            {
                _settings.MinimizeToTray = value;
                OnPropertyChanged();
                _ = _persistence.SaveSettingsAsync(_settings);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether to start with Windows.
    /// When enabled, the app starts minimized in the system tray.
    /// </summary>
    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows != value)
            {
                _settings.StartWithWindows = value;
                OnPropertyChanged();
                ToggleStartupAsync(value);
            }
        }
    }

    private async void ToggleStartupAsync(bool enable)
    {
        if (_startupService == null)
        {
            _settings.StartWithWindows = false;
            OnPropertyChanged(nameof(StartWithWindows));
            return;
        }

        if (enable)
        {
            if (!_startupService.EnableStartup())
            {
                _settings.StartWithWindows = false;
                OnPropertyChanged(nameof(StartWithWindows));
                StatusMessage = "Failed to enable startup - see logs";
            }
            else
            {
                StatusMessage = "StickyTerm will start with Windows";
            }
        }
        else
        {
            _startupService.DisableStartup();
            StatusMessage = "Startup with Windows disabled";
        }

        await _persistence.SaveSettingsAsync(_settings);
    }

    /// <summary>
    /// Gets or sets whether the API server is enabled.
    /// </summary>
    public bool ApiEnabled
    {
        get => _settings.ApiEnabled;
        set
        {
            if (_settings.ApiEnabled != value)
            {
                _settings.ApiEnabled = value;
                OnPropertyChanged();
                _ = _persistence.SaveSettingsAsync(_settings);
                ToggleApiServerAsync(value);
            }
        }
    }

    /// <summary>
    /// Gets whether the API server is currently running.
    /// </summary>
    public bool IsApiRunning => _apiServerService?.IsRunning ?? false;

    /// <summary>
    /// Gets the API server URL if running.
    /// </summary>
    public string ApiUrl
    {
        get
        {
            if (_apiServerService?.ListeningUrl != null)
            {
                return $"{_apiServerService.ListeningUrl}/api/ports";
            }
            // Fallback when server isn't running
            return $"http://localhost:{_settings.ApiPort}/api/ports";
        }
    }

    private async void ToggleApiServerAsync(bool enable)
    {
        if (_apiServerService == null)
            return;

        try
        {
            if (enable)
            {
                await _apiServerService.StartAsync();
                StatusMessage = $"API server started on port {_settings.ApiPort}";
            }
            else
            {
                await _apiServerService.StopAsync();
                StatusMessage = "API server stopped";
            }
            OnPropertyChanged(nameof(IsApiRunning));
            OnPropertyChanged(nameof(ApiUrl));
        }
        catch (Exception ex)
        {
            StatusMessage = $"API server error: {ex.Message}";
            _settings.ApiEnabled = false;
            OnPropertyChanged(nameof(ApiEnabled));
        }
    }

    [RelayCommand]
    private void CopyApiUrl()
    {
        try
        {
            System.Windows.Clipboard.SetText(ApiUrl);
            StatusMessage = "API URL copied to clipboard";
        }
        catch
        {
            StatusMessage = "Failed to copy URL";
        }
    }

    [RelayCommand]
    private void OpenApiUrl()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ApiUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            StatusMessage = "Failed to open URL";
        }
    }

    /// <summary>
    /// Gets or sets the API server port.
    /// </summary>
    public int ApiPort
    {
        get => _settings.ApiPort;
        set
        {
            if (_settings.ApiPort != value)
            {
                _settings.ApiPort = value;
                OnPropertyChanged();
                _ = _persistence.SaveSettingsAsync(_settings);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the API server should only listen on localhost.
    /// </summary>
    public bool ApiLocalhostOnly
    {
        get => _settings.ApiLocalhostOnly;
        set
        {
            if (_settings.ApiLocalhostOnly != value)
            {
                _settings.ApiLocalhostOnly = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ApiUrl));
                _ = _persistence.SaveSettingsAsync(_settings);

                // Restart API server if running to apply new setting
                if (IsApiRunning)
                {
                    RestartApiServerAsync();
                }
            }
        }
    }

    private async void RestartApiServerAsync()
    {
        if (_apiServerService == null)
            return;

        try
        {
            await _apiServerService.StopAsync();
            await _apiServerService.StartAsync();
            OnPropertyChanged(nameof(ApiUrl));
            OnPropertyChanged(nameof(IsApiRunning));
            StatusMessage = $"API server restarted on port {_settings.ApiPort}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"API server restart failed: {ex.Message}";
        }
    }

    public MainViewModel(
        IDeviceEnumerationService deviceService,
        IRuleService ruleService,
        ILoggingService logger,
        IPersistenceService persistence,
        AppSettings settings,
        SerialTerminalViewModel terminalViewModel,
        IStartupService? startupService = null,
        IApiServerService? apiServerService = null)
    {
        _deviceService = deviceService;
        _ruleService = ruleService;
        _logger = logger;
        _persistence = persistence;
        _startupService = startupService;
        _apiServerService = apiServerService;
        _settings = settings;
        Terminal = terminalViewModel;

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

    /// <summary>
    /// Sets the API server service after construction (needed due to circular dependency).
    /// </summary>
    public void SetApiServerService(IApiServerService apiServerService)
    {
        _apiServerService = apiServerService;
        _apiServerService.SetRescanCallback(ScanDevicesForApiAsync);
        OnPropertyChanged(nameof(IsApiRunning));
        OnPropertyChanged(nameof(ApiUrl));
    }

    /// <summary>
    /// Rescans devices for the API server (silent, no UI updates).
    /// </summary>
    private async Task ScanDevicesForApiAsync()
    {
        try
        {
            var devices = await _deviceService.DiscoverDevicesAsync();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Devices.Clear();
                foreach (var device in devices)
                {
                    ApplyAliasToDevice(device);
                    Devices.Add(device);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Warning($"API device scan failed: {ex.Message}"));
        }
    }

    public async Task InitializeAsync()
    {
        await _ruleService.LoadRulesAsync();
        RefreshRules();

        // Load device aliases
        _aliases = (await _persistence.LoadAliasesAsync()).ToList();

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
    private void ShowAbout()
    {
        AboutWindow.ShowAbout();
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
                ApplyAliasToDevice(device);
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

    partial void OnCurrentTabChanged(int value)
    {
        // App runs as admin via manifest, no elevation check needed
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
            FileName = $"StickyTerm_Rules_{DateTime.Now:yyyyMMdd}"
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
            FileName = $"StickyTerm_Log_{DateTime.Now:yyyyMMdd_HHmmss}"
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
                   $"Alias: {SelectedDevice.UserAlias ?? "(none)"}\n" +
                   $"VID: {SelectedDevice.Vid}\n" +
                   $"PID: {SelectedDevice.Pid}\n" +
                   $"Serial: {SelectedDevice.SerialNumber}\n" +
                   $"PnP Device ID: {SelectedDevice.PnpDeviceId}\n" +
                   $"Device Type: {SelectedDevice.DeviceType}\n" +
                   $"Driver: {SelectedDevice.Driver}";

        Clipboard.SetText(info);
        StatusMessage = "Device info copied to clipboard";
    }

    [RelayCommand]
    private void OpenTerminalWithDevice()
    {
        if (SelectedDevice == null || string.IsNullOrEmpty(SelectedDevice.ComPort))
            return;

        Terminal.UseDevicePort(SelectedDevice.ComPort);
        CurrentTab = 3; // Terminal tab
        StatusMessage = $"Terminal set to {SelectedDevice.ComPort}";
    }

    [RelayCommand]
    private async Task SaveAliasAsync()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Select a device first";
            return;
        }

        if (string.IsNullOrEmpty(SelectedDevice.Vid) || string.IsNullOrEmpty(SelectedDevice.Pid))
        {
            StatusMessage = "Device has no VID/PID - cannot save alias";
            return;
        }

        // Find existing alias or create new one
        var existingAlias = _aliases.FirstOrDefault(a =>
            a.Matches(SelectedDevice.Vid, SelectedDevice.Pid, SelectedDevice.SerialNumber));

        if (string.IsNullOrWhiteSpace(SelectedDevice.UserAlias))
        {
            // Remove alias if empty
            if (existingAlias != null)
            {
                _aliases.Remove(existingAlias);
                await _persistence.SaveAliasesAsync(_aliases);
                StatusMessage = "Alias removed";
                _logger.Log(LogEntry.Info($"Removed alias for {SelectedDevice.FriendlyName}"));
            }
            return;
        }

        if (existingAlias != null)
        {
            // Update existing alias
            existingAlias.Alias = SelectedDevice.UserAlias;
            existingAlias.UpdatedAt = DateTime.Now;
        }
        else
        {
            // Create new alias
            var newAlias = new DeviceAlias
            {
                Vid = SelectedDevice.Vid,
                Pid = SelectedDevice.Pid,
                SerialNumber = !string.IsNullOrEmpty(SelectedDevice.SerialNumber) ? SelectedDevice.SerialNumber : null,
                Alias = SelectedDevice.UserAlias
            };
            _aliases.Add(newAlias);
        }

        await _persistence.SaveAliasesAsync(_aliases);
        StatusMessage = $"Alias saved: {SelectedDevice.UserAlias}";
        _logger.Log(LogEntry.Info($"Saved alias '{SelectedDevice.UserAlias}' for {SelectedDevice.FriendlyName}"));
    }

    /// <summary>
    /// Applies a saved alias to a device if one exists.
    /// </summary>
    private void ApplyAliasToDevice(ComDevice device)
    {
        var alias = _aliases.FirstOrDefault(a =>
            a.Matches(device.Vid, device.Pid, device.SerialNumber));

        if (alias != null)
        {
            device.UserAlias = alias.Alias;
        }
    }

    private Task RefreshAvailablePortsAsync()
    {
        // Show all COM ports 1-99 so user can pick any port (even ones in use)
        // The system will handle reassignment/conflicts when rules are applied
        AvailableComPorts.Clear();
        for (int i = 1; i <= 99; i++)
        {
            AvailableComPorts.Add(i);
        }

        // Default to the preferred starting port
        if (AvailableComPorts.Contains(_settings.PreferredStartingComPort))
        {
            SelectedTargetPort = _settings.PreferredStartingComPort;
        }
        else if (AvailableComPorts.Count > 0)
        {
            SelectedTargetPort = AvailableComPorts[0];
        }

        return Task.CompletedTask;
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
            ApplyAliasToDevice(device);
            Devices.Add(device);
            StatusMessage = $"Device connected: {device.UserAlias ?? device.FriendlyName}";

            // Auto-apply rules in watch mode
            if (IsWatchModeEnabled)
            {
                var rule = _ruleService.FindMatchingRule(device);
                if (rule != null)
                {
                    _logger.Log(LogEntry.Info($"Applying rule '{rule.Name}' to {device.FriendlyName}: {device.ComPort} -> {rule.TargetComPort}"));

                    var result = await _ruleService.ApplyRuleAsync(device, rule, _settings.DryRunMode);

                    if (result.Success)
                    {
                        if (result.RequiresRestart)
                        {
                            StatusMessage = $"Rule applied to {device.FriendlyName} - replug device";
                        }
                        else
                        {
                            StatusMessage = result.Message;
                        }
                    }
                    else
                    {
                        // Handle failure case
                        StatusMessage = $"Failed to apply rule: {result.Message}";
                        _logger.Log(LogEntry.Error($"Failed to apply rule to {device.FriendlyName}: {result.Message}",
                            result.Exception?.ToString()));
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
                d.DeviceType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (d.UserAlias?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredDevices));
    }
}
