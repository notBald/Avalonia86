using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _86BoxManager.Linux
{
    public class AppImageInfo
    {
        public readonly string Arch;
        public readonly string Name;
        public readonly string Version;

        public AppImageInfo(string arch, string name, string version)
        {
            Arch = arch;
            Name = name;
            Version = version;
        }

        public override string ToString()
        {
            return $"{Name} {Version} {Arch}";
        }
    }
}
