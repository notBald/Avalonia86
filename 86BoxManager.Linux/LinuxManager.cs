using System.IO;
using System.Linq;
using _86BoxManager.API;
using _86BoxManager.Common;
using _86BoxManager.Unix;
using System;

namespace _86BoxManager.Linux
{
    public sealed class LinuxManager : UnixManager
    {
        public LinuxManager() : base(GetTmpDir()) { }

        public override IVerInfo GetBoxVersion(string exeDir)
        {
            if (string.IsNullOrWhiteSpace(exeDir) || !Directory.Exists(exeDir))
            {
                // Not found!
                return null;
            }
            var info = new CommonVerInfo();
            var appImage = Directory.GetFiles(exeDir, "86Box-*").FirstOrDefault();
            if (appImage != null)
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
            return info;
        }

        public static string GetTmpDir() => "/tmp";
    }
}