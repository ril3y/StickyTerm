using System.Windows;
using Microsoft.Win32;

namespace ComPortManager.Services;

/// <summary>
/// Manages application theme switching between light and dark modes.
/// </summary>
public class ThemeManager
{
    private static ThemeManager? _instance;
    public static ThemeManager Instance => _instance ??= new ThemeManager();

    public bool IsDarkMode { get; private set; }

    public event EventHandler? ThemeChanged;

    private ThemeManager()
    {
    }

    /// <summary>
    /// Detects if Windows is using dark mode.
    /// </summary>
    public static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                var value = key.GetValue("AppsUseLightTheme");
                if (value is int intValue)
                {
                    return intValue == 0; // 0 = dark mode, 1 = light mode
                }
            }
        }
        catch
        {
            // Fallback to light mode if we can't read the registry
        }
        return false;
    }

    /// <summary>
    /// Sets the theme to match the Windows system theme.
    /// </summary>
    public void SetSystemTheme()
    {
        SetTheme(IsSystemDarkMode());
    }

    public void SetTheme(bool isDark)
    {
        IsDarkMode = isDark;
        var themePath = isDark
            ? "Themes/DarkTheme.xaml"
            : "Themes/LightTheme.xaml";

        var themeUri = new Uri(themePath, UriKind.Relative);
        var themeDict = new ResourceDictionary { Source = themeUri };

        // Remove existing theme dictionary
        var existingTheme = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme.xaml") == true);

        if (existingTheme != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
        }

        // Add new theme dictionary
        Application.Current.Resources.MergedDictionaries.Insert(0, themeDict);

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleTheme()
    {
        SetTheme(!IsDarkMode);
    }
}
