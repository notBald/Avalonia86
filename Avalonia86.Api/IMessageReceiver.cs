using System;

namespace Avalonia86.API;

public interface IMessageReceiver
{
    void OnEmulatorInit(IntPtr hWnd, uint vmId);
    void OnEmulatorShutdown(IntPtr hWnd);

    void OnVmPaused(IntPtr hWnd);
    void OnVmResumed(IntPtr hWnd);

    void OnDialogOpened(IntPtr hWnd);
    void OnDialogOpened(long uid);
    void OnDialogClosed(IntPtr hWnd);
    void OnDialogClosed(long uid);

    void OnManagerStartVm(string vmName);
}