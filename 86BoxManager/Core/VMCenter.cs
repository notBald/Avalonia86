using _86BoxManager.API;
using _86BoxManager.Common;
using _86BoxManager.Models;
using _86BoxManager.Tools;
using _86BoxManager.ViewModels;
using _86BoxManager.Views;
using _86BoxManager.Xplat;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ButtonsType = MsBox.Avalonia.Enums.ButtonEnum;
using IOPath = System.IO.Path;
using MessageType = MsBox.Avalonia.Enums.Icon;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;

// ReSharper disable InconsistentNaming
namespace _86BoxManager.Core
{
    internal static class VMCenter
    {
        private readonly static Dictionary<string, VMWatch> _watch = new();
        private static AppSettings Sett => Program.Root.Settings;

        public static bool IsWatching => _watch.Count > 0;

        public static void DisposeMe(VMWatch w, string name)
        {
            w.Dispose();
            _watch.Remove(name);
        }

        public async static Task<bool> CloseAllWindows(frmMain ui)
        {
            var vmCount = 0;
            foreach (var w in _watch.Values)
            {
                var vm = w.Tag;
                vmCount += await ForceStop(w, ui);

                try
                {
                    var p = Process.GetProcessById(vm.Pid);
                    // Wait 500 milliseconds for each VM to close
                    p.WaitForExit(500);
                }
                catch { }
            }

            // Wait just a bit to make sure everything goes as planned
            Thread.Sleep(vmCount * 500);

            return true;
        }

        public static (string cfgpath, string exepath) FindPaths()
        {
            var cfgPath = IOPath.Combine(Platforms.Env.UserProfile, "86Box VMs").CheckTrail();
            var exeFolders = Platforms.Env.GetProgramFiles("86Box");
            var exeFound = Platforms.Manager.Find(exeFolders, Platforms.Env.ExeNames);
            if (exeFound == null)
            {
                // The old code did that, so... reproduce
                exeFound = exeFolders.First();
            }
            var exePath = exeFound.CheckTrail();
            return (cfgPath, exePath);
        }

        /// <summary>
        /// Checks if a VM with this name already exists
        /// </summary>
        /// <param name="path">Path to the VM</param>
        /// <param name="parent">Parent window to use for messages</param>
        /// <returns></returns>
        public static async Task<string> CheckIfExists(string path, Window parent)
        {
            try
            {
                return Sett.PathToName(path);
            }
            catch
            {
                await Dialogs.ShowMessageBox("Could not load the virtual machine information from the " +
                                       "registry. Make sure you have the required permissions " +
                                       "and try again.",
                    MessageType.Error, parent, ButtonsType.Ok, "Error");
                return null;
            }
        }

        public static void OpenConfig(VMVisual vm, Window parent)
        {
            try
            {
                var file = IOPath.Combine(vm.Path, "86box.cfg");
                Platforms.Shell.EditFile(file);
            }
            catch
            {
                Dialogs.DispatchMSGBox($@"The config file for the virtual machine ""{vm.Name}"" could" +
                                        " not be opened. Make sure it still exists and that you have " +
                                        "sufficient privileges to access it.",
                    MessageType.Error, parent, ButtonsType.Ok, "Error");
            }
        }

        // Opens the folder containing the selected VM
        public static void OpenFolder(VMVisual vm, Window parent)
        {
            try
            {
                Platforms.Shell.OpenFolder(vm.Path);
            }
            catch
            {
                Dialogs.DispatchMSGBox($@"The folder for the virtual machine ""{vm.Name}"" could" +
                                        " not be opened. Make sure it still exists and that you have " +
                                        "sufficient privileges to access it.",
                    MessageType.Error, parent, ButtonsType.Ok, "Error");
            }
        }

        // Opens the folder containing the selected VM
        public static void OpenScreenshotsFolder(VMVisual vm, Window parent)
        {
            try
            {
                var path = Path.Combine(vm.Path, "screenshots");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                Platforms.Shell.OpenFolder(path);
            }
            catch
            {
                Dialogs.DispatchMSGBox($@"The screenshots folder for the virtual machine ""{vm.Name}"" could" +
                                        " not be opened. Make sure you have " +
                                        "sufficient privileges to access it.",
                    MessageType.Error, parent, ButtonsType.Ok, "Error");
            }
        }

        // Opens the folder containing the selected VM
        public static void OpenTrayFolder(VMVisual vm, Window parent)
        {
            try
            {
                var path = Path.Combine(vm.Path, "printer");
                if (!Directory.Exists(path))
                    throw new Exception("Folder does not exist");

                Platforms.Shell.OpenFolder(path);
            }
            catch
            {
                Dialogs.DispatchMSGBox($@"The screenshots folder for the virtual machine ""{vm.Name}"" could" +
                                        " not be opened. Make sure you have " +
                                        "sufficient privileges to access it.",
                    MessageType.Error, parent, ButtonsType.Ok, "Error");
            }
        }

        // Creates a new VM from the data received and adds it to the listview
        
        public static void Add(string name, string vm_path, string desc, string cat, string icon_path, DateTime created, bool openCFG, bool startVM, Window parent)
        {
            var m = Program.Root.Model;

            var id = Sett.RegisterVM(name, vm_path, icon_path, cat, created);

            var visual = Sett.RefreshVisual(id);
            if (visual == null)
                throw new Exception("Failed to create database entery");
            visual.Desc = desc;

            //Create the folder, but only if it's in the VM folder we manage
            CreateVMFolder(vm_path);

            //First we refersh the categories so that we know they're correct.
            Sett.RefreshCats();

            //Then we set the category equal to the new machine, if we're not showing all machines
            if (m.CategoryIndex != 0)
                m.CategoryName = cat;

            //Then we refresh the machines to ensure the list is correct
            Sett.RefreshVMs();

            //When we created the visual, it got added to the cache, which the notified the
            //list of changes, but did no sorting.

            //Then we set the machine as selected
            m.Machine = visual;

            // Start the VM and/or open settings window if the user chose this option
            if (startVM)
            {
                Start(parent);
            }
            if (openCFG)
            {
                Configure();
            }
            
            CountRefresh();
        }

        private static void CreateVMFolder(string inputFolder)
        {
            inputFolder = Path.GetFullPath(inputFolder);

            // Check if the input folder exists
            if (!Directory.Exists(inputFolder))
            {
                // Check if the input folder is a direct child of CFGdir
                string vm_dir = Sett.CFGdir;
                if (string.IsNullOrWhiteSpace(vm_dir))
                    return;
                vm_dir = Path.GetFullPath(vm_dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string parentDir = FolderHelper.GetParentFolderPath(inputFolder);

                if (parentDir != null)
                {
                    parentDir = parentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (parentDir.Equals(vm_dir, StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if CFGdir exists
                        if (Directory.Exists(vm_dir))
                        {
                            // Create the input folder
                            Directory.CreateDirectory(inputFolder);
                        }
                    }
                }
            }
        } 

        // Deletes the config and nvr of selected VM
        public static async Task<bool> Wipe(VMVisual vm, Window parent)
        {
            var result = await Dialogs.ShowMessageBox(
                "Wiping a virtual machine deletes its configuration" +
                " and nvr files. You'll have to reconfigure the virtual " +
                "machine (and the BIOS if applicable).\n\n Are you sure " +
                @$"you wish to wipe the virtual machine ""{vm.Name}""?",
                MessageType.Warning, Program.Root, ButtonsType.YesNo, "Warning");
            if (result == ResponseType.Yes)
            {
                if (vm.Status != MachineStatus.STOPPED)
                {
                    await Dialogs.ShowMessageBox($@"The virtual machine ""{vm.Name}"" is currently " +
                                            "running and cannot be wiped. Please stop virtual machines " +
                                            "before attempting to wipe them.",
                        MessageType.Error, parent, ButtonsType.Ok, "Success");
                    return false;
                }
                try
                {
                    string p = Path.Combine(vm.Path, "86box.cfg");
                    if (File.Exists(p))
                        File.Delete(p);
                    p = Path.Combine(vm.Path, "nvr");
                    if (Directory.Exists(p))
                        Directory.Delete(p, true);
                    await Dialogs.ShowMessageBox($@"The virtual machine ""{vm.Name}"" was successfully wiped.",
                        MessageType.Info, parent, ButtonsType.Ok, "Success");
                }
                catch (Exception ex)
                {
                    await Dialogs.ShowMessageBox($@"An error occurred trying to wipe the virtual machine ""{vm.Name}"".",
                        MessageType.Error, parent, ButtonsType.Ok, ex.GetType().Name);
                }
            }

            return true;
        }

        // Kills the process associated with the selected VM
        public static async Task<bool> Kill(VMVisual vis, frmMain ui)
        {
            var vm = vis.Tag;

            //Ask the user to confirm
            var result = await Dialogs.ShowMessageBox(
                $@"Killing a virtual machine can cause data loss. " +
                "Only do this if 86Box executable process gets stuck. Do you " +
                @$"really wish to kill the virtual machine ""{vm.Name}""?",
                MessageType.Warning, ui, ButtonsType.YesNo, "Warning");
            if (result == ResponseType.Yes)
            {
                try
                {
                    if (_watch.TryGetValue(vm.Name, out VMWatch w))
                    {
                        w.Dispose();
                        _watch.Remove(vm.Name);
                    }

                    vis.CommitUptime(DateTime.Now);

                    var p = Process.GetProcessById(vm.Pid);
                    p.Kill();
                }
                catch
                {
                    await Dialogs.ShowMessageBox($@"Could not kill 86Box.exe process for virtual " +
                                            @"machine ""{vm.Name}"". The process may have already " +
                                            "ended on its own or access was denied.",
                        MessageType.Error, ui, ButtonsType.Ok, "Could not kill process");
                }

                // We need to cleanup afterwards to make sure the VM is put back into a valid state
                vis.Status = MachineStatus.STOPPED;
                vm.hWnd = IntPtr.Zero;

                vis.RefreshStatus();

                ui.UpdateState();
            }

            CountRefresh();

            return true;
        }

        // Sends a running/pause VM a request to stop without asking the user for confirmation
        private static async Task<int> ForceStop(VMWatch w, frmMain ui)
        {
            int clean_stop = 0;
            var vm = w.Tag;
            try
            {
                Platforms.Manager.GetSender().DoVmForceStop(vm);

                w.Dispose();
                _watch.Remove(vm.Name);

                clean_stop = 1;
            }
            catch (Exception)
            {
                await Dialogs.ShowMessageBox("An error occurred trying to stop the selected virtual machine.",
                    MessageType.Error, ui, ButtonsType.Ok, "Error");
            }

            w.CommitUptime(DateTime.Now);

            CountRefresh();

            return clean_stop;
        }

        // Changes a VM's name and/or description
        public static async Task<bool> Edit(long uid, string name, string desc, string category, string icon, string comment, Window parent)
        {
            var m = Program.Root.Model;
            var current_cat = m.CategoryIndex != 0 ? m.CategoryName : null;
            var selected = m.Machine;
            var is_selected = (selected != null) && selected.Tag.UID == uid;
            VMVisual vm;

            using (var t = Sett.BeginTransaction())
            {
                Sett.EditVM(uid, name, category, icon);
                vm = Sett.RefreshVisual(uid);
                if (vm == null)
                    throw new Exception("Failed to refresh database");


                vm.Desc = desc;
                vm.Comment = comment;

                t.Commit();
            }

            //Since it is possible that the category, icon or name has changed, we ensure the UI is refreshed.
            Sett.RefreshVMs();
            Sett.RefreshCats();

            //The Name and IconPath properties are a little special, so we have to manually tell the UI they've updated.
            vm.RefreshNameAndIcon();
            
            //Ensures that the edited machine continues to be displayed
            if (is_selected && current_cat != null)
            {
                m.CategoryName = category;
                m.Machine = vm;
            }

            await Dialogs.ShowMessageBox($@"Virtual machine ""{name}"" was successfully modified.",
                MessageType.Info, parent, ButtonsType.Ok, "Success");

            //Done for async
            return true;
        }

        // Refreshes the VM counter in the status bar
        public static void CountRefresh()
        {
            var ui = Program.Root;

            var runningVMs = 0;
            var pausedVMs = 0;
            var waitingVMs = 0;
            var stoppedVMs = 0;

            var vms = ui.Model.Machines;
            foreach (var vis in vms)
            {
                switch (vis.Status)
                {
                    case MachineStatus.PAUSED:
                        pausedVMs++;
                        break;
                    case MachineStatus.RUNNING:
                        runningVMs++;
                        break;
                    case MachineStatus.STOPPED:
                        stoppedVMs++;
                        break;
                    case MachineStatus.WAITING:
                        waitingVMs++;
                        break;
                }
            }

            var model = ui.Model;
            model.VmCount = "All VMs: " + vms.Count + " | Running: " + runningVMs + " | Paused: " + pausedVMs +
                            " | Waiting: " + waitingVMs + " | Stopped: " + stoppedVMs;
        }

        // Sends the CTRL+ALT+DEL keystroke to the VM, result depends on the guest OS
        public static void CtrlAltDel(VMVisual vis, frmMain ui)
        {
            if (vis.Status == MachineStatus.RUNNING || vis.Status == MachineStatus.PAUSED)
            {
                Platforms.Manager.GetSender().DoVmCtrlAltDel(vis.Tag);
                vis.Status = MachineStatus.RUNNING;
                ui.UpdateState();
            }
            CountRefresh();
        }

        // Removes the selected VM. Confirmations for maximum safety
        public static async void Remove(VMVisual vm, frmMain ui)
        {
            var result1 = await Dialogs.ShowMessageBox((string)($@"Are you sure you want to remove the" +
                                                    @$" virtual machine ""{vm.Name}""?"),
                MessageType.Warning, ui, ButtonsType.YesNo, "Remove virtual machine");

            if (result1 == ResponseType.Yes)
            {
                if (vm.Status != MachineStatus.STOPPED)
                {
                    await Dialogs.ShowMessageBox((string)($@"Virtual machine ""{vm.Name}"" is currently " +
                                            "running and cannot be removed. Please stop virtual machines" +
                                            " before attempting to remove them."),
                        MessageType.Error, ui, ButtonsType.Ok, "Error");
                    return;
                }

                var vm_path = vm.Path;
                var vm_name = vm.Name;

                try
                {
                    var m = Program.Root.Model;
                    bool is_selected = ReferenceEquals(m.Machine, vm);

                    //Stores away the current category
                    var cat_name = m.CategoryIndex != 0 ? m.CategoryName : null;

                    using (var t = Sett.BeginTransaction())
                    { 
                        //Deletes from the database
                        Sett.RemoveVM(vm.Tag.UID);

                        //Note, the "vm" object is now dead and should not be accessed anymore.
                        vm = null;

                        t.Commit();
                    }

                    //Refreshes the cached data
                    Sett.RefreshVMs();
                    Sett.RefreshCats(); 

                    if (is_selected)
                    {
                        //This will set "All machines" if the category no longer exist
                        m.CategoryName = cat_name;

                        //We're also no longer viewing a machine.
                        m.MachineIndex = -1;
                    }
                }
                catch (Exception ex) // Catches "regkey doesn't exist" exceptions and such
                {
                    await Dialogs.ShowMessageBox((string)(@$"Virtual machine ""{vm_name}"" could not be removed due to " +
                                            $"the following error:\n\n{ex.Message}"),
                        MessageType.Error, ui, ButtonsType.Ok, "Error");
                    return;
                }

                var result2 = await Dialogs.ShowMessageBox((string)($@"Virtual machine ""{vm_name}"" was " +
                                                        "successfully removed. Would you like to delete" +
                                                        " its files as well?"),
                    MessageType.Question, ui, ButtonsType.YesNo, "Virtual machine removed");
                if (result2 == ResponseType.Yes)
                {
                    try
                    {
                        Directory.Delete(vm_path, true);
                    }
                    catch (UnauthorizedAccessException) //Files are read-only or protected by privileges
                    {
                        await Dialogs.ShowMessageBox("86Box Manager was unable to delete the files of this " +
                                                "virtual machine because they are read-only or you don't " +
                                                "have sufficient privileges to delete them.\n\nMake sure " +
                                                "the files are free for deletion, then remove them manually.",
                            MessageType.Error, ui, ButtonsType.Ok, "Error");
                        return;
                    }
                    catch (DirectoryNotFoundException) //Directory not found
                    {
                        await Dialogs.ShowMessageBox("86Box Manager was unable to delete the files of this " +
                                                "virtual machine because they no longer exist.",
                            MessageType.Error, ui, ButtonsType.Ok, "Error");
                        return;
                    }
                    catch (IOException) //Files are in use by another process
                    {
                        await Dialogs.ShowMessageBox("86Box Manager was unable to delete some files of this " +
                                                "virtual machine because they are currently in use by " +
                                                "another process.\n\nMake sure the files are free for " +
                                                "deletion, then remove them manually.",
                            MessageType.Error, ui, ButtonsType.Ok, "Error");
                        return;
                    }
                    catch (Exception ex) //Other exceptions
                    {
                        await Dialogs.ShowMessageBox($"The following error occurred while trying to remove" +
                                                $" the files of this virtual machine:\n\n{ex.Message}",
                            MessageType.Error, ui, ButtonsType.Ok, "Error");
                        return;
                    }
                    await Dialogs.ShowMessageBox($@"Files of virtual machine ""{vm_name}"" were successfully deleted.",
                        MessageType.Info, ui, ButtonsType.Ok, "Virtual machine files removed");
                }
            }

            CountRefresh();
        }

        // Performs a hard reset for the selected VM
        public static void HardReset(VMVisual vis)
        {
            if (vis.Status == MachineStatus.RUNNING || vis.Status == MachineStatus.PAUSED)
            {
                Platforms.Manager.GetSender().DoVmHardReset(vis.Tag);
                Platforms.Shell.PushToForeground(vis.Tag.hWnd);
            }
            CountRefresh();
        }

        // Pauses the selected VM
        public static void Pause(VMVisual vis, frmMain ui)
        {
            Platforms.Manager.GetSender().DoVmPause(vis.Tag);
            vis.Status = MachineStatus.PAUSED;
            vis.RefreshStatus();
            ui.UpdateState();

            CountRefresh();
        }

        // Resumes the selected VM
        public static void Resume(VMVisual vis, frmMain ui)
        {
            Platforms.Manager.GetSender().DoVmResume(vis.Tag);
            vis.Status = MachineStatus.RUNNING;
            vis.RefreshStatus();
            ui.UpdateState();

            CountRefresh();
        }

        private static IExecVars GetExecArgs(frmMain ui, VMVisual vm, string idString)
        {
            var hWndHex = ui.hWndHex;
            var vmPath = vm.Path;
            var exePath = Sett.EXEdir;
            var exeName = Platforms.Shell.DetermineExeName(exePath, Platforms.Env.ExeNames);

            if (vmPath != null)
                vmPath = vmPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var vars = new CommonExecVars
            {
                FileName = IOPath.Combine(exePath, exeName),
                VmPath = vmPath,
                Vm = vm.Tag,
                LogFile = ui.Settings.EnableLogging ? ui.Settings.LogPath : null,
                Handle = idString != null ? (idString, hWndHex) : null
            };
            return vars;
        }

        public static void Start(Window parent)
        {
            var m = Program.Root.Model;
            if (m.MachineIndex != -1)
                Start(m.Machine, parent);
        }

        // Starts the selected VM
        public static void Start(VMVisual vis, Window parent)
        {
            var ui = Program.Root;
            var start_time = DateTime.Now;
            string start_file = "";

            try
            {
                var id = VMWatch.GetTempId(vis);
                var idString = $"{id:X}".PadLeft(16, '0');

                if (vis.Status == MachineStatus.STOPPED)
                {
                    var exec = Platforms.Manager.GetExecutor();
                    var info = exec.BuildStartInfo(GetExecArgs(ui, vis, idString));
                    if (!ui.Settings.ShowConsole)
                    {
                        info.RedirectStandardOutput = true;
                        info.UseShellExecute = false;
                    }

                    start_file = info.FileName;

                    var p = new Process();
                    p.StartInfo.FileName = info.FileName;
                    foreach (var a in info.ArgumentList)
                        p.StartInfo.ArgumentList.Add(a);
                    p.StartInfo.WorkingDirectory = info.WorkingDirectory;
                    if (!p.Start())
                        throw new InvalidOperationException($"Could not start: {info.FileName}");

                    //var p = Process.Start(info);
                    //if (p == null)
                    //    throw new InvalidOperationException($"Could not start: {info.FileName}");

                    vis.Status = MachineStatus.RUNNING;
                    vis.Tag.Pid = p.Id;
                    vis.ClearWaiting();

                    vis.RefreshStatus();

                    // Minimize the main window if the user wants this
                    if (ui.Settings.MinimizeOnVMStart)
                    {
                        ui.Iconify();
                    }
                    vis.IsConfig = false;

                    // Create a new background worker which will wait for the VM's window to
                    // close, so it can update the UI accordingly
                    var bgw = new BackgroundWorker
                    {
                        WorkerReportsProgress = false,
                        WorkerSupportsCancellation = false
                    };
                    var watch = new VMWatch(bgw, vis);
                    _watch.Add(vis.Name, watch);
                    bgw.RunWorkerAsync(vis);

                    ui.UpdateState();

                    using (var t = Sett.BeginTransaction())
                    {
                        vis.RunCount++;
                        vis.SetLastRun(start_time);

                        t.Commit();
                    }
                }
            }
            catch (InvalidOperationException)
            {
                Dialogs.DispatchMSGBox("The process failed to initialize or its window " +
                                       "handle could not be obtained.",
                    MessageType.Error, parent, ButtonsType.Ok, "Error");
            }
            catch (Win32Exception)
            {
                Dialogs.DispatchMSGBox("Cannot find 86Box executable. Make sure your settings " +
                                       $"are correct and try again. ({start_file})",
                    MessageType.Error, parent, ButtonsType.Ok, "Error");
            }
            catch (Exception ex)
            {
                Dialogs.DispatchMSGBox("An error has occurred. Please provide the following " +
                                       $"information to the developer:\n{ex.Message}\n{ex.StackTrace}",
                    MessageType.Error, parent, ButtonsType.Ok, "Error");
            }

            CountRefresh();
        }

        // Sends a running/paused VM a request to stop and asking the user for confirmation
        public static void RequestStop(VMVisual vis, frmMain ui)
        {
            try
            {
                if (vis.Status == MachineStatus.RUNNING || vis.Status == MachineStatus.PAUSED)
                {
                    Platforms.Manager.GetSender().DoVmRequestStop(vis.Tag);
                    Platforms.Shell.PushToForeground(vis.Tag.hWnd);
                }
            }
            catch (Exception)
            {
                Dialogs.DispatchMSGBox("An error occurred trying to stop the selected virtual machine.",
                    MessageType.Error, ui, ButtonsType.Ok, "Error");
            }

            CountRefresh();
        }

        // Opens the settings window for the selected VM
        public static void Configure()
        {
            var ui = Program.Root;

            var vis = ui.Model.Machine;

            // If the VM is already running, only send the message to open the settings window. 
            // Otherwise, start the VM with the -S parameter
            if (vis.Status == MachineStatus.RUNNING || vis.Status == MachineStatus.PAUSED)
            {
                Platforms.Manager.GetSender().DoVmConfigure(vis.Tag);
                Platforms.Shell.PushToForeground(vis.Tag.hWnd);
            }
            else if (vis.Status == MachineStatus.STOPPED)
            {
                try
                {
                    var exec = Platforms.Manager.GetExecutor();
                    var info = exec.BuildConfigInfo(GetExecArgs(ui, vis, null));
                    if (!ui.Settings.ShowConsole)
                    {
                        info.RedirectStandardOutput = true;
                        info.UseShellExecute = false;
                    }

                    var p = new Process();
                    p.StartInfo.FileName = info.FileName;
                    foreach (var a in info.ArgumentList)
                        p.StartInfo.ArgumentList.Add(a);
                    p.StartInfo.WorkingDirectory = info.WorkingDirectory;
                    if (!p.Start())
                        throw new InvalidOperationException($"Could not start: {info.FileName}");

                    //var p = Process.Start(info);
                    //if (p == null)
                    //    throw new InvalidOperationException($"Could not start: {info.FileName}");
                    VMWatch.TryWaitForInputIdle(p, 250);

                    vis.Status = MachineStatus.WAITING;
                    vis.Tag.hWnd = p.MainWindowHandle;
                    vis.Tag.Pid = p.Id;
                    vis.CancelUptime();

                    vis.RefreshStatus();
                    vis.IsConfig = true;

                    var bgw = new BackgroundWorker
                    {
                        WorkerReportsProgress = false,
                        WorkerSupportsCancellation = false
                    };
                    var watch = new VMWatch(bgw, vis);
                    _watch.Add(vis.Name, watch);
                    bgw.RunWorkerAsync(vis);

                    ui.UpdateState();
                }
                catch (Win32Exception)
                {
                    Dialogs.DispatchMSGBox("Cannot find 86Box executable. Make sure your " +
                                           "settings are correct and try again.",
                        MessageType.Error, ui, ButtonsType.Ok, "Error");
                }
                catch (Exception ex)
                {
                    // Revert to stopped status and alert the user
                    vis.Status = MachineStatus.STOPPED;
                    vis.Tag.hWnd = IntPtr.Zero;
                    vis.Tag.Pid = -1;
                    Dialogs.DispatchMSGBox("This virtual machine could not be configured. Please " +
                                           "provide the following information to the developer:\n" +
                                           $"{ex.Message}\n{ex.StackTrace}",
                        MessageType.Error, ui, ButtonsType.Ok, "Error");
                }
            }

            CountRefresh();
        }
    }
}