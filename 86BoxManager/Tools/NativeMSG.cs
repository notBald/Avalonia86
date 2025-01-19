using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Styling;

namespace _86BoxManager.Tools
{
    /// <summary>
    /// Intended for use before Avalonia has inizialized, and intends to be a best
    /// effort to show an error message to a user. Do not use for any other purpose.
    /// </summary>
    internal static class NativeMSG
    {
        public static bool IsLinux { get => RuntimeInformation.IsOSPlatform(OSPlatform.Linux); }
        public static bool IsWindows { get => RuntimeInformation.IsOSPlatform(OSPlatform.Windows); }
        private static bool IsWindows10 { get => Environment.OSVersion.Version.Major == 10; }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);


        [DllImport("libmessagebox.so", CallingConvention = CallingConvention.Cdecl)]
        private static extern void show_message_box(string message, string title);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("DwmApi")] //System.Runtime.InteropServices
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

        /// <summary>
        /// Workarround for this feature being missing from Windows 10
        /// </summary>
        public static void SetDarkMode(Window w)
        {
            if (IsWindows && IsWindows10 && App.Current != null)
            {
                var build = Environment.OSVersion.Version.Build;
                var attrib = (build > 18985) ? DWMWA_USE_IMMERSIVE_DARK_MODE : DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (build > 17763)
                {
                    var hwnd = w.TryGetPlatformHandle();
                    if ((hwnd.Handle is { } handle))
                    {
                        int useImmersiveDarkMode = (App.Current.RequestedThemeVariant == ThemeVariant.Dark) ? 1 : 0;
                        DwmSetWindowAttribute(handle, attrib, [ useImmersiveDarkMode ], sizeof(int));
                    }
                }
            }
        }

#if DEBUG

        /// <summary>
        /// For giving the app a nicer name in the Linux "task manager"
        /// </summary>
        [DllImport("libc")]
        public static extern int prctl(int option, string arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5);
        public const int PR_SET_NAME = 15;
#endif

        public static void Msg(string message, string title)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                MessageBox(IntPtr.Zero, message, title, 0);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                show_message_box(message, title);
        }
    }
}
