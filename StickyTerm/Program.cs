using System;
using System.IO;
using System.Windows;

namespace StickyTerm;

/// <summary>
/// Custom entry point to catch errors that occur before OnStartup.
/// This is necessary because XAML resource loading can fail silently in elevated MSIX scenarios.
/// </summary>
public static class Program
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StickyTerm", "startup-early.log");

    [STAThread]
    public static void Main(string[] args)
    {
        // FIRST THING: Write to log and show messagebox to prove we're running
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyTerm");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MAIN STARTED Args: {string.Join(" ", args)}\n");
        }
        catch { }


        Log($"ProcessPath: {Environment.ProcessPath}");
        Log($"CurrentDirectory: {Environment.CurrentDirectory}");
        Log($"Is64BitProcess: {Environment.Is64BitProcess}");

        try
        {
            Log("Creating App instance...");
            var app = new App();

            Log("Calling InitializeComponent...");
            app.InitializeComponent();

            Log("Calling Run...");
            app.Run();

            Log("App.Run() returned normally");
        }
        catch (Exception ex)
        {
            Log($"FATAL ERROR: {ex}");
            MessageBox.Show(
                $"StickyTerm failed to start:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllText(LogPath, $"[{timestamp}] {message}\n");
        }
        catch
        {
            // Can't even log - nothing we can do
        }
    }
}
