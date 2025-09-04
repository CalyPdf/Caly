using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Caly.Core.Converters
{
    // Its used for dark mode (true = black, false = white)
    public class BoolToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is bool isDark && isDark
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.White);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}