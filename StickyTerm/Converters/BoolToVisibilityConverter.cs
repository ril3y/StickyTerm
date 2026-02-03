using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StickyTerm.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var invert = parameter?.ToString()?.ToLowerInvariant() == "invert";
            if (invert)
                boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            var invert = parameter?.ToString()?.ToLowerInvariant() == "invert";
            var result = visibility == Visibility.Visible;
            return invert ? !result : result;
        }
        return false;
    }
}
