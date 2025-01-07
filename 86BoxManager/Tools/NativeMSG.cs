using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace _86BoxManager.Tools
{
    /// <summary>
    /// Intended for use before Avalonia has inizialized, and intends to be a best
    /// effort to show an error message to a user. Do not use for any other purpose.
    /// </summary>
    internal static class NativeMSG
    {
        public static bool IsLinux { get => RuntimeInformation.IsOSPlatform(OSPlatform.Linux); }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int W32MessageBox(IntPtr hWnd, String text, String caption, uint type);


        [DllImport("libmessagebox.so", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkMessageBox(string message, string title);

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
                W32MessageBox(IntPtr.Zero, message, title, 0);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                GtkMessageBox(message, title);
        }
    }
}
