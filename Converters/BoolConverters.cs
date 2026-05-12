using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OllamaManager.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.Parse("#30D158"))
            : new SolidColorBrush(Color.Parse("#3A3A44"));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToStatusConverter : IValueConverter
{
    public static readonly BoolToStatusConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "In esecuzione" : "Fermo";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
