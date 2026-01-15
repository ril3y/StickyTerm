using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ComPortManager.Services;

namespace ComPortManager.Views;

public partial class ThemedMessageBox : Window
{
    // DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public string Message { get; set; } = "";
    public bool ShowYesNo { get; set; }
    public bool ShowOk { get; set; } = true;
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public ThemedMessageBox()
    {
        InitializeComponent();
        DataContext = this;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Apply dark title bar based on current theme
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero && ThemeManager.Instance.IsDarkMode)
        {
            int value = 1;
            // Try Windows 10 20H1+ attribute first, fall back to older attribute
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
            }
        }
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Yes;
        DialogResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.No;
        DialogResult = false;
        Close();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        DialogResult = true;
        Close();
    }

    public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK)
    {
        var dialog = new ThemedMessageBox
        {
            Title = title,
            Message = message,
            ShowYesNo = buttons == MessageBoxButton.YesNo,
            ShowOk = buttons == MessageBoxButton.OK,
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();
        return dialog.Result;
    }

    public static MessageBoxResult Show(Window owner, string message, string title, MessageBoxButton buttons = MessageBoxButton.OK)
    {
        var dialog = new ThemedMessageBox
        {
            Title = title,
            Message = message,
            ShowYesNo = buttons == MessageBoxButton.YesNo,
            ShowOk = buttons == MessageBoxButton.OK,
            Owner = owner
        };

        dialog.ShowDialog();
        return dialog.Result;
    }
}
