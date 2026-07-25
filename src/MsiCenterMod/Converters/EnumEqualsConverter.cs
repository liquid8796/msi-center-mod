using System.Globalization;
using System.Windows.Data;

namespace MsiCenterMod.Converters;

/// <summary>
/// Nối RadioButton.IsChecked với một giá trị enum:
/// checked ⇔ giá trị hiện tại bằng ConverterParameter.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && value.Equals(parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is not null ? parameter : System.Windows.Data.Binding.DoNothing;
}
