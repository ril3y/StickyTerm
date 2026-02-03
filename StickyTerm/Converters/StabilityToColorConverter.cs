using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StickyTerm.Models;

namespace StickyTerm.Converters;

public class StabilityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DeviceStability stability)
        {
            return stability switch
            {
                DeviceStability.Stable => new SolidColorBrush(Colors.Green),
                DeviceStability.Unstable => new SolidColorBrush(Colors.DodgerBlue), // Blue - works fine, just no serial
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StabilityToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DeviceStability stability)
        {
            return stability switch
            {
                DeviceStability.Stable => "Unique (has serial number)",
                DeviceStability.Unstable => "Tracked by VID/PID only",
                _ => "Unknown"
            };
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
