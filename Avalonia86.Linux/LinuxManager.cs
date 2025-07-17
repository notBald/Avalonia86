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

    public override IVerInfo Get86BoxInfo(string path, out bool bad_image)
    {
        bad_image = false;

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
        }
        return info;
    }

    public static string GetTmpDir() => "/tmp";
}