using System.Globalization;

namespace AgriMitraMobile.Converters;

public class BoolToColorConverter : IValueConverter
{
    public Color? TrueColor  { get; set; }
    public Color? FalseColor { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueColor : FalseColor;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
