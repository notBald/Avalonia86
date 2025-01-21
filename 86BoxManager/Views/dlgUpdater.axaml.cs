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
        _dm.Update86Box(_m.SelectedArtifact, _dm.LatestBuild.Value, _m.UpdateROMs);
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
            s.ArchivePath = _m.ArhivePath;

            t.Commit();
        }

        _m.Commit();
        _m.RaisePropertyChanged(nameof(dlgUpdaterModel.HasChanges));
    }

    private async void btnBrowse_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var text = "Select a folder where 86Box builds will be archived";

        var fldName = await Dialogs.SelectFolder(_m.CurrentExe.VMExe ?? AppDomain.CurrentDomain.BaseDirectory, text, parent: this);

        if (!string.IsNullOrWhiteSpace(fldName))
        {
            _m.ArhivePath = fldName;

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

    public int TabIndex { get => _tab_idx; set => this.RaiseAndSetIfChanged(ref _tab_idx, value); }

    public bool CanUpdate { get => _dm.LatestBuild != null && _dm.LatestBuild > CurrentBuild; }

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

    public string ArhivePath
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
                b = 6000;

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
                   !string.Equals(_me.ArhivePath, ArhivePath);
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
            ArhivePath = s.ArchivePath;

            var exe_fld = s.EXEdir;
            var rom_fld = s.ROMdir;
            Has86BoxFolder = !string.IsNullOrWhiteSpace(exe_fld) && Directory.Exists(exe_fld);

            if (!Has86BoxFolder)
                return;

            CurrentExe.Version = "N/A";
            CurrentExe.Arch = "N/A";
            CurrentExe.Build = "N/A";
            CurrentExe.VMExe = "N/A";
            CurrentExe.VMRoms = "N/A";

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
            this.RaisePropertyChanged(nameof(SelectedArtifact));

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
        HasCLError = true;
        ChangeLog.Add(new LogEntery() { Entery = s, IsError = true });
        this.RaisePropertyChanged(nameof(HasCLError));
    }

    private void AddToUpdateLog(string s)
    {
        UpdateLog.Add(new LogEntery() { Entery = s });
    }

    private void ErrorToUpdateLog(string s)
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