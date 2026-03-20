using System.Globalization;

namespace AgriMitraMobile.Converters;

/// <summary>
/// ConverterParameter = "trueHex|falseHex"  e.g. "#E8F5E9|#FFEBEE"
/// Returns the first color when value is bool true, second otherwise.
/// </summary>
public class StringToBoolColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is true;
        string param = parameter as string ?? "#000000|#000000";
        var parts = param.Split('|');
        string hex = flag ? parts[0] : (parts.Length > 1 ? parts[1] : "#000000");
        return Color.FromArgb(hex);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
