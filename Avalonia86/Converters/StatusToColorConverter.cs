using Avalonia86.ViewModels;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Globalization;
using System.Reflection;

namespace Avalonia86.Converters
{
    /// <summary>
    /// Converts a status into a color
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush StoppedColor = new(System.Convert.ToUInt32("FF808080", 16));
        private static readonly SolidColorBrush RunningColor = new(System.Convert.ToUInt32("FF00F000", 16));
        private static readonly SolidColorBrush PausedColor = new(System.Convert.ToUInt32("FFFFFF00", 16));
        private static readonly SolidColorBrush WaitingColor = new(System.Convert.ToUInt32("FFFF7F27", 16));

        public static readonly StatusToColorConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MachineStatus ms && targetType.IsAssignableFrom(typeof(IBrush)))
            {
                switch(ms)
                {
                    case MachineStatus.RUNNING:
                        return RunningColor;

                    case MachineStatus.PAUSED:
                        return PausedColor;

                    case MachineStatus.WAITING:
                        return WaitingColor;

                    default:
                        return StoppedColor;
                }
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
