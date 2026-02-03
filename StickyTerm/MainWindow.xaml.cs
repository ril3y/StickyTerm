using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using StickyTerm.Models;
using StickyTerm.Services;
using StickyTerm.ViewModels;

namespace StickyTerm;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private AppSettings? _settings;
    private bool _isExiting;
    private Paragraph? _currentParagraph;

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

        // Handle minimize-to-tray and close behavior
        StateChanged += OnStateChanged;
        Closing += OnClosing;

        // Subscribe to terminal ViewModel events
        _viewModel.Terminal.ContentAppended += OnTerminalContentAppended;
        _viewModel.Terminal.TerminalCleared += OnTerminalCleared;
    }

    private void OnTerminalContentAppended(object? sender, TerminalContentEventArgs e)
    {
        var document = TerminalRichTextBox.Document;

        // Get or create current paragraph
        if (_currentParagraph == null || !document.Blocks.Contains(_currentParagraph))
        {
            _currentParagraph = new Paragraph { Margin = new Thickness(0) };
            document.Blocks.Add(_currentParagraph);
        }

        foreach (var run in e.Runs)
        {
            // Check if this run contains a newline
            if (run.Text.EndsWith("\n"))
            {
                // Add the run without the newline to current paragraph
                var text = run.Text.TrimEnd('\n');
                if (!string.IsNullOrEmpty(text))
                {
                    var newRun = CloneRunWithText(run, text);
                    _currentParagraph.Inlines.Add(newRun);
                }

                // Start a new paragraph
                _currentParagraph = new Paragraph { Margin = new Thickness(0) };
                document.Blocks.Add(_currentParagraph);
            }
            else
            {
                _currentParagraph.Inlines.Add(run);
            }
        }

        // Limit the number of paragraphs to prevent memory issues
        var maxParagraphs = _viewModel.Terminal.Settings.MaxDisplayLines;
        while (document.Blocks.Count > maxParagraphs)
        {
            document.Blocks.Remove(document.Blocks.FirstBlock);
        }
    }

    private static Run CloneRunWithText(Run original, string text)
    {
        var newRun = new Run(text)
        {
            Foreground = original.Foreground,
            Background = original.Background,
            FontWeight = original.FontWeight,
            FontStyle = original.FontStyle,
            TextDecorations = original.TextDecorations
        };
        return newRun;
    }

    private void OnTerminalCleared(object? sender, EventArgs e)
    {
        TerminalRichTextBox.Document.Blocks.Clear();
        _currentParagraph = null;
    }

    /// <summary>
    /// Sets the app settings for tray behavior configuration.
    /// </summary>
    public void SetSettings(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Marks the window for actual exit (not minimize to tray).
    /// </summary>
    public void ExitApplication()
    {
        _isExiting = true;
        Close();
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

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // If minimize to tray is enabled and window is minimized, hide it
        if (WindowState == WindowState.Minimized && (_settings?.MinimizeToTray ?? false))
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // If exiting for real (from tray menu), allow close
        if (_isExiting)
            return;

        // If minimize to tray is enabled, minimize instead of closing
        if (_settings?.MinimizeToTray ?? false)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            Hide();
            ShowInTaskbar = false;
        }
    }

    private void ApiUrl_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var url = _viewModel.ApiUrl;

        // Ctrl+Click opens in browser
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }
        else
        {
            // Regular click copies to clipboard
            try
            {
                System.Windows.Clipboard.SetText(url);
                _viewModel.StatusMessage = "API URL copied to clipboard";

                // Show toast notification
                if (Application.Current is App app)
                {
                    app.TrayIcon?.ShowNotification("Copied", $"API URL copied to clipboard:\n{url}");
                }
            }
            catch { }
        }
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
