using System.Globalization;
using System.Windows.Data;

namespace StickyTerm.Converters;

/// <summary>
/// Converts boolean (connected state) to "Connect" or "Disconnect" text.
/// </summary>
public class BoolToConnectTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isConnected)
        {
            return isConnected ? "Disconnect" : "Connect";
        }
        return "Connect";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
