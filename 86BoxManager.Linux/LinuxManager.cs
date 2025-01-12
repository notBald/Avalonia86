using System.IO;
using System.Linq;
using _86BoxManager.API;
using _86BoxManager.Common;
using _86BoxManager.Unix;
using System;
using DiscUtils.SquashFs;

namespace _86BoxManager.Linux
{
    public sealed class LinuxManager : UnixManager
    {
        private static readonly byte[] AppImageMagicNumber = { 0x41, 0x49, 0x01 };
        private static readonly byte[] SquashFsMagicNumber = { 0x68, 0x73, 0x71, 0x73 }; // "sqsh" in little-endian

        public LinuxManager() : base(GetTmpDir()) { }

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
            if (appImage != null)
            {
                Console.WriteLine("Hello AppImage: "+appImage);

                //For now we read the entire file into memory. This is only proof of concept code.
                try
                {
                    var bytes = File.ReadAllBytes(appImage);
                    Console.WriteLine("Hello bytes: " + bytes.Length);
                    int pos = FindMagicOffset(bytes, AppImageMagicNumber);
                    pos = 0;
                    if (pos != -1)
                    {

                        while(!found_version)
                        {
                            pos = FindMagicOffset(bytes, SquashFsMagicNumber, pos + 4);
                            if (pos == -1)
                                break;
                            Console.WriteLine("Hello squash magic: " + pos);
                            var ms = new OffsetStream(new MemoryStream(bytes), pos);

                            try
                            {
                                var squash = new SquashFileSystemReader(ms);

                                foreach (var name in squash.GetFiles(""))
                                {
                                    if (name.EndsWith("86Box.desktop", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        var file = squash.GetFileInfo(name);

                                        using (var reader = file.OpenRead())
                                        {
                                            using (var sr = new StreamReader(reader, System.Text.Encoding.UTF8))
                                            {
                                                string line;
                                                do
                                                {
                                                    line = sr.ReadLine();

                                                    if (line != null && line.StartsWith("X-AppImage-Version="))
                                                    {
                                                        line = line.Substring("X-AppImage-Version=".Length);
                                                        var chunks = line.Split('-');
                                                        if (chunks.Length == 2)
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
                                                        }
                                                    }
                                                } while (line != null);
                                            }
                                        }

                                        break;
                                    }
                                }
                            } catch { }
                        }
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