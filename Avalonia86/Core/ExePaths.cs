using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia86.Core;

public class ExePaths
{
    public readonly string ExePath;
    public string RomPath;
    public readonly string Arch;
    public readonly long Build;

    public ExePaths(string exe, string rom, string build, string arch)
    {
        ExePath = exe;
        RomPath = rom;
        Arch = arch;
        long.TryParse(build, out Build);
    }
}
