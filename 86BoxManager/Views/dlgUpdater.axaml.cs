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
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close(ResponseType.Cancel);
    }
}

public class dlgUpdaterModel : ReactiveObject, IDisposable
{
    private readonly Download86Manager _dm;
    private int _tab_idx;

    public ObservableCollection<LogEntery> Log { get; } = new();

    public bool Has86BoxFolder { get; private set; }
    public bool Has86BoxExe { get; private set; }
    public bool BoxFolderExist { get; private set; }

    public int TabIndex { get => _tab_idx; set => this.RaiseAndSetIfChanged(ref _tab_idx, value); }

    public bool CanUpdate { get => _dm.LatestBuild != null && _dm.LatestBuild > CurrentBuild; }

    public Download86Manager DM => _dm;

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

    public ExeModel CurrentExe { get; private set; } = new();

    internal dlgUpdaterModel(AppSettings s, Download86Manager dm)
    {
        _dm = dm;
        Attach();

        if (Design.IsDesignMode)
        {
            Has86BoxFolder = true;

            CurrentExe.Version = "3.11";
            CurrentExe.Arch = "x86";
            CurrentExe.Build = "3110";
            CurrentExe.VMExe = @"c:\86Box\86Box.exe";
            CurrentExe.VMRoms = @"c:\86Box\roms";
        }
        else
        {
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
            }

            
        }
    }

    private void _dm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Download86Manager.LatestBuild))
            this.RaisePropertyChanged(nameof(CanUpdate));
    }

    public void Dispose()
    {
        Detach();
    }

    private void Attach()
    {
        _dm.Log += AddToLog;
        _dm.PropertyChanged += _dm_PropertyChanged;
    }

    private void Detach()
    {
        _dm.Log -= AddToLog;
        _dm.PropertyChanged -= _dm_PropertyChanged;
    }

    private void AddToLog(string s)
    {
        Log.Add(new LogEntery() { Entery = s });
    }

    public class LogEntery
    {
        public DateTime Created { get; } = DateTime.Now;

        public string Entery { get; set; }
    }
}