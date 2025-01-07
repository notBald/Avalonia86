using System;
using System.Diagnostics;
using _86BoxManager.Core;
using _86BoxManager.ViewModels;
using _86BoxManager.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace _86BoxManager
{
    public partial class App : Application
    {
        private static App Me;

        internal static void Quit(int code = 0)
        {
            if (Me.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown(code);
            }
            else
            {
                Environment.Exit(code);
            }
        }

        public override void Initialize()
        {
            Me = this;

            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var main = new frmMain
                {
                    DataContext = new MainModel()
                };

                var size = Core.DBStore.FetchWindowSize();

                //If size for some reason fails to load, we do nothing. The window will the open with
                //default size.
                if (size != null && size.Width > 50 && size.Height > 50) 
                {
                    var left_pos = new PixelPoint((int)size.Left, (int)size.Top);
                    var right_pos = new PixelPoint((int)(size.Left + size.Width) , (int)size.Top);
                    var windowRect = new PixelRect(left_pos, new PixelSize((int)size.Width, (int)size.Height));
                    double windowArea = windowRect.Width * windowRect.Height * 0.5;
                    double totalIntersectionArea = 0;
                    bool isPositionValid = false;

                    //What we want is to furfill two conditions.
                    // 1. That the top/left position on the window is visible on at least one screen.
                    //    The goal here is to avoid situations where the top of the window is above
                    //    the screen.
                    // 2. At least 50% of the window is visible on all screens combinded. Maybe we can
                    //    reduse this number, as what we want is a decent chunk of the app visible.
                    foreach (var screen in main.Screens.All)
                    {
                        var intersection = screen.Bounds.Intersect(windowRect);
                        totalIntersectionArea += intersection.Width * intersection.Height;

                        if (screen.Bounds.Contains(left_pos) || screen.Bounds.Contains(right_pos))
                            isPositionValid = true;
                    }

                    //Note that "windowArea" referes to the size of the app's window, and we've halved it
                    //so that we'll pass the check with half the window intersecting with all screens.
                    if (totalIntersectionArea >= windowArea && isPositionValid)
                    {
                        main.Position = left_pos;
                        main.Width = size.Width;
                        main.Height = size.Height;
                        if (size.Maximized)
                            main.WindowState = WindowState.Maximized;
                    }
                }

                desktop.MainWindow = main;
                desktop.Exit += Desktop_Exit;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void Desktop_Exit(object sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            Core.DBStore.CloseDatabase();
            Tools.HWDB.CloseDatabase();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is frmMain main)
            {
                main.Exit();
            }
        }

        private void open86BoxManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Program.Root != null)
                Program.Root.open86BoxManagerToolStripMenuItem_Click(sender, e);
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Program.Root != null)
                Program.Root.settingsToolStripMenuItem_Click(sender, e);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Program.Root != null)
                Program.Root.exitToolStripMenuItem_Click(sender, e);
        }

        private void trayIcon_MouseClick(object sender, EventArgs e) 
        {
            if (Program.Root != null)
                Program.Root.trayIcon_MouseClick(sender, e);
        }
    }
}