using System.Diagnostics;

namespace Avalonia86.API;

public interface IExecutor
{
    ProcessStartInfo BuildStartInfo(IExecVars args);

    ProcessStartInfo BuildConfigInfo(IExecVars args);
}