using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Win11Tweaker.UI.Converters;

/// <summary>True → green, False → grey (for mod installed status dot)</summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true
            ? new SolidColorBrush(Color.FromRgb(0, 188, 114))   // green
            : new SolidColorBrush(Color.FromRgb(120, 120, 120)); // grey

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>True → "Remove", False → "Install" (for mod toggle button)</summary>
public class BoolToInstallTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "Remove" : "Install";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
