using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia86.API;
using Avalonia86.Common;
using Avalonia86.DialogBox;
using Avalonia86.Tools;
using Avalonia86.ViewModels;
using Avalonia86.Views;
using Avalonia86.Xplat;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IOPath = System.IO.Path;

// ReSharper disable InconsistentNaming
namespace Avalonia86.Core;

internal static class VMCenter
{
    private readonly static Dictionary<long, VMWatch> _watch = new();
    private static AppSettings Sett => AppSettings.Settings;

    public static bool IsWatching => _watch.Count > 0;

    public static void DisposeMe(VMWatch w, long uid)
    {
        w.Dispose();
        _watch.Remove(uid);
    }

    public async static Task<bool> CloseAllWindows(frmMain ui)
    {
        var vmCount = 0;
        foreach (var w in _watch.Values)
        {
            var vm = w.Tag;

            //Don't try to force stop waiting machines. It causes trouble more often than not.
            if (w.Vis.Status == MachineStatus.WAITING)
                continue;

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
        var exeFound = Platforms.Manager.FindFolderFor86Box(exeFolders, Platforms.Env.ExeNames);
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
        catch (Exception e)
        {
            await parent.ShowError("Could not load the virtual machine information from the " +
                                   "registry. Make sure you have the required permissions " +
                                   "and try again.", e);
            return null;
        }
    }

    public static void OpenConfig(VMVisual vm, Window ui)
    {
        try
        {
            var file = IOPath.Combine(vm.Path, "86box.cfg");
            Platforms.Shell.EditFile(file);
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await ui.ShowError($@"The config file for the virtual machine ""{vm.Name}"" could" +
                                     " not be opened. Make sure it still exists and that you have " +
                                     "sufficient privileges to access it.", e);
            });
        }
    }

    // Opens the folder containing the selected VM
    public static void OpenFolder(VMVisual vm, Window ui)
    {
        try
        {
            Platforms.Shell.OpenFolder(vm.Path);
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await ui.ShowError($@"The folder for the virtual machine ""{vm.Name}"" could" +
                                     " not be opened. Make sure it still exists and that you have " +
                                     "sufficient privileges to access it.", e);
            });
        }
    }

    // Opens the folder containing the selected VM
    public static void OpenScreenshotsFolder(VMVisual vm, Window ui)
    {
        try
        {
            var path = Path.Combine(vm.Path, "screenshots");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            Platforms.Shell.OpenFolder(path);
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await ui.ShowError($@"The screenshots folder for the virtual machine ""{vm.Name}"" could" +
                                     " not be opened. Make sure you have " +
                                     "sufficient privileges to access it.", e);
            });
        }
    }

    // Opens the folder containing the selected VM
    public static void OpenTrayFolder(VMVisual vm, Window ui)
    {
        try
        {
            var path = Path.Combine(vm.Path, "printer");
            if (!Directory.Exists(path))
                throw new Exception("Folder does not exist");

            Platforms.Shell.OpenFolder(path);
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await ui.ShowError($@"The screenshots folder for the virtual machine ""{vm.Name}"" could" +
                                     " not be opened. Make sure you have " +
                                     "sufficient privileges to access it.", e);
            });
        }
    }

    // Creates a new VM from the data received and adds it to the listview
    
    public static void Add(string name, string vm_path, string desc, string cat, string icon_path, DateTime created, long? exe_id, bool openCFG, bool startVM, Window parent)
    {
        var m = Program.Root.Model;

        var id = Sett.RegisterVM(name, vm_path, icon_path, cat, created, exe_id);

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
        var result = await parent.ShowQuestion(
            "Wiping a virtual machine deletes its configuration" +
            " and nvr files. You'll have to reconfigure the virtual " +
            "machine (and the BIOS if applicable).\n\n Are you sure " +
            @$"you wish to wipe the virtual machine ""{vm.Name}""?", $"Wipe {vm.Name}", "Delete configuration and BIOS");
        if (result == DialogResult.Yes)
        {
            if (vm.Status != MachineStatus.STOPPED)
            {
                await parent.ShowError($@"The virtual machine ""{vm.Name}"" is currently " +
                                         "running and cannot be wiped.", $"{vm.Name} is running");
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
                await parent.ShowMsg($@"The virtual machine ""{vm.Name}"" was successfully wiped.");
            }
            catch (Exception e)
            {
                await parent.ShowError($@"An error occurred trying to wipe the virtual machine ""{vm.Name}"".", e);
            }
        }

        return true;
    }

    // Kills the process associated with the selected VM
    public static async Task<bool> Kill(VMVisual vis, frmMain ui)
    {
        var vm = vis.Tag;

        //Ask the user to confirm
        var result = await ui.ShowQuestion(
            $@"Killing a virtual machine can cause data loss. " +
            "Only do this if 86Box executable process gets stuck. Do you " +
            @$"really wish to kill the virtual machine ""{vm.Name}""?", $"Halt {vm.Name}", "Force the machine to stop");
        if (result == DialogResult.Yes)
        {
            try
            {
                if (_watch.TryGetValue(vm.UID, out VMWatch w))
                {
                    w.Dispose();
                    _watch.Remove(vm.UID);
                }

                vis.CommitUptime(DateTime.Now);

                var p = Process.GetProcessById(vm.Pid);
                p.Kill();
            }
            catch (Exception e)
            {
                await ui.ShowError($@"Could not kill 86Box.exe process for virtual " +
                                    @"machine ""{vm.Name}"". The process may have already " +
                                     "ended on its own or access was denied.", e, "Could not kill process");
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
            _watch.Remove(vm.UID);

            clean_stop = 1;
        }
        catch (Exception e)
        {
            await ui.ShowError("An error occurred trying to stop the selected virtual machine.", e);
        }

        w.CommitUptime(DateTime.Now);

        CountRefresh();

        return clean_stop;
    }

    /// <summary>
    /// Changes a VM's name and/or description
    /// </summary>
    public static void Edit(long uid, string name, string new_folder, string desc, string category, string icon, string comment, long? exe_id, Window parent)
    {
        var m = Program.Root.Model;
        var current_cat = m.CategoryIndex != 0 ? m.CategoryName : null;
        var selected = m.Machine;
        var is_selected = (selected != null) && selected.Tag.UID == uid;
        VMVisual vm;
        bool rename = Sett.RenameFolders;

        using (var t = Sett.BeginTransaction())
        {
            Sett.EditVM(uid, name, category, icon, exe_id);
            vm = Sett.RefreshVisual(uid);
            if (vm == null)
                throw new Exception("Failed to refresh database");

            vm.Desc = desc;
            vm.Comment = comment;

            if (rename && !Directory.Exists(new_folder))
            {
                try
                {
                    Directory.Move(vm.Path, new_folder);
                    vm.Path = new_folder;
                }
                catch { rename = false; }
            }

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

        if (rename)
            vm.RaisePropertyChanged(nameof(VMVisual.Path));
    }

    public static (string, IVerInfo) GetPathExeInfo()
    {
        var m = Platforms.Manager;
        if (m != null)
        {
            var files = m.List86BoxExecutables(Sett.EXEdir);
            if (files != null)
            {
                foreach (var exe in files)
                {
                    if (m.IsExecutable(exe))
                    {
                        return (exe, m.Get86BoxInfo(exe));
                    }
                }
            }
        }

        return ("", null);
    }

    // Refreshes the VM counter in the status bar
    public static void CountRefresh()
    {
        var ui = Program.Root;

        var total = 0;
        var runningVMs = 0;
        var allRunningVMs = 0;
        var pausedVMs = 0;
        var waitingVMs = 0;
        var stoppedVMs = 0;

        var vms = ui.Model.AllMachines;
        foreach (var vis in vms)
        {
            total++;

            switch (vis.Status)
            {
                case MachineStatus.PAUSED:
                    pausedVMs++;
                    allRunningVMs++;
                    break;
                case MachineStatus.RUNNING:
                    runningVMs++;
                    allRunningVMs++;
                    break;
                case MachineStatus.STOPPED:
                    stoppedVMs++;
                    break;
                case MachineStatus.WAITING:
                    waitingVMs++;
                    allRunningVMs++;
                    break;
            }
        }

        var m = ui.Model;
        m.AllVmCount = total;
        m.AllRunningVMs = allRunningVMs;
        m.RunningVMs = runningVMs;
        m.PausedVMs = pausedVMs;
        m.WaitingVMs = waitingVMs;
        m.StoppedVMs = stoppedVMs;

        m.VmCount = "All VMs: " + total + " | Running: " + runningVMs + " | Paused: " + pausedVMs +
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
        var r = await new DialogBoxBuilder(ui)
            .WithButtons(DialogButtons.YesNo)
            .WithCheckbox($"Also delete {vm.Name}'s files.", !vm.IsLinked)
            .WithIcon(DialogIcon.Question)
            .WithHeader("Remove virtual machine", $"{vm.Name} will be deleted")
            .WithMessage("Are you sure you want to remove the"+
                       @$" virtual machine ""{vm.Name}""?")
            .ShowDialog();

        if (r == DialogResult.Yes || r == DialogResult.YesChecked)
        {
            if (vm.Status != MachineStatus.STOPPED)
            {
                await ui.ShowError($@"Virtual machine ""{vm.Name}"" is currently " +
                               "running and cannot be removed.", $"{vm.Name} is still running");
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
                await ui.ShowError(@$"Virtual machine ""{vm_name}"" could not be removed due to " +
                                    $"the following error:\n\n{ex.Message}", ex);
                return;
            }

            if (r == DialogResult.YesChecked)
            {
                try
                {
                    Directory.Delete(vm_path, true);
                }
                catch (UnauthorizedAccessException e) //Files are read-only or protected by privileges
                {
                    await ui.ShowError("86Box Manager was unable to delete the files of this " +
                                       "virtual machine because they are read-only or you don't " +
                                       "have sufficient privileges to delete them.\n\nMake sure " +
                                       "the files are free for deletion, then remove them manually.", e);
                    return;
                }
                catch (DirectoryNotFoundException e) //Directory not found
                {
                    await ui.ShowError("86Box Manager was unable to delete the files of this " +
                                            "virtual machine because they no longer exist.", e);
                    return;
                }
                catch (IOException e) //Files are in use by another process
                {
                    await ui.ShowError("86Box Manager was unable to delete some files of this " +
                                            "virtual machine because they are currently in use by " +
                                            "another process.\n\nMake sure the files are free for " +
                                            "deletion, then remove them manually.", e);
                    return;
                }
                catch (Exception ex) //Other exceptions
                {
                    await ui.ShowError($"The following error occurred while trying to remove" +
                                            $" the files of this virtual machine:\n\n{ex.Message}", ex);
                    return;
                }
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
        if (vmPath != null)
            vmPath = vmPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var paths = DeterminePaths(vm);
        if (paths == null)
            throw new Exception("No 86Box executable found.");

        var vars = new CommonExecVars
        {
            FileName = paths.ExePath,
            RomPath = paths.RomPath,
            VmPath = vmPath,
            Build = paths.Build,
            Arch = paths.Arch,
            Vm = vm.Tag,
            LogFile = AppSettings.Settings.EnableLogging ? AppSettings.Settings.LogPath : null,
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

    private static ExePaths DeterminePaths(VMVisual vis)
    {
        string dir;

        //1. The visual has a executable selected
        var paths = vis.Paths;
        if (paths != null && File.Exists(paths.ExePath))
        {
            if (paths.RomPath == null || !Directory.Exists(paths.RomPath))
            {
                paths.RomPath = null;

                //1. If the rom path is not set, prefer the "roms" in the 86Box dir
                dir = Path.Combine(Path.GetDirectoryName(paths.ExePath), "roms");
                if (Directory.Exists(dir))
                    return paths;

                //2. Next we prefer the default rom path
                paths.RomPath = AppSettings.Settings.ROMdir;

                //3. Then the rom path in the default 86Box dir
                if (!Directory.Exists(paths.RomPath))
                {
                    dir = AppSettings.Settings.EXEdir;
                    if (dir != null)
                    {
                        dir = Path.Combine(dir, "roms");
                        if (Directory.Exists(dir))
                            paths.RomPath = dir;
                    }
                }
            }

            return paths;
        }

        //2. Using the selected default exe
        foreach(var exe in AppSettings.Settings.GetDefExe())
        {
            paths = new ExePaths((string)exe["VMExe"],
                exe["VMRoms"] as string, exe["Build"] as string, exe["Arch"] as string);

            if (File.Exists(paths.ExePath))
            {
                if (paths.RomPath == null || !Directory.Exists(paths.RomPath))
                {
                    paths.RomPath = null;

                    //1. If the rom path is not set, prefer the "roms" in the 86Box dir
                    dir = Path.Combine(Path.GetDirectoryName(paths.ExePath), "roms");
                    if (Directory.Exists(dir))
                        return paths;

                    //2. Next we prefer the default rom path
                    paths.RomPath = AppSettings.Settings.ROMdir;

                    //3. Then the rom path in the default 86Box dir
                    if (!Directory.Exists(paths.RomPath))
                    {
                        dir = AppSettings.Settings.EXEdir;
                        if (dir != null)
                        {
                            dir = Path.Combine(dir, "roms");
                            if (Directory.Exists(dir))
                                paths.RomPath = dir;
                        }
                    }
                }

                return paths;
            }
        }

        //3. Using the default exe
        dir = AppSettings.Settings.EXEdir;
        if (!string.IsNullOrWhiteSpace(dir)) 
        {
            var exes = Platforms.Manager.List86BoxExecutables(dir);
            if (exes != null)
            {
                foreach (var exe in exes)
                {
                    var info = Platforms.Manager.Get86BoxInfo(exe);
                    paths = new ExePaths(exe, AppSettings.Settings.ROMdir, "" + info.FilePrivatePart, info.Arch);
                    if (!Directory.Exists(paths.RomPath))
                        paths.RomPath = null;

                    return paths;
                }
            }
        }
        return null;
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
                if (!AppSettings.Settings.ShowConsole)
                {
                    info.RedirectStandardOutput = true;
                    info.UseShellExecute = false;
                }

                start_file = info.FileName;

                var p = Process.Start(info);
                if (p == null)
                    throw new InvalidOperationException($"Could not start: {info.FileName}");

                vis.Status = MachineStatus.RUNNING;
                vis.Tag.Pid = p.Id;
                vis.ClearWaiting();

                vis.RefreshStatus();

                // Minimize the main window if the user wants this
                if (AppSettings.Settings.MinimizeOnVMStart)
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
                _watch.Add(vis.Tag.UID, watch);
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
        catch (InvalidOperationException e)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await parent.ShowError("The process failed to initialize or its window " +
                                       "handle could not be obtained.", e);
            });
        }
        catch (Win32Exception e)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await parent.ShowError("Cannot find 86Box executable. Make sure your settings " +
                                      $"are correct and try again. ({start_file})", e);
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await parent.ShowError("An error has occurred. Please provide the following " +
                                      $"information to the developer:\n{ex.Message}\n{ex.StackTrace}", ex);
            });
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
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await ui.ShowError("An error occurred trying to stop the selected virtual machine.", e);
            });
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
                if (!AppSettings.Settings.ShowConsole)
                {
                    info.RedirectStandardOutput = true;
                    info.UseShellExecute = false;
                }

                var p = Process.Start(info);
                if (p == null)
                    throw new InvalidOperationException($"Could not start: {info.FileName}");

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
                _watch.Add(vis.Tag.UID, watch);
                bgw.RunWorkerAsync(vis);

                ui.UpdateState();
            }
            catch (Win32Exception e)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await ui.ShowError("Cannot find 86Box executable. Make sure your " +
                                       "settings are correct and try again.", e);
                });
            }
            catch (Exception ex)
            {
                // Revert to stopped status and alert the user
                vis.Status = MachineStatus.STOPPED;
                vis.Tag.hWnd = IntPtr.Zero;
                vis.Tag.Pid = -1;
                Dispatcher.UIThread.Post(async () =>
                {
                    await ui.ShowError("This virtual machine could not be configured. Please " +
                                       "provide the following information to the developer:\n" +
                                       $"{ex.Message}\n{ex.StackTrace}", ex);
                });
            }
        }

        CountRefresh();
    }
}