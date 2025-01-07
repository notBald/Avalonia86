using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using _86BoxManager.Tools;
using _86BoxManager.Models;
using _86BoxManager.ViewModels;
using ButtonsType = MsBox.Avalonia.Enums.ButtonEnum;
using MessageType = MsBox.Avalonia.Enums.Icon;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;

// ReSharper disable InconsistentNaming

namespace _86BoxManager.Core
{
    public sealed class VMWatch
    {
        private readonly BackgroundWorker _bgw;
        private readonly VMVisual _vis;

        public VM Tag { get => _vis.Tag; }

        internal VMWatch(BackgroundWorker bgw, VMVisual tag)
        {
            _bgw = bgw;
            _bgw.DoWork += background_DoWork;
            _bgw.RunWorkerCompleted += background_RunCompleted;
            _vis = tag;
        }

        public void Dispose()
        {
            _bgw.DoWork -= background_DoWork;
            _bgw.RunWorkerCompleted -= background_RunCompleted;
            _bgw.Dispose();
        }

        public void CommitUptime(DateTime d) => _vis.CommitUptime(d);

        // Wait for the associated window of a VM to close
        private async void background_DoWork(object sender, DoWorkEventArgs e)
        {
            var vm = e.Argument as VM;
            try
            {
                // Find the process associated with the VM
                var p = Process.GetProcessById(vm.Pid);

                // Wait for it to exit
                p.WaitForExit();
            }
            catch (Exception ex)
            {
                await Dialogs.ShowMessageBox("An error has occurred. Please provide the following details" +
                                       $" to the developer:\n{ex.Message}\n{ex.StackTrace}",
                    MessageType.Error, null, ButtonsType.Ok, "Error");
            }
            e.Result = vm;
        }

        // Update the UI once the VM's window is closed
        private void background_RunCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var Now = DateTime.Now;

            var ui = Program.Root;
            var lstVMs = ui.lstVMs;
            var vm = e.Result as VM;

            _vis.CommitUptime(Now);

            var allItems = ui.Model.AllMachines;
            var selected = ui.Model.Machine;

            // Go through the listview, find the item representing the VM and update things accordingly
            foreach (var item in allItems)
            {
                if (ReferenceEquals(item.Tag, vm))
                {
                    vm.Status = VM.STATUS_STOPPED;
                    vm.hWnd = IntPtr.Zero;
                    item.RefreshStatus(vm.Status);

                    if (vm.OnExit != null)
                    {
                        vm.OnExit(vm);
                        vm.OnExit = null;
                    }

                    if (ReferenceEquals(selected, item))
                    {
                        ui.UpdateState();
                    }
                }
            }

            VMCenter.CountRefresh();
            VMCenter.DisposeMe(this, vm.Name);
        }

        public static bool TryWaitForInputIdle(Process process, int forceDelay)
        {
            try
            {
                return process.WaitForInputIdle();
            }
            catch (InvalidOperationException)
            {
                Thread.Sleep(forceDelay);
                return false;
            }
        }

        internal static uint GetTempId(VMVisual vm)
        {
            /* This generates a VM ID on the fly from the VM path. The reason it's done this way is
                 * it doesn't break existing VMs and doesn't require extensive modifications to this
                 * legacy version for it to work with newer 86Box versions...
                 * IDs also have to be unsigned for 86Box, but GetHashCode() returns signed and result
                 * can be negative, so shift it up by int.MaxValue to ensure it's always positive. */

            var tempid = vm.Path.GetHashCode();
            uint id;

            if (tempid < 0)
                id = (uint)(tempid + int.MaxValue);
            else
                id = (uint)tempid;

            return id;
        }
    }
}