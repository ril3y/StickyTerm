using System.IO;
using System.Windows;
using System.Windows.Threading;
using StickyTerm.Models;
using StickyTerm.Services;
using StickyTerm.ViewModels;

namespace StickyTerm;

public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "StickyTerm_SingleInstance_Mutex";
    private static string? _startupLogPath;

    private ILoggingService? _loggingService;
    private IPersistenceService? _persistenceService;
    private ITrayIconService? _trayIconService;
    private IStartupService? _startupService;
    private IApiServerService? _apiServerService;
    private ISerialTerminalService? _serialTerminalService;
    private AppSettings? _settings;
    private MainWindow? _mainWindow;

    /// <summary>
    /// Gets the tray icon service for showing notifications.
    /// </summary>
    public ITrayIconService? TrayIcon => _trayIconService;

    /// <summary>
    /// Logs startup diagnostics to a file for debugging.
    /// </summary>
    private static void LogStartup(string message)
    {
        try
        {
            _startupLogPath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StickyTerm",
                "startup.log");

            var dir = Path.GetDirectoryName(_startupLogPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLine = $"[{timestamp}] {message}{Environment.NewLine}";
            File.AppendAllText(_startupLogPath, logLine);
        }
        catch
        {
            // Can't log - ignore
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Set up global exception handlers FIRST
        SetupGlobalExceptionHandlers();

        // Set shutdown mode explicitly to prevent silent exit
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        LogStartup($"=== Application Starting === Args: {string.Join(", ", e.Args)}");

        base.OnStartup(e);

        // Single instance check
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            LogStartup("Another instance is already running");
            MessageBox.Show(
                "StickyTerm is already running.\n\nCheck the system tray for the running instance.",
                "StickyTerm",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        LogStartup("Mutex acquired");

        // Check for command line arguments
        bool startMinimized = e.Args.Contains("--minimized");

        try
        {
            LogStartup("Initializing services...");

            // Initialize services
            _loggingService = new LoggingService();
            _persistenceService = new PersistenceService();
            _settings = Task.Run(() => _persistenceService.LoadSettingsAsync()).GetAwaiter().GetResult();
            _startupService = new StartupService(_loggingService);

            LogStartup("Core services initialized");

            // Sync startup setting with actual Task Scheduler state
            _settings.StartWithWindows = _startupService.IsStartupEnabled;

            var registryService = new RegistryService(_loggingService);
            var deviceManagerService = new DeviceManagerService(_loggingService);
            var deviceEnumerationService = new WmiDeviceEnumerationService(_loggingService);

            var ruleService = new RuleService(
                registryService,
                deviceManagerService,
                _persistenceService,
                _loggingService,
                _settings);

            LogStartup("Device services initialized");

            // Create terminal service
            var terminalService = new SerialTerminalService();
            _serialTerminalService = terminalService;
            var terminalViewModel = new SerialTerminalViewModel(terminalService, _loggingService);

            // Create ViewModel
            var viewModel = new MainViewModel(
                deviceEnumerationService,
                ruleService,
                _loggingService,
                _persistenceService,
                _settings,
                terminalViewModel,
                _startupService);

            LogStartup("ViewModel created");

            // Initialize API server
            _apiServerService = new ApiServerService(
                _settings,
                ruleService,
                _loggingService,
                () => viewModel.Devices);

            viewModel.SetApiServerService(_apiServerService);

            LogStartup("Creating MainWindow...");
            _mainWindow = new MainWindow(viewModel);
            _mainWindow.SetSettings(_settings);
            MainWindow = _mainWindow;
            LogStartup("MainWindow created");

            // Initialize tray icon
            try
            {
                LogStartup("Initializing tray icon...");
                _trayIconService = new TrayIconService();
                _trayIconService.Initialize(_mainWindow);
                LogStartup("Tray icon initialized");
            }
            catch (Exception ex)
            {
                LogStartup($"Tray icon failed: {ex.Message}");
                _loggingService?.Log(LogEntry.Warning($"Tray icon failed to initialize: {ex.Message}"));
            }

            // Start API server if enabled
            if (_settings.ApiEnabled)
            {
                LogStartup("Starting API server...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _apiServerService.StartAsync();
                        LogStartup("API server started");
                    }
                    catch (Exception ex)
                    {
                        LogStartup($"API server failed: {ex.Message}");
                        _loggingService?.Log(LogEntry.Warning($"API server failed to start: {ex.Message}"));
                    }
                });
            }

            // Show window
            LogStartup($"Showing window (minimized={startMinimized})");

            if (startMinimized)
            {
                if (_settings.MinimizeToTray)
                {
                    _mainWindow.ShowInTaskbar = false;
                    _loggingService?.Log(LogEntry.Info("Started minimized to system tray"));
                    LogStartup("Window hidden in tray");
                }
                else
                {
                    _mainWindow.WindowState = WindowState.Minimized;
                    _mainWindow.Show();
                    _loggingService?.Log(LogEntry.Info("Started minimized"));
                    LogStartup("Window shown minimized");
                }
            }
            else
            {
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.ShowInTaskbar = true;
                _mainWindow.Show();
                LogStartup("Window shown");
            }

            LogStartup("=== Startup completed successfully ===");
        }
        catch (Exception ex)
        {
            LogStartup($"STARTUP EXCEPTION: {ex}");
            MessageBox.Show(
                $"Failed to start StickyTerm:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void SetupGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogStartup($"DISPATCHER EXCEPTION: {e.Exception}");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "StickyTerm Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        LogStartup($"UNHANDLED EXCEPTION: {ex}");
        if (ex != null)
        {
            MessageBox.Show(
                $"A critical error occurred:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "StickyTerm Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogStartup($"UNOBSERVED TASK EXCEPTION: {e.Exception}");
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Close serial port first so the device is released cleanly
        try { _serialTerminalService?.Dispose(); } catch { }
        try { _apiServerService?.Dispose(); } catch { }
        try { _trayIconService?.Dispose(); } catch { }
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();

        if (_settings != null && _persistenceService != null)
        {
            try
            {
                // Save settings with timeout to prevent hang on exit
                var saveTask = Task.Run(() => _persistenceService.SaveSettingsAsync(_settings));
                saveTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch { }
        }

        base.OnExit(e);
    }
}
