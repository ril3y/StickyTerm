using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace StickyTerm.Services;

/// <summary>
/// Service for managing the system tray icon.
/// </summary>
public class TrayIconService : ITrayIconService
{
    private TaskbarIcon? _trayIcon;
    private Window? _mainWindow;

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;

        // Try to load the icon
        System.Windows.Media.Imaging.BitmapImage? iconSource = null;
        try
        {
            iconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Resources/app.ico"));
        }
        catch
        {
            // Pack URI may fail - icon will be null but app still works
        }

        var contextMenu = CreateContextMenu();

        _trayIcon = new TaskbarIcon
        {
            IconSource = iconSource,
            ToolTipText = "StickyTerm - COM Port Manager",
            ContextMenu = contextMenu,
            Visibility = Visibility.Visible
        };

        _trayIcon.TrayMouseDoubleClick += OnTrayDoubleClick;
        _trayIcon.TrayRightMouseUp += OnTrayRightClick;
        _trayIcon.ForceCreate();
    }

    private void OnTrayRightClick(object sender, RoutedEventArgs e)
    {
        // Explicitly show context menu on right-click (fixes H.NotifyIcon issue on some systems)
        if (_trayIcon?.ContextMenu != null)
        {
            _trayIcon.ContextMenu.IsOpen = true;
        }
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open StickyTerm" };
        openItem.Click += (s, e) => ShowMainWindow();

        var separator = new Separator();

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) => ExitApplication();

        menu.Items.Add(openItem);
        menu.Items.Add(separator);
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ExitApplication()
    {
        // Force-kill fallback if graceful shutdown hangs
        Task.Run(async () =>
        {
            await Task.Delay(1500);
            Environment.Exit(0);
        });

        try
        {
            // Dispose tray icon immediately on this thread so it disappears right away
            var icon = _trayIcon;
            _trayIcon = null;
            if (icon != null)
            {
                icon.TrayMouseDoubleClick -= OnTrayDoubleClick;
                icon.TrayRightMouseUp -= OnTrayRightClick;
                icon.Dispose();
            }

            // Then dispatch shutdown to UI thread (use BeginInvoke to avoid blocking)
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (_mainWindow is MainWindow mainWin)
                {
                    mainWin.ExitApplication();
                }
                Application.Current?.Shutdown();
            });
        }
        catch
        {
            Environment.Exit(0);
        }
    }

    private void OnTrayDoubleClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ShowMainWindow();
        }
        catch
        {
            // Ignore errors during window activation
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        try
        {
            if (!_mainWindow.Dispatcher.CheckAccess())
            {
                _mainWindow.Dispatcher.Invoke(ShowMainWindow);
                return;
            }

            _mainWindow.Show();
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
        }
        catch
        {
            // Ignore activation errors
        }
    }

    public void ShowNotification(string title, string message)
    {
        _trayIcon?.ShowNotification(title, message, NotificationIcon.Info);
    }

    public void UpdateToolTip(string text)
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = text;
        }
    }

    public void Show()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visibility = Visibility.Visible;
        }
    }

    public void Hide()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visibility = Visibility.Collapsed;
        }
    }

    public void Dispose()
    {
        var icon = _trayIcon;
        _trayIcon = null;
        _mainWindow = null;

        // Icon may already be disposed by ExitApplication
        if (icon != null)
        {
            try
            {
                icon.TrayMouseDoubleClick -= OnTrayDoubleClick;
                icon.TrayRightMouseUp -= OnTrayRightClick;
                icon.Dispose();
            }
            catch { }
        }
    }
}
