using System.Globalization;

namespace AgriMitraMobile.Converters;

/// <summary>Returns true when the value is not null — used for IsVisible bindings.</summary>
public class NotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
