using System.IO;
using System.Linq;
using _86BoxManager.API;
using _86BoxManager.Common;
using _86BoxManager.Unix;
using System;
using Mono.Unix;
using System.Reflection;
using System.ComponentModel.DataAnnotations;

namespace _86BoxManager.Linux
{
    public sealed class LinuxManager : UnixManager
    {
        public LinuxManager() : base(GetTmpDir()) { }

        public override ExeInfo Get86BoxInfo(string path)
        {
            var ei = new ExeInfo();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                if (AppImageChecker.TryGetAppInfo(path, out var info))
                {
                    if (info.Version != null)
                    {
                        var version = AppImageInfo.ParseVersion(info.Version);
                        ei.VerInfo = new CommonVerInfo()
                        {
                            FilePrivatePart = version[3],
                            FileMajorPart = version[0],
                            FileMinorPart = version[1],
                            FileBuildPart = version[2]
                        };
                    }
                }

                if (ei.VerInfo == null)
                {
                    var full = Path.GetFileNameWithoutExtension(path);
                    var split = full.Split('-');
                    if (split.Length > 1)
                    {
                        var build = split.LastOrDefault();

                        //We try getting it from the filename
                        if (build.StartsWith('b') && build.Length > 2 && int.TryParse(build.AsSpan(1), out int build_nr))
                        {
                            ei.VerInfo = new CommonVerInfo
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
            var appImage = Directory.GetFiles(exeDir, "86Box*").FirstOrDefault();
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
}