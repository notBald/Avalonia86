using System;
using System.IO;
using _86BoxManager.API;

namespace _86BoxManager.Linux
{
    public sealed class LinuxEnv : IEnv
    {
        public string[] ExeNames { get => new[] { "86Box" }; }
        public string MyComputer { get => Environment.GetFolderPath(Environment.SpecialFolder.MyComputer); }
        public string UserProfile { get => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
        public string MyDocuments 
        { 
            get
            {
                var fakeDoc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(fakeDoc, "Documents");
            }
        }
        public string Desktop { get => Environment.GetFolderPath(Environment.SpecialFolder.Desktop); }

        public string[] GetProgramFiles(string appName)
        {
            var folders = new[]
            {
                Path.Combine(UserProfile, "Portable", appName),
                Path.Combine("/opt", appName),
                "/usr/local/bin",
                "/usr/bin"
            };
            return folders;
        }
    }
}