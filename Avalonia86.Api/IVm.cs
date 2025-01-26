using System;

// ReSharper disable InconsistentNaming

namespace Avalonia86.API;

public interface IVm
{
    /// <summary>
    /// Name of the virtual machine
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Unique identifier
    /// </summary>
    long UID { get; }

    /// <summary>
    /// Window handle for the VM once it's started
    /// </summary>
    IntPtr hWnd { get; }
    
    /// <summary>
    /// Callback to invoke when VM is gone
    /// </summary>
    Action<IVm> OnExit { set; }
}