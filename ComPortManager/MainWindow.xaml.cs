using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ComPortManager.Services;
using ComPortManager.ViewModels;

namespace ComPortManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Subscribe to theme changes
        ThemeManager.Instance.ThemeChanged += OnThemeChanged;

        // Apply title bar theme as soon as window handle is available
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyTitleBarTheme(ThemeManager.Instance.IsDarkMode);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyTitleBarTheme(ThemeManager.Instance.IsDarkMode);
    }

    private void ApplyTitleBarTheme(bool isDark)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            int value = isDark ? 1 : 0;
            // Try Windows 10 20H1+ attribute first, fall back to older attribute
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
            }
        }
    }
}
