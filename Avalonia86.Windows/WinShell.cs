using System;
using System.IO;
using Avalonia86.Common;
using IWshRuntimeLibrary;
using static Avalonia86.Windows.Internal.Win32Imports;

namespace Avalonia86.Windows;

public sealed class WinShell : CommonShell
{
    public override void CreateShortcut(string address, string name, string desc, string startup)
    {
        dynamic shell = new WshShell();
        dynamic shortcut = (IWshShortcut)shell.CreateShortcut(address);
        shortcut.Description = desc;
        shortcut.IconLocation = $"{Path.Combine(startup, "86manager.exe")},0";
        shortcut.TargetPath = Path.Combine(startup, "86manager.exe");
        shortcut.Arguments = $@"-S ""{name}""";
        shortcut.Save();
    }

    public override void PushToForeground(IntPtr hWnd)
    {
        SetForegroundWindow(hWnd);
    }

    public override void PrepareAppId(string appId)
    {
        if (Environment.OSVersion.Version.Major >= 6)
            SetProcessDPIAware();

        SetCurrentProcessExplicitAppUserModelID(appId);
    }

    public override bool SetExecutable(string filePath)
    {
        return true;
    }
}