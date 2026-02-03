using System.Globalization;
using System.Windows.Data;

namespace StickyTerm.Converters;

public class BoolToThemeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "☀ Light" : "🌙 Dark";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
