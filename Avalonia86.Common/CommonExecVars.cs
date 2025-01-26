using Avalonia86.API;

namespace Avalonia86.Common;

public sealed class CommonExecVars : IExecVars
{
    public string FileName { get; set; }

    public string RomPath { get; set; }

    public string LogFile { get; set; }

    public string VmPath { get; set; }

    public long? Build {  get; set; }

    public string Arch {  get; set; }

    public IVm Vm { get; set; }

    public (string id, string hWnd)? Handle { get; set; }
}