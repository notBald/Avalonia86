using System;
using _86BoxManager.API;
using _86BoxManager.Models;
using _86BoxManager.Tools;
using _86BoxManager.ViewModels;
using Avalonia.Threading;
using ButtonsType = MsBox.Avalonia.Enums.ButtonEnum;
using MessageType = MsBox.Avalonia.Enums.Icon;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;

// ReSharper disable InconsistentNaming
namespace _86BoxManager.Core
{
    internal sealed class VMHandler : IMessageReceiver
    {
        public void OnEmulatorInit(IntPtr hWnd, uint vmId)
        {
            var dc = (MainModel)Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var lvi in items)
            {
                var vm = lvi.Tag;
                var id = VMWatch.GetTempId(lvi);
                if (id != vmId)
                    continue;

                vm.hWnd = hWnd;
                break;
            }
        }

        public void OnEmulatorShutdown(IntPtr hWnd)
        {
            var dc = (MainModel) Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var vis in items)
            {
                var vm = vis.Tag;
                if (!vm.hWnd.Equals(hWnd) || vis.Status == MachineStatus.STOPPED)
                    continue;

                vis.Status = MachineStatus.STOPPED;
                vm.hWnd = IntPtr.Zero;
                vis.RefreshStatus();

                Program.Root.UpdateState();
            }
            VMCenter.CountRefresh();
        }

        public void OnVmPaused(IntPtr hWnd)
        {
            var dc = (MainModel)Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var vis in items)
            {
                var vm = vis.Tag;
                if (!vm.hWnd.Equals(hWnd) || vis.Status == MachineStatus.PAUSED)
                    continue;

                vis.Status = MachineStatus.PAUSED;
                vis.RefreshStatus();
                Program.Root.UpdateState();
            }
            VMCenter.CountRefresh();
        }

        public void OnVmResumed(IntPtr hWnd)
        {
            var dc = (MainModel)Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var vis in items)
            {
                var vm = vis.Tag;
                if (!vm.hWnd.Equals(hWnd) || vis.Status == MachineStatus.RUNNING)
                    continue;

                vis.Status = MachineStatus.RUNNING;
                vis.RefreshStatus();
                Program.Root.UpdateState();
            }
            VMCenter.CountRefresh();
        }

        public void OnDialogOpened(IntPtr hWnd)
        {
            var dc = (MainModel)Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var vis in items)
            {
                var vm = vis.Tag;
                if (!vm.hWnd.Equals(hWnd) || vis.Status == MachineStatus.WAITING)
                    continue;

                vis.Status = MachineStatus.WAITING;
                vis.RefreshStatus();
                Program.Root.UpdateState();
            }
            VMCenter.CountRefresh();
        }

        public void OnDialogClosed(IntPtr hWnd)
        {
            var dc = (MainModel)Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var vis in items)
            {
                var vm = vis.Tag;
                if (!vm.hWnd.Equals(hWnd) || vis.Status == MachineStatus.RUNNING)
                    continue;

                vis.Status = vis.IsPaused ? MachineStatus.PAUSED : MachineStatus.RUNNING;
                vis.RefreshStatus();
                Program.Root.UpdateState();
            }
            VMCenter.CountRefresh();
        }

        public void OnManagerStartVm(string vmName)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                OnManagerStartVmInternal(vmName);
                return;
            }
            DispatcherPriority lvl = DispatcherPriority.Background;
            Dispatcher.UIThread.Post(() => OnManagerStartVmInternal(vmName), lvl);
        }

        private void OnManagerStartVmInternal(string vmName)
        {
            var ui = Program.Root;
            var lstVMs = ui.lstVMs;

            var ids = AppSettings.Settings.NameToIds(vmName);

            // This check is necessary in case the specified VM was already removed but the shortcut remains
            if (ids != null && ids.Length > 0)
            {
                var vis = AppSettings.Settings.RefreshVisual(ids[0]);

                // If the VM is already running, display a message, otherwise, start it
                if (vis.Status != MachineStatus.STOPPED)
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await Dialogs.ShowMessageBox($@"The virtual machine ""{vmName}"" is already running.",
                                MessageType.Info, null, ButtonsType.Ok, "Virtual machine already running");
                    });
                }
                else
                {
                    VMCenter.Start(vis, ui);

                    ui.Model.Machine = vis;
                }
                return;
            }

            Dispatcher.UIThread.Post(async () =>
            {
                await Dialogs.ShowMessageBox($@"The virtual machine ""{vmName}"" could not be found. " +
                       "It may have been removed or the specified name is incorrect.",
                        MessageType.Error, null, ButtonsType.Ok, "Virtual machine not found");
            });
        }
    }
}