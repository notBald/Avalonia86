using System.Diagnostics;
using Avalonia86.API;

namespace Avalonia86.Common;

public abstract class CommonExecutor : IExecutor
{
    public virtual ProcessStartInfo BuildStartInfo(IExecVars args)
    {
        var info = new ProcessStartInfo(args.FileName);
        var ops = info.ArgumentList;
        if (!string.IsNullOrWhiteSpace(args.RomPath) && args.Build >= 3333)
        {
            ops.Add("-R");
            ops.Add(args.RomPath);
        }
        if (!string.IsNullOrWhiteSpace(args.LogFile))
        {
            ops.Add("-L");
            ops.Add(args.LogFile);
        }
        ops.Add("-P");
        ops.Add(args.VmPath);
        if (args.Build >= 3333)
        {
            ops.Add("-V");
            ops.Add(args.Vm.Title);
        }
        info.WorkingDirectory = args.VmPath;
        return info;
    }

    public virtual ProcessStartInfo BuildConfigInfo(IExecVars args)
    {
        var info = new ProcessStartInfo(args.FileName);
        var ops = info.ArgumentList;
        ops.Add("--settings");
        ops.Add("-P");
        ops.Add(args.VmPath);
        info.WorkingDirectory = args.VmPath;
        return info;
    }
}