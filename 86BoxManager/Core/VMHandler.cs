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

            foreach (var lvi in items)
            {
                var vm = lvi.Tag;
                if (!vm.hWnd.Equals(hWnd) || vm.Status == VM.STATUS_STOPPED)
                    continue;

                vm.Status = VM.STATUS_STOPPED;
                vm.hWnd = IntPtr.Zero;
                lvi.RefreshStatus(0);

                Program.Root.UpdateState();
            }
            VMCenter.CountRefresh();
        }

        public void OnVmPaused(IntPtr hWnd)
        {
            var dc = (MainModel)Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var lvi in items)
            {
                var vm = lvi.Tag;
                if (!vm.hWnd.Equals(hWnd) || vm.Status == VM.STATUS_PAUSED)
                    continue;

                vm.Status = VM.STATUS_PAUSED;
                lvi.RefreshStatus(2);
                Program.Root.UpdateState();
            }
            VMCenter.CountRefresh();
        }

        public void OnVmResumed(IntPtr hWnd)
        {
            var dc = (MainModel)Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var lvi in items)
            {
                var vm = lvi.Tag;
                if (!vm.hWnd.Equals(hWnd) || vm.Status == VM.STATUS_RUNNING)
                    continue;

                vm.Status = VM.STATUS_RUNNING;
                lvi.RefreshStatus(1);
                Program.Root.UpdateState();
            }
            VMCenter.CountRefresh();
        }

        public void OnDialogOpened(IntPtr hWnd)
        {
            var dc = (MainModel)Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var lvi in items)
            {
                var vm = lvi.Tag;
                if (!vm.hWnd.Equals(hWnd) || vm.Status == VM.STATUS_WAITING)
                    continue;

                vm.Status = VM.STATUS_WAITING;
                lvi.RefreshStatus(2);
                Program.Root.UpdateState();
            }
            VMCenter.CountRefresh();
        }

        public void OnDialogClosed(IntPtr hWnd)
        {
            var dc = (MainModel)Program.Root.DataContext;
            var items = dc.AllMachines;

            foreach (var lvi in items)
            {
                var vm = lvi.Tag;
                if (!vm.hWnd.Equals(hWnd) || vm.Status == VM.STATUS_RUNNING)
                    continue;

                vm.Status = vm.IsPaused ? VM.STATUS_PAUSED : VM.STATUS_RUNNING;
                lvi.RefreshStatus(1);
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

            var ids = ui.Settings.NameToIds(vmName);

            // This check is necessary in case the specified VM was already removed but the shortcut remains
            if (ids != null && ids.Length > 0)
            {
                var lvi = ui.Settings.RefreshVisual(ids[0]);

                var vm = lvi.Tag;

                // If the VM is already running, display a message, otherwise, start it
                if (vm.Status != VM.STATUS_STOPPED)
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await Dialogs.ShowMessageBox($@"The virtual machine ""{vmName}"" is already running.",
                                MessageType.Info, null, ButtonsType.Ok, "Virtual machine already running");
                    });
                }
                else
                {
                    VMCenter.Start(lvi, ui);

                    ui.Model.Machine = lvi;
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