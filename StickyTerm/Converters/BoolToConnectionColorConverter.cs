using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StickyTerm.Converters;

/// <summary>
/// Converts connection state to appropriate button background color.
/// Connected = Red (for disconnect action), Disconnected = Accent (for connect action)
/// </summary>
public class BoolToConnectionColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isConnected)
        {
            if (isConnected)
            {
                // Connected - show red for "Disconnect" action
                return Application.Current.FindResource("ErrorBrush") as Brush ?? Brushes.Red;
            }
            else
            {
                // Disconnected - show accent for "Connect" action
                return Application.Current.FindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue;
            }
        }
        return Application.Current.FindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
