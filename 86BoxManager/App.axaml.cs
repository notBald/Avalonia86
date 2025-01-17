using System;
using System.Diagnostics;
using _86BoxManager.Core;
using _86BoxManager.ViewModels;
using _86BoxManager.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

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
            var s = AppSettings.Settings;
            if (s != null)
                RequestedThemeVariant = s.Theme;

            Me = this;
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var main = new frmMain();

                desktop.MainWindow = main;
                desktop.Exit += Desktop_Exit;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

                if (main.DataContext is MainModel mm)
                {
                    mm.PropertyChanged += Mm_PropertyChanged;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void Mm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainModel.ApplicationTheme))
            {
                var s = AppSettings.Settings;
                if (s != null)
                    RequestedThemeVariant = s.Theme;
            }
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