using CommunityToolkit.Mvvm.ComponentModel;

namespace ComPortManager.Models;

/// <summary>
/// Application settings.
/// </summary>
public partial class AppSettings : ObservableObject
{
    [ObservableProperty]
    private bool _autoApplyOnStartup;

    [ObservableProperty]
    private bool _watchModeEnabled;

    [ObservableProperty]
    private int _watchModeIntervalMs = 2000;

    [ObservableProperty]
    private bool _showWarningsForMissingSerials = true;

    [ObservableProperty]
    private bool _confirmBeforeApply = true;

    [ObservableProperty]
    private bool _autoRestartDevice;

    [ObservableProperty]
    private int _preferredStartingComPort = 20;

    [ObservableProperty]
    private bool _dryRunMode;

    [ObservableProperty]
    private bool _enableLogging = true;

    [ObservableProperty]
    private int _logRetentionDays = 30;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private string _theme = "Light";

    [ObservableProperty]
    private double _windowWidth = 1200;

    [ObservableProperty]
    private double _windowHeight = 700;

    [ObservableProperty]
    private double _windowLeft = 100;

    [ObservableProperty]
    private double _windowTop = 100;

    [ObservableProperty]
    private bool _windowMaximized;

    /// <summary>
    /// Creates default settings.
    /// </summary>
    public static AppSettings CreateDefault()
    {
        return new AppSettings();
    }
}
