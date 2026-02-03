using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StickyTerm.Converters;

/// <summary>
/// Converts connection state to appropriate button background or foreground color.
/// Connected = Red (for disconnect action), Disconnected = Accent (for connect action).
/// Pass ConverterParameter="Foreground" to get the text color instead of background.
/// </summary>
public class BoolToConnectionColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isForeground = parameter is string s && s.Equals("Foreground", StringComparison.OrdinalIgnoreCase);

        if (value is bool isConnected)
        {
            if (isConnected)
            {
                // Connected - red background, light text
                return Application.Current.FindResource(isForeground ? "ErrorForegroundBrush" : "ErrorBrush") as Brush ?? Brushes.Red;
            }
            else
            {
                // Disconnected - accent background, dark text
                return Application.Current.FindResource(isForeground ? "AccentForegroundBrush" : "AccentBrush") as Brush ?? Brushes.DodgerBlue;
            }
        }
        return Application.Current.FindResource(isForeground ? "AccentForegroundBrush" : "AccentBrush") as Brush ?? Brushes.DodgerBlue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
