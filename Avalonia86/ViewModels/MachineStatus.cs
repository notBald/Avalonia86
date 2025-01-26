using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia86.ViewModels;

public enum MachineStatus
{
    /// <summary>
    /// VM is not running
    /// </summary>
    STOPPED,

    /// <summary>
    /// VM is running
    /// </summary>
    RUNNING,

    /// <summary>
    /// VM is waiting for user response
    /// </summary>
    WAITING,

    /// <summary>
    /// VM is paused
    /// </summary>
    PAUSED
}
