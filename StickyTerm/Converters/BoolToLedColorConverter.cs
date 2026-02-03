using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StickyTerm.Converters;

/// <summary>
/// Converts boolean state to LED on/off color.
/// </summary>
public class BoolToLedColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOn && isOn)
        {
            return Application.Current.FindResource("LedOnBrush") as Brush ?? Brushes.LimeGreen;
        }
        return Application.Current.FindResource("LedOffBrush") as Brush ?? Brushes.DarkGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
