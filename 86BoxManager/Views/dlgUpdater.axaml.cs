using _86BoxManager.Core;
using _86BoxManager.Tools;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;
using ReactiveUI;
using System;
using System.IO;
using IOPath = System.IO.Path;
using System.Diagnostics;
using System.Collections.Generic;
using Avalonia.Metadata;
using DynamicData;
using System.Collections.ObjectModel;
using static _86BoxManager.Tools.JenkinsBase;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using ZstdSharp.Unsafe;
using _86BoxManager.Xplat;
using System.Globalization;
using Avalonia.Threading;
using System.Xml.Linq;

namespace _86BoxManager.Views;

public partial class dlgUpdater : Window
{
    private readonly dlgUpdaterModel _m;
    private readonly Download86Manager _dm = new();

    public dlgUpdater()
    {
        _m = new dlgUpdaterModel(AppSettings.Settings, _dm);
        DataContext = _m;

        InitializeComponent();

        if (!Design.IsDesignMode)
            Loaded += DlgUpdater_Loaded;
        Closed += DlgUpdater_Closed;
    }

    private void DlgUpdater_Closed(object sender, EventArgs e)
    {
        _m.Dispose();
    }

    private void DlgUpdater_Loaded(object sender, RoutedEventArgs e)
    {        
        _dm.FetchMetadata(_m.CurrentBuild);
    }

    private void btnUpdatel_Click(object sender, RoutedEventArgs e)
    {
        _m.TabIndex = 1;
        _m.DetachChangelog();
        _m.HasUpdated = true;
        _dm.Update86Box(_m.SelectedArtifact, _dm.LatestBuild.Value, _m.UpdateROMs, store_files);
        _m.RaisePropertyChanged(nameof(dlgUpdaterModel.HasUpdated));
    }

    private bool store_files((string name, List<Download86Manager.ExtractedFile> files) input)
    {
        if (input.name == "86box")
        {
            string store_path = null;
            string archive_path = null;
            string vm_exe = null;
            string archive_name = null;
            string rom_dir = null;
            bool preserve_roms = false;
            bool dl_roms = false;

            //The propper way of doing this is to collect all this info first. For now, we
            //disable the settings tab and anything that can change state while proceesing
            //is going on.
            Dispatcher.UIThread.Invoke(() =>
            {
                vm_exe = _m.CurrentExe.VMExe;
                if (!File.Exists(vm_exe))
                    store_path = AppSettings.Settings.EXEdir;
                else
                    store_path = IOPath.GetDirectoryName(vm_exe);
                archive_name = _m.ArchiveName;
                archive_path = _m.ArchivePath;
                preserve_roms = _m.PreserveROMs;
                dl_roms = _m.DownloadROMs;
                rom_dir = _m.CurrentExe.VMRoms;
            });


            if (!string.IsNullOrEmpty(archive_path) && !string.IsNullOrEmpty(archive_name))
            {
                var path = FolderHelper.EnsureUniqueFolderName(archive_path, archive_name);

                _m.AddToUpdateLog($"Archiving to: {path}");

                try
                {
                    //Move files
                    Directory.CreateDirectory(path);
                    var dir = IOPath.GetDirectoryName(vm_exe);
                    foreach(var file in Directory.GetFiles(dir))
                    {
                        var name = IOPath.GetFileName(file);
                        File.Move(file, IOPath.Combine(path, name));
                        _m.AddToUpdateLog($" - {name}");
                    }

                    if (preserve_roms && Directory.Exists(rom_dir))
                    {
                        var dest_dir = IOPath.Combine(path, "roms");

                        //If we're downloading new roms, move them.
                        if (dl_roms)
                        {
                            try 
                            {
                                _m.AddToUpdateLog("Moving ROMs to archive");
                                _m.AddToUpdateLog(" - Source: " + rom_dir);
                                _m.AddToUpdateLog(" - Dest: " + dest_dir);
                                Directory.Move(rom_dir, dest_dir); 
                            }
                            catch { _m.ErrorToUpdateLog("Failed to archive roms"); }
                        }
                        else
                        {
                            //Not downloading new roms, don't archive. 
                            //try
                            //{
                            //    _m.AddToUpdateLog("Copy ROMs to archive");
                            //    _m.AddToUpdateLog(" - Source: " + rom_dir);
                            //    _m.AddToUpdateLog(" - Dest: " + dest_dir);
                            //    string[] files = Directory.GetFiles(rom_dir, "*.*", SearchOption.AllDirectories);
                            //    _m.AddToUpdateLog(" - Files to copy: " + files.Length);

                            //    Directory.CreateDirectory(dest_dir);
                            //    foreach (string file in files)
                            //    {
                            //        // Determine the destination path
                            //        string relativePath = file.Substring(rom_dir.Length + 1);
                            //        string destFile = IOPath.Combine(dest_dir, relativePath);

                            //        // Ensure the destination subdirectory exists
                            //        Directory.CreateDirectory(IOPath.GetDirectoryName(destFile));

                            //        // Copy the file
                            //        File.Copy(file, destFile, true); // true to overwrite existing files
                            //    }
                            //}
                            //catch { _m.ErrorToUpdateLog("Failed to archive roms"); }
                        }
                    }

                    //Adds a new entery
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        using (var t = AppSettings.Settings.BeginTransaction())
                        {
                            string exe_name = IOPath.Combine((string)path, store_path);
                            AppSettings.Settings.AddExe(_m.ArchiveName, exe_name, null, _m.ArchiveComment, _m.ArchiveVersion, _m.CurrentExe.Arch, _m.CurrentExe.Build, false);
                            t.Commit();
                        }

                        _m.AddToUpdateLog($"Entery for {_m.ArchiveName} created");
                        _m.AddToUpdateLog($"");
                    });                    
                }
                catch (Exception e)
                {
                    _m.ErrorToUpdateLog("Error: " + e.Message);
                    _m.ErrorToUpdateLog("Failed to arhive current version - stopping.");
                    return false; 
                }
            }

            _m.AddToUpdateLog($"Writing new 86Box to: {store_path}");

            foreach (var file in input.files)
            {
                if (file.FileData.Length == 0)
                    continue;

                var dest = IOPath.Combine(store_path, file.FilePath);
                var dest_dir = IOPath.GetDirectoryName(dest);
                if (!Directory.Exists(dest_dir))
                    Directory.CreateDirectory(dest_dir);
                else if (File.Exists(dest))
                    File.Delete(dest);
                _m.AddToUpdateLog($" - {IOPath.GetFileName(dest)}");
                File.WriteAllBytes(dest, file.FileData.ToArray());
            }

            _m.AddToUpdateLog($"86Box has been updated.");

            Dispatcher.UIThread.Invoke(() =>
            {
                _m.CurrentExe.Build = _m.DM.LatestBuild.Value.ToString(); //<-- Prevents the "update" button from enabeling again.
                _m.CurrentExe.RaisePropertyChanged(nameof(ExeModel.Build));
                _m.CurrentExe.Version = "Unknown";
                _m.CurrentExe.RaisePropertyChanged(nameof(ExeModel.Version));
                _m.CurrentExe.Name = "Latest";
                _m.CurrentExe.RaisePropertyChanged(nameof(ExeModel.Name));
            });

            return true;
        }

        if (input.name == "ROMs")
        {
            string rom_dir = null;

            //The propper way of doing this is to collect all this info first. For now, we
            //disable the settings tab and anything that can change state while proceesing
            //is going on.
            Dispatcher.UIThread.Invoke(() =>
            {
                rom_dir = _m.CurrentExe.VMRoms;
            });
            try
            {
                _m.AddToUpdateLog($"Writing {input.files.Count} ROMs to: {rom_dir}");
                Directory.CreateDirectory(rom_dir);
                int strip = 0;
                if (input.files.Count > 0)
                {
                    strip = input.files[0].FilePath.Length;
                }

                foreach (var file in input.files)
                {
                    if (file.FileData.Length == 0)
                        continue;

                    var dest = IOPath.Combine(rom_dir, file.FilePath.Substring(strip));
                    var dest_dir = IOPath.GetDirectoryName(dest);
                    if (!Directory.Exists(dest_dir))
                        Directory.CreateDirectory(dest_dir);
                    else if (File.Exists(dest))
                        File.Delete(dest);
                    File.WriteAllBytes(dest, file.FileData.ToArray());
                }

                _m.AddToUpdateLog($"ROMs has been updated.");

                Dispatcher.UIThread.Invoke(() =>
                {
                    _m.RomsLastUpdated = _m.DM.LatestRomCommit;
                    _m.RaisePropertyChanged(nameof(dlgUpdaterModel.RomsLastUpdated));
                });
            }
            catch (Exception e) { _m.ErrorToUpdateLog("Failed to update ROMs: "+e.Message); }

            return true;
        }

        return false;
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close(ResponseType.Cancel);
    }

    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
        var s = AppSettings.Settings;
        using (var t = s.BeginTransaction())
        {
            s.PrefNDR = _m.PrefNDR;
            s.UpdateROMs = _m.UpdateROMs;
            s.PreserveROMs = _m.PreserveROMs;
            s.PreferedOS = _m.SelectedOS.ID;
            s.PreferedCPUArch = _m.SelectedArch.ID;
            s.ArchivePath = _m.ArchivePath;

            t.Commit();
        }

        _m.Commit();
        _m.RaisePropertyChanged(nameof(dlgUpdaterModel.HasChanges));
    }

    private async void btnBrowse_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var text = "Select a folder where 86Box builds will be archived";

        var fldName = await Dialogs.SelectFolder(_m.CurrentExe.VMExe ?? AppDomain.CurrentDomain.BaseDirectory, text, parent: this);

        if (!string.IsNullOrWhiteSpace(fldName))
        {
            _m.ArchivePath = fldName;

            if (!FolderHelper.IsDirectoryWritable(fldName))
            {
                await Dialogs.ShowMessageBox("The selected folder is not writable.", MsBox.Avalonia.Enums.Icon.Warning, this);
            }
        }
    }
}

public class dlgUpdaterModel : ReactiveObject, IDisposable
{
    private dlgUpdaterModel _me;
    private IDtoNAME _sel_os, _sel_arch;
    bool _pref_ndr, _upt_roms, _save_roms;
    string _archive_path;

    private readonly Download86Manager _dm;
    private int _tab_idx;
    private readonly IDisposable _subscription;

    public ObservableCollection<LogEntery> ChangeLog { get; } = new();
    public ObservableCollection<LogEntery> UpdateLog { get; } = new();

    public bool Has86BoxFolder { get; private set; }
    public bool Has86BoxExe { get; private set; }
    public bool BoxFolderExist { get; private set; }

    public bool HasUpdated { get; set; }

    public int TabIndex { get => _tab_idx; set => this.RaiseAndSetIfChanged(ref _tab_idx, value); }

    public bool CanUpdate { get => _dm.LatestBuild != null && _dm.LatestBuild > CurrentBuild; }

    public bool DownloadROMs { get; set; }

    public bool CanArchive { get => !string.IsNullOrWhiteSpace(_archive_path); }

    public List<IDtoNAME> Architectures { get; private set; }
    public List<IDtoNAME> OSs { get; private set; }

    public Download86Manager DM => _dm;

    private readonly ReadOnlyObservableCollection<JenkinsBase.Artifact> _artifacts;
    public ReadOnlyObservableCollection<JenkinsBase.Artifact> Artifacts => _artifacts;

    /// <summary>
    /// True if there is an error in the change log
    /// </summary>
    public bool HasCLError { get; private set; }

    public DateTime? RomsLastUpdated { get; set; }
    public string RomsLastUpdatedStr
    {
        get
        {
            var d = RomsLastUpdated;
            if (d == null) return "N/A";

            return d.Value.ToString("d", CultureInfo.CurrentCulture);
        }
    }

    public IDtoNAME SelectedArch
    {
        get => _sel_arch;
        set
        {
            if (!ReferenceEquals(value, _sel_arch))
            {
                this.RaiseAndSetIfChanged(ref _sel_arch, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }
    public IDtoNAME SelectedOS 
    { 
        get => _sel_os;
        set
        {
            if (!ReferenceEquals(value, _sel_os))
            {
                this.RaiseAndSetIfChanged(ref _sel_os, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }
    public bool PrefNDR 
    {
        get => _pref_ndr;
        set
        {
            if (value != _pref_ndr)
            {
                this.RaiseAndSetIfChanged(ref _pref_ndr, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        } 
    }
    public bool UpdateROMs
    {
        get => _upt_roms;
        set
        {
            if (value != _upt_roms)
            {
                this.RaiseAndSetIfChanged(ref _upt_roms, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool PreserveROMs
    {
        get => _save_roms;
        set
        {
            if (value != _save_roms)
            {
                this.RaiseAndSetIfChanged(ref _save_roms, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string ArchivePath
    {
        get => _archive_path;
        set
        {
            if (value != _archive_path)
            {
                this.RaiseAndSetIfChanged(ref _archive_path, value);
                this.RaisePropertyChanged(nameof(CanArchive));
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string ArchiveName { get; set; }
    public string ArchiveVersion { get; set; }
    public string ArchiveComment { get; set; }

    public Artifact SelectedArtifact { get; set; }

    public int CurrentBuild
    {
        get
        {
            int b;
            if (!int.TryParse(CurrentExe.Build, out b))
                b = -1;

            return b;
        }
    }

    public bool HasChanges
    {
        get
        {
            return !ReferenceEquals(_me.SelectedArch, SelectedArch) ||
                   !ReferenceEquals(_me.SelectedOS, SelectedOS) ||
                   _me.PrefNDR != PrefNDR ||
                   _me.UpdateROMs != UpdateROMs ||
                   _me.PreserveROMs != PreserveROMs ||
                   !string.Equals(_me.ArchivePath, ArchivePath);
        }
    }

    public ExeModel CurrentExe { get; private set; } = new();

    internal dlgUpdaterModel(AppSettings s, Download86Manager dm)
    {
        _dm = dm;
        AttachChangelog();

        Architectures = new List<IDtoNAME> 
        { 
            new IDtoNAME() { ID = "arm", Name = "Arm 64" },
            new IDtoNAME() { ID = "64", Name = "Intel/AMD x64"}
        };

        OSs = new()
        {
            new IDtoNAME() { ID = "linux", Name = "Linux" },
            new IDtoNAME() { ID = "mac", Name = "macOS" },
            new IDtoNAME() { ID = "windows", Name = "Windows" },
        };

        if (Design.IsDesignMode)
        {
            Has86BoxFolder = true;

            CurrentExe.Version = "3.11";
            CurrentExe.Arch = "x86";
            CurrentExe.Build = "3110";
            CurrentExe.VMExe = @"c:\86Box\86Box.exe";
            CurrentExe.VMRoms = @"c:\86Box\roms";
            SelectedArch = Architectures[0];
            SelectedOS = OSs[0];
        }
        else
        {
            var name = s.PreferedCPUArch;
            if (name != null)
            {
                foreach (var arch in Architectures)
                {
                    if (string.Equals(name, arch.ID))
                    {
                        SelectedArch = arch;
                        break;
                    }
                }
            }
            name = s.PreferedOS;
            if (name != null)
            {
                foreach (var os in OSs)
                {
                    if (string.Equals(name, os.ID))
                    {
                        SelectedOS = os;
                        break;
                    }
                }
            }
            if (SelectedOS == null)
            {
                if (NativeMSG.IsLinux)
                    SelectedOS = OSs[0];
                else if (NativeMSG.IsWindows)
                    SelectedOS = OSs[2];
                else
                    SelectedOS = OSs[1];
            }
            if (SelectedArch == null)
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    SelectedArch = Architectures[0];
                else
                    SelectedArch = Architectures[1];
            }
            UpdateROMs = s.UpdateROMs;
            PreserveROMs = s.PreserveROMs;
            PrefNDR = s.PrefNDR;
            ArchivePath = s.ArchivePath;

            var exe_fld = s.EXEdir;
            Has86BoxFolder = !string.IsNullOrWhiteSpace(exe_fld) && Directory.Exists(exe_fld);

            if (!Has86BoxFolder)
                return;

            CurrentExe.Version = "N/A";
            CurrentExe.Arch = "N/A";
            CurrentExe.Build = "N/A";
            CurrentExe.VMExe = "N/A";
            //CurrentExe.VMRoms = "N/A";

            //We will always have a ROM folder, as the 86Box folder is required to exist. Though, the roms
            //folder might not exist.
            CurrentExe.VMRoms = s.ROMdir;
            if (string.IsNullOrWhiteSpace(CurrentExe.VMRoms) || !FolderHelper.IsValidFilePath(CurrentExe.VMRoms))
                CurrentExe.VMRoms = IOPath.Combine(exe_fld, "roms");

            //I'm not sure about this.
            if (Directory.Exists(CurrentExe.VMRoms))
                RomsLastUpdated = FolderHelper.GetAModifiedDate(CurrentExe.VMRoms);

            var (exe, info) = VMCenter.GetPathExeInfo();
            if (info != null)
            {
                Has86BoxExe = true;

                if (info.FilePrivatePart > 0)
                    CurrentExe.Build = "" + info.FilePrivatePart;
                if (info.FileMajorPart > 0)
                    CurrentExe.Version = $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}";
                if (!string.IsNullOrWhiteSpace(info.Arch))
                    CurrentExe.Arch = info.Arch;
                CurrentExe.VMExe = exe;

                ArchiveName = $"86Box {CurrentExe.Version} - build {CurrentExe.Build}";
                ArchiveVersion = CurrentExe.Version;
            }

            _subscription = _dm.Artifacts.Connect()
                .Filter(s => !s.FileName.Contains("source", StringComparison.OrdinalIgnoreCase))
                //.ObserveOn(RxApp.MainThreadScheduler) //<-- We switch over to the UI thread.
                .Bind(out _artifacts)
                .Subscribe();
        }

        Commit();
    }

    private void _dm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        //Note, LatestBuild is "invoked", meaning the background thread is waiting until we've
        //      done our work. This isn't strickly needed, but I'm being extra safe.
        if (e.PropertyName == nameof(Download86Manager.LatestBuild))
        {
            Artifact candidate = null;
            bool has_ndr = false;

            //We adjust the selection to the prefered artifact
            foreach (var art in Artifacts)
            {
                if (art.FileName.Contains(SelectedOS.ID, StringComparison.InvariantCultureIgnoreCase) 
                    && art.FileName.Contains(SelectedArch.ID, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (candidate == null || has_ndr != PrefNDR)
                    {
                        candidate = art;
                        has_ndr = art.FileName.Contains("-NDR-");
                    }
                }
            }
            if (candidate == null && Artifacts.Count > 0)
                candidate = Artifacts[0];
            SelectedArtifact = candidate;

            if (UpdateROMs && (_dm.LatestRomCommit == null || RomsLastUpdated == null || _dm.LatestRomCommit.Value > RomsLastUpdated.Value))
            {
                DownloadROMs = true;
            }

            this.RaisePropertyChanged(nameof(SelectedArtifact));
            this.RaisePropertyChanged(nameof(DownloadROMs));
            this.RaisePropertyChanged(nameof(CanUpdate));
        }
    }

    public void Commit()
    {
        _me = (dlgUpdaterModel)MemberwiseClone();
    }

    public void Dispose()
    {
        Detach();
    }

    private void AttachChangelog()
    {
        _dm.Log += AddToChangeLog;
        _dm.ErrorLog += ErrorToChangeLog;
        _dm.PropertyChanged += _dm_PropertyChanged;
    }

    public void DetachChangelog()
    {
        _dm.Log -= AddToChangeLog;
        _dm.Log += AddToUpdateLog;
        _dm.ErrorLog -= ErrorToChangeLog;
        _dm.ErrorLog += ErrorToUpdateLog;
    }

    private void Detach()
    {
        _dm.Log -= AddToChangeLog;
        _dm.Log -= AddToUpdateLog;
        _dm.ErrorLog -= ErrorToUpdateLog;
        _dm.ErrorLog -= ErrorToChangeLog;
        _dm.PropertyChanged -= _dm_PropertyChanged;
        if (_subscription != null)
            _subscription.Dispose();
    }

    private void AddToChangeLog(string s)
    {
        ChangeLog.Add(new LogEntery() { Entery = s });
    }

    private void ErrorToChangeLog(string s)
    {
        HasCLError = DM.LatestBuild == null;
        ChangeLog.Add(new LogEntery() { Entery = s, IsError = true });
        this.RaisePropertyChanged(nameof(HasCLError));
    }

    public void AddToUpdateLog(string s)
    {
        UpdateLog.Add(new LogEntery() { Entery = s });
    }

    public void ErrorToUpdateLog(string s)
    {
        UpdateLog.Add(new LogEntery() { Entery = s, IsError = true });
    }

    public class LogEntery
    {
        public DateTime Created { get; } = DateTime.Now;

        public string Entery { get; set; }

        public bool IsError { get; set; }
    }

    public class IDtoNAME
    {
        public string ID { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}