using System.Windows;
using ComPortManager.Models;
using ComPortManager.Services;
using ComPortManager.ViewModels;

namespace ComPortManager;

public partial class App : Application
{
    private ILoggingService? _loggingService;
    private IPersistenceService? _persistenceService;
    private AppSettings? _settings;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize services
        _loggingService = new LoggingService();
        _persistenceService = new PersistenceService();
        _settings = await _persistenceService.LoadSettingsAsync();

        var registryService = new RegistryService(_loggingService);
        var deviceManagerService = new DeviceManagerService(_loggingService);
        var deviceEnumerationService = new WmiDeviceEnumerationService(_loggingService);

        var ruleService = new RuleService(
            registryService,
            deviceManagerService,
            _persistenceService,
            _loggingService,
            _settings);

        var viewModel = new MainViewModel(
            deviceEnumerationService,
            ruleService,
            _loggingService,
            _persistenceService,
            _settings);

        var mainWindow = new MainWindow(viewModel);
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_settings != null && _persistenceService != null)
        {
            await _persistenceService.SaveSettingsAsync(_settings);
        }

        base.OnExit(e);
    }
}
