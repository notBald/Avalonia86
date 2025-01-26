using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Avalonia86.Tools;

public static class Trays
{
    public static void ApplyIcon(this TrayIcon trayIcon, Bitmap pix)
    {
        trayIcon.Icon = new WindowIcon(pix);
    }

    public static void MakeVisible(this TrayIcon trayIcon, bool value)
    {
        if (trayIcon != null)
            trayIcon.IsVisible = value;
    }
}