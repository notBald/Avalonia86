using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Globalization;
using System.Reflection;


namespace Avalonia86.DialogBox
{
    public class DialogIconConverter : IValueConverter
    {
        public static readonly DialogIconConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DialogIcon icon)
            {
                string rawUri;

                switch (icon)
                {
                    case DialogIcon.Information:
                        rawUri = "Mix/info.png";
                        break;

                    case DialogIcon.Warning:
                        rawUri = "Mix/warn.png";
                        break;

                    case DialogIcon.Error:
                        rawUri = "Mix/x.png";
                        break;

                    case DialogIcon.Question:
                        rawUri = "Mix/q.png";
                        break;

                    default:
                        rawUri = "Mix/icon.png";
                        break;
                }


                string assemblyName = Assembly.GetEntryAssembly().GetName().Name;
                var uri = new Uri($"avares://{assemblyName}/Assets/{rawUri}");

                var asset = AssetLoader.Open(uri);
                if (asset != null)
                    return new Bitmap(asset);
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
