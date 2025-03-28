namespace Avalonia86.API;

public interface IExecVars
{
    string FileName { get; }

    string RomPath { get; }

    string LogFile { get; }

    string VmPath { get; }

    long? Build { get; }

    string Arch { get; }

    IVm Vm { get; }

    (string id, string hWnd)? Handle { get; }
}