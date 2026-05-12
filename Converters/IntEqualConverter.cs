using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OllamaManager.Converters;

public class IntEqualConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int v && parameter is string s && int.TryParse(s, out var p))
            return v == p;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
