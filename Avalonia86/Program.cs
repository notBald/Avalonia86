using _86BoxManager.Core;
using _86BoxManager.Tools;
using _86BoxManager.Views;
using _86BoxManager.Xplat;
using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace _86BoxManager
{
    internal static class Program
    {
        //Get command line arguments
        public static string[] Args;

        //For grouping windows together in Win7+ taskbar
        private static readonly string AppId = "Avalonia.86Box";

        internal static frmMain Root;

        [STAThread]
        private static int Main(string[] args)
        {
            //Causes current app to determine startup path
            if (CurrentApp.IsLinux)
            {
#if DEBUG
                // Set the process name on Linux
                NativeMSG.prctl(NativeMSG.PR_SET_NAME, "Avalonia86", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
#endif
            }

            try
            {

                //Causes the databases to load.
                if (!Core.DBStore.HasDatabase || !Tools.HWDB.HasDatabase)
                    return -1;
            }
            catch (TypeInitializationException e)
            {
                Exception ex = e.InnerException ?? e;

                Console.WriteLine(ex.Message);

                NativeMSG.Msg(ex.Message, "Database init failed");

                return -1;
            }

            try
            {

                Args = args;

                Platforms.Shell.PrepareAppId(AppId);
                var startIt = BuildAvaloniaApp(args);

                //Check if it is the very first and only instance running.
                //If it's not, we need to restore and focus the existing window, 
                //as well as pass on any potential command line arguments
                if (CheckRunningManagerAndAbort(args, frmMain.WindowTitle))
                    return -1;

                //Note, If you wish to do anything on application exit, do it in App.axaml.cs
                return startIt.StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                NativeMSG.Msg(ex.Message, "App init failed");
                NativeMSG.Msg(ex.StackTrace, "Stacktrace");

                return -1;
            }
            finally
            {
                Core.DBStore.CloseDatabase();
                Tools.HWDB.CloseDatabase();
            }
        }


        /// <summary>
        /// Used by visual designer
        /// </summary>
        public static AppBuilder BuildAvaloniaApp()
            => BuildAvaloniaApp(Args, false);

        private static AppBuilder BuildAvaloniaApp(string[] args, bool withLife = true)
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
        }

        private static bool CheckRunningManagerAndAbort(string[] args, string window_title)
        {
            const string name = "86Box Manager";
            const string handleName = "86Box Manager Secret";
            if (DBStore.HasDatabase && AppSettings.Settings.AllowInstances)
                return false;


            var firstInstance = Platforms.Manager.IsFirstInstance(name);
            if (!firstInstance)
            {
                var hWnd = Platforms.Manager.RestoreAndFocus(window_title, handleName);

                // If this second instance comes from a VM shortcut, we need to pass on the
                // command line arguments so the VM will start in the existing instance.
                // NOTE: This code will have to be modified in case more
                // command line arguments are added in the future.
                if (GetVmArg(args, out var message))
                {
                    var sender = Platforms.Manager.GetSender();
                    sender.DoManagerStartVm(hWnd, message);
                }
                return true;
            }
            return false;
        }

        internal static bool GetVmArg(string[] args, out string vmName)
        {
            if (args != null && args.Length == 2 && args[0] == "-S" && args[1] != null)
            {
                vmName = args[1];
                return true;
            }
            vmName = default;
            return false;
        }
    }
}