using System;
using System.Runtime.InteropServices;
using Avalonia86.API;
using Avalonia86.Windows;
using Avalonia86.Linux;
using Avalonia86.Mac;

namespace Avalonia86.Xplat;

public static class Platforms
{
    public static readonly IShell Shell;
    public static readonly IManager Manager;
    public static readonly IEnv Env;

    static Platforms()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Shell = new LinuxShell();
            Manager = new LinuxManager();
            Env = new LinuxEnv();
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Shell = new MacShell();
            Manager = new MacManager();
            Env = new MacEnv();
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Shell = new WinShell();
            Manager = new WinManager();
            Env = new WinEnv();
            return;
        }

        throw new InvalidOperationException("Not supported OS! Sorry!");
    }

    public static IManager RequestManager(OSPlatform os)
    {
        if (OSPlatform.Linux == os)
            return new LinuxManager();

        return null;
    }
}