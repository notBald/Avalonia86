using System;
using System.IO;
using System.Text;
using Avalonia86.Common;
using Mono.Unix;
using Mono.Unix.Native;

namespace Avalonia86.Linux;

public sealed class LinuxShell : CommonShell
{
    public override void CreateShortcut(string address, string name, string desc, string startup)
    {
        var fileName = address.Replace(".lnk", ".desktop");
        var myExe = Path.Combine(startup, "86Manager");
        var myIcon = Path.Combine(startup, "Resources", "86Box-gray.svg");
        var lines = new[]
        {
            "[Desktop Entry]",
            "Version=1.0",
            "Type=Application",
            $"Name={name}",
            @$"Exec=""{myExe}"" -S ""{name}""",
            $"Icon={myIcon}",
            $"Comment={desc}",
            "Terminal=false",
            "Categories=Game;Emulator;",
            "StartupWMClass=86box-vm",
            "StartupNotify=true"
        };
        var bom = new UTF8Encoding(false);
        File.WriteAllLines(fileName, lines, bom);
    }

    public override string DetermineExeName(string path, string[] exeNames)
    {
        return DetermineExe(path, exeNames);
    }

    internal static string DetermineExe(string path, string[] exeNames)
    {
        var di = new DirectoryInfo(path);
        var fileStat = new Stat();
        FileInfo newestExe = null;

        foreach (var exeName in exeNames)
        {
            foreach (var exe in di.GetFiles(exeName + "*"))
            {
                Syscall.stat(exe.FullName, out fileStat);

                bool isExecutable = (fileStat.st_mode & FilePermissions.S_IXUSR) != 0 ||
                                    (fileStat.st_mode & FilePermissions.S_IXGRP) != 0 ||
                                    (fileStat.st_mode & FilePermissions.S_IXOTH) != 0;

                if (isExecutable)
                {
                    if (newestExe == null || exe.LastWriteTime > newestExe.LastWriteTime)
                    {
                        newestExe = exe;
                    }
                }
            }
        }

        return newestExe?.Name ?? "86Box";
    }

    public override bool SetExecutable(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        UnixFileInfo fileInfo = new UnixFileInfo(filePath);
        fileInfo.FileAccessPermissions |= FileAccessPermissions.UserExecute | FileAccessPermissions.GroupExecute | FileAccessPermissions.OtherExecute;

        return true;
    }
}