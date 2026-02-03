using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StickyTerm.Models;

namespace StickyTerm.Converters;

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => new SolidColorBrush(Colors.Gray),
                LogLevel.Info => new SolidColorBrush(Colors.Black),
                LogLevel.Warning => new SolidColorBrush(Colors.Orange),
                LogLevel.Error => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Black)
            };
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
