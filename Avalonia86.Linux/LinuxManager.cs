using Avalonia86.API;
using Avalonia86.Common;
using Avalonia86.Unix;
using Mono.Unix.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
//using X11;

namespace Avalonia86.Linux;

public sealed class LinuxManager : UnixManager
{
    public LinuxManager() : base(GetTmpDir()) { }

    //private static IntPtr display;

    //public override nint RestoreAndFocus(string title, string handleTitle)
    //{
    //    if (IsRunningOnX11())
    //    {
    //        //throw new Exception("Is X11");

    //        display = Xlib.XOpenDisplay(title);
    //        RestoreAndFocus_x11(title, handleTitle);
    //    }

    //    return base.RestoreAndFocus(title, handleTitle);
    //}

    //private static bool IsRunningOnX11()
    //{
    //    return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));
    //}

    //public static Window RestoreAndFocus_x11(string windowTitle, string handleTitle)
    //{
    //    Window windowHandle = Window.None;
    //    var rootWindow = Xlib.XDefaultRootWindow(display);
    //    List<Window> children;

    //    Xlib.XQueryTree(display, rootWindow, ref rootWindow, ref rootWindow, out children);
    //    foreach (var child in children)
    //    {
    //        if (GetWindowTitle_x11(child) == windowTitle)
    //        {
    //            Xlib.XMapWindow(display, child); // Restore the window if minimized
    //            Xlib.XRaiseWindow(display, child); // Bring the window to the foreground
    //            Xlib.XSetInputFocus(display, child, RevertFocus.RevertToPointerRoot, IntPtr.Zero);
    //            windowHandle = child;
    //            break;
    //        }
    //    }

    //    return windowHandle;
    //}

    //private static string GetWindowTitle_x11(Window window)
    //{
    //    string windowName = "";
    //    Xlib.XFetchName(display, window, ref windowName);
    //    return windowName;
    //}

    //public static void Cleanup()
    //{
    //    if (display != IntPtr.Zero)
    //    {
    //        Xlib.XCloseDisplay(display);
    //        display = IntPtr.Zero;
    //    }
    //}


    //private static FileStream lockFile;

    //public override bool IsFirstInstance(string name)
    //{
    //    string exePath = Process.GetCurrentProcess().MainModule.FileName;
    //    string exeName = Path.GetFileNameWithoutExtension(exePath);
    //    string lockFilePath = Path.Combine(Path.GetTempPath(), $"{exeName}.lock");

    //    lockFile = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    //    var lockStruct = new Flock
    //    {
    //        l_type = LockType.F_WRLCK,
    //        l_whence = SeekFlags.SEEK_SET,
    //        l_start = 0,
    //        l_len = 0,
    //        l_pid = Syscall.getpid()
    //    };

    //    return Syscall.fcntl(lockFile.SafeFileHandle.DangerousGetHandle().ToInt32(), FcntlCommand.F_SETLK, ref lockStruct) == 0;
    //}

    public override IVerInfo Get86BoxInfo(Stream file)
    {
        CommonVerInfo ei = null;

        if (AppImageChecker.TryGetAppInfo(file, out var info))
        {
            if (info.Version != null)
            {
                var version = AppImageInfo.ParseVersion(info.Version);
                ei = new CommonVerInfo()
                {
                    FilePrivatePart = version[3],
                    FileMajorPart = version[0],
                    FileMinorPart = version[1],
                    FileBuildPart = version[2],
                    Arch = info.Arch
                };
            }
        }

        return ei;
    }

    public override IVerInfo Get86BoxInfo(string path)
    {
        CommonVerInfo ei = null;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            if (AppImageChecker.TryGetAppInfo(path, out var info))
            {
                if (info.Version != null)
                {
                    var version = AppImageInfo.ParseVersion(info.Version);
                    ei = new CommonVerInfo()
                    {
                        FilePrivatePart = version[3],
                        FileMajorPart = version[0],
                        FileMinorPart = version[1],
                        FileBuildPart = version[2],
                        Arch = info.Arch
                    };
                }
            }

            if (ei == null)
            {
                var full = Path.GetFileNameWithoutExtension(path);
                var split = full.Split('-');
                if (split.Length > 1)
                {
                    var build = split.LastOrDefault();

                    //We try getting it from the filename
                    if (build.StartsWith('b') && build.Length > 2 && int.TryParse(build.AsSpan(1), out int build_nr))
                    {
                        ei = new CommonVerInfo
                        {
                            FilePrivatePart = build_nr,

                            // We can't read the info
                            FileMinorPart = -1,
                            FileMajorPart = -1,
                            FileBuildPart = -1
                        };
                    }
                }
            }
        }
        return ei;
    }

    public override IVerInfo GetBoxVersion(string exeDir)
    {
        if (string.IsNullOrWhiteSpace(exeDir) || !Directory.Exists(exeDir))
        {
            // Not found!
            return null;
        }
        bool found_version = false;

        var info = new CommonVerInfo();
        var exe_name = LinuxShell.DetermineExe(exeDir, ["86Box"]);
        var appImage = Directory.GetFiles(exeDir, exe_name).FirstOrDefault();
        if (appImage != null && AppImageChecker.TryGetAppInfo(appImage, out var app_info))
        {
            Console.WriteLine("Hello AppImage: "+appImage);

            try
            {
                var line = app_info.Version;
                var chunks = line.Split('-');
                if (chunks.Length == 2)
                {
                    do
                    {
                        var nums = chunks[0].Split('.');
                        {
                            if (nums.Length == 3 && chunks[1].StartsWith('b') && int.TryParse(chunks[1].AsSpan(1), out int build_nr) &&
                                int.TryParse(nums[0], out int major) &&
                                int.TryParse(nums[1], out int minor) &&
                                int.TryParse(nums[2], out int very_minor))
                            {
                                info.FileMajorPart = major;
                                info.FileMinorPart = minor;
                                info.FileBuildPart = very_minor;
                                info.FilePrivatePart = build_nr;

                                found_version = true;

                                break;
                            }
                        }
                        {
                            if (nums.Length == 2 && chunks[1].StartsWith('b') && int.TryParse(chunks[1].AsSpan(1), out int build_nr) &&
                                int.TryParse(nums[0], out int major) &&
                                int.TryParse(nums[1], out int minor))
                            {
                                info.FileMajorPart = major;
                                info.FileMinorPart = minor;
                                info.FileBuildPart = 0;
                                info.FilePrivatePart = build_nr;

                                found_version = true;

                                break;
                            }
                        }
                    } while (false);
                }
                                                
            }
            catch { }

            if (!found_version)
            {
                var full = Path.GetFileNameWithoutExtension(appImage);
                var split = full.Split('-');
                if (split.Length > 1)
                {
                    var build = split.LastOrDefault();

                    if (build.StartsWith('b') && build.Length > 2 && int.TryParse(build.AsSpan(1), out int build_nr))
                    {
                        info.FilePrivatePart = build_nr;

                        // HACK: Set version because we can't read the ELF version
                        info.FileMinorPart = 11;
                        info.FileMajorPart = 3;
                        info.FileBuildPart = 0;
                    }
                }
            }
            else
            {
                Console.WriteLine("Hello AppImage version: "+info.ToString());
            }
        }
        return info;
    }

    public static string GetTmpDir() => "/tmp";

    public static int FindMagicOffset(byte[] data, byte[] magicNumber, int pos = 0)
    {
        for (int i = pos; i <= data.Length - magicNumber.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < magicNumber.Length; j++)
            {
                if (data[i + j] != magicNumber[j])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return i; // Return the starting position of the magic number
            }
        }

        return -1; // Return -1 if the magic number is not found
    }
}