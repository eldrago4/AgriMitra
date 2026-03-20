using System.Globalization;

namespace AgriMitraMobile.Converters;

/// <summary>
/// Converts a yield value (q/ha) to a bar width (pixels) relative to a max value.
/// Pass the max value as ConverterParameter (default 30).
/// </summary>
public class YieldToBarWidthConverter : IValueConverter
{
    public double MaxWidth { get; set; } = 240;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double yield = value is double d ? d : (value is float f ? f : 0);
        double max   = parameter is string s && double.TryParse(s, out double v) ? v : 30.0;
        return Math.Min(MaxWidth, yield / max * MaxWidth);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
