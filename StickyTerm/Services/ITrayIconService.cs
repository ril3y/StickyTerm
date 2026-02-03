using System.Windows;

namespace StickyTerm.Services;

/// <summary>
/// Service for managing the system tray icon.
/// </summary>
public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Initializes the tray icon with the main window.
    /// </summary>
    void Initialize(Window mainWindow);

    /// <summary>
    /// Shows a balloon notification.
    /// </summary>
    void ShowNotification(string title, string message);

    /// <summary>
    /// Updates the tooltip text.
    /// </summary>
    void UpdateToolTip(string text);

    /// <summary>
    /// Shows the tray icon.
    /// </summary>
    void Show();

    /// <summary>
    /// Hides the tray icon.
    /// </summary>
    void Hide();
}
