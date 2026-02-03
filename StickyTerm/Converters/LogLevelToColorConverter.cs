using System.Globalization;
using System.Windows;
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
                LogLevel.Debug => Application.Current.FindResource("SecondaryTextBrush") as Brush
                                  ?? new SolidColorBrush(Colors.Gray),
                LogLevel.Info => Application.Current.FindResource("PrimaryTextBrush") as Brush
                                 ?? new SolidColorBrush(Colors.Black),
                LogLevel.Warning => Application.Current.FindResource("WarningBrush") as Brush
                                    ?? new SolidColorBrush(Colors.Orange),
                LogLevel.Error => Application.Current.FindResource("ErrorBrush") as Brush
                                  ?? new SolidColorBrush(Colors.Red),
                _ => Application.Current.FindResource("PrimaryTextBrush") as Brush
                     ?? new SolidColorBrush(Colors.Black)
            };
        }
        return Application.Current.FindResource("PrimaryTextBrush") as Brush
               ?? new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
