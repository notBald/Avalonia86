using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Avalonia86.Converters;

/// <summary>
/// Usage: 
/// 
/// <Window.Resources>
///   <local:HeightToIsVisibleConverter x:Key="HeightToIsVisibleConverter"/>
/// </Window.Resources>
/// 
/// Then: 
/// <local:MyUserControl IsVisible = "{Binding #MainWindow.Height,
///                                    Converter={StaticResource HeightToIsVisibleConverter},
///                                    ConverterParameter = 500}" />
/// </summary>
public class HeightToIsVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double height && double.TryParse(parameter?.ToString(), out double threshold))
        {
            return height >= threshold;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
