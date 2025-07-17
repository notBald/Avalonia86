using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Threading;
using Avalonia86.API;
using Avalonia86.Common;
using Avalonia86.Windows.Internal;
using static Avalonia86.Windows.Internal.Win32Imports;

namespace Avalonia86.Windows;

public sealed class WinManager : CommonManager, IManager
{
    private static Mutex mutex = null;

    public override bool IsFirstInstance(string name)
    {
        //Use a mutex to check if this is the first instance of Manager
        mutex = new Mutex(true, name, out var firstInstance);
        return firstInstance;
    }

    public override IntPtr RestoreAndFocus(string windowTitle, string handleTitle)
    {
        //Finds the existing window, unhides it, restores it and sets focus to it
        var hWnd = FindWindow(null, windowTitle);
        ShowWindow(hWnd, ShowWindowEnum.Show);
        ShowWindow(hWnd, ShowWindowEnum.Restore);
        SetForegroundWindow(hWnd);

        hWnd = FindWindow(null, handleTitle);
        return hWnd;
    }

    protected override bool IsExecutable(FileInfo fileInfo)
    {
        if (fileInfo == null)
            return false;

        string[] executableExtensions = { ".exe", ".bat", ".cmd", ".com" };
        string fileExtension = fileInfo.Extension.ToLower();

        foreach (string extension in executableExtensions)
        {
            if (fileExtension == extension)
                return true;
        }

        return false;
    }

    public override IVerInfo Get86BoxInfo(string path, out bool bad_image)
    {
        bad_image = false;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var vi = new WinVerInfo(FileVersionInfo.GetVersionInfo(path));

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new PEReader(stream))
                {
                    try
                    {
                        var headers = reader.PEHeaders;
                        vi.Arch = headers.CoffHeader.Machine.ToString();
                    } 
                    catch (BadImageFormatException)
                    {
                        bad_image = true;
                        return null;
                    }
                }
            }

            return vi;
        }
        return null;
    }

    public override IVerInfo GetBoxVersion(string exeDir)
    {
        var exePath = Path.Combine(exeDir, "86Box.exe");
        if (!File.Exists(exePath))
        {
            // Not found!
            return null;
        }
        var vi = FileVersionInfo.GetVersionInfo(exePath);
        return new WinVerInfo(vi);
    }

    public override IMessageLoop GetLoop(IMessageReceiver callback)
    {
        var loop = new WinLoop(callback);
        return loop;
    }

    public override IMessageSender GetSender()
    {
        var loop = new WinLoop(null);
        return loop;
    }

    public override IExecutor GetExecutor()
    {
        var exec = new WinExecutor();
        return exec;
    }
}