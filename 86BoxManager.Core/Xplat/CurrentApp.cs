using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace _86BoxManager.Xplat;

public static class CurrentApp
{
    public static bool IsLinux { get; private set; }
    public static bool IsWindows { get; private set; }
    public static bool IsAppImage { get; private set; }
    public static string TrueStartupPath { get; private set; }

    public static string ProductVersion { get; } = ReadVersion();
    public static string VersionString
    {
        get
        {
            var txt = ProductVersion.Substring(0, ProductVersion.Length - 2);
#if DEBUG
            txt += " (Debug)";
#endif
            return txt;
        }
    }

    public static string StartupPath { get; } = ReadStartup();

    static CurrentApp()
    {
        IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        try
        {
            if (IsLinux)
            {
                string appImagePath = Environment.GetEnvironmentVariable("APPIMAGE");
                if (!string.IsNullOrEmpty(appImagePath))
                {
                    string appImageDirectory = Path.GetDirectoryName(appImagePath);
                    string currentDirectory = Directory.GetCurrentDirectory();

                    bool isReadOnly = new DirectoryInfo(currentDirectory).Attributes.HasFlag(FileAttributes.ReadOnly);

                    IsAppImage = appImageDirectory != currentDirectory && isReadOnly;
                    TrueStartupPath = appImageDirectory;
                }
            }
        }
        catch { }

        if (TrueStartupPath == null)
            TrueStartupPath = StartupPath;
    }

    private static Assembly GetEntryAss()
    {
        return Assembly.GetEntryAssembly() ?? typeof(CurrentApp).Assembly;
    }

    private static string ReadStartup()
    {
        //var ass = GetEntryAss();
        //var path = Path.GetFullPath(ass.Location);
        //var dir = Path.GetDirectoryName(path);
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static string ReadVersion()
    {
        var ass = GetEntryAss();
        var ver = ass.GetName().Version;
        return ver?.ToString();
    }
}