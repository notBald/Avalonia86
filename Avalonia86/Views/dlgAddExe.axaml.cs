using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using Avalonia86.Tools;
using Avalonia86.Xplat;
using System.ComponentModel;
using Avalonia86.DialogBox;
using Avalonia.Platform.Storage;

namespace Avalonia86.Views;

public partial class dlgAddExe : Window
{
    private readonly dlgAddExeModel _m;

    public string DefExePath {  get; set; }

    public dlgAddExe()
    {
        _m = new dlgAddExeModel();

        InitializeComponent();

        DataContext = _m;

        _m.PropertyChanged += _m_PropertyChanged;

        //Windows 10 workarround
        NativeMSG.SetDarkMode(this);
    }

    private void _m_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(dlgAddExeModel.ExePath))
        {
            Get86BoxVersion();
        }
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close(DialogResult.Cancel);
    }

    private void btnOK_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_m.Name))
        {
            if (!string.IsNullOrWhiteSpace(_m._sugested_name))
                _m.Name = _m._sugested_name;
            else
                _m.Name = System.IO.Path.GetFileName(_m.ExePath);
        }
        if (string.IsNullOrWhiteSpace(_m.Version))
        {
            if (!string.IsNullOrWhiteSpace(_m._sugested_ver))
                _m.Version = _m._sugested_ver;
            else
                _m.Version = "Unknown";
        }
        if (string.IsNullOrWhiteSpace(_m.RomDir))
            _m.RomDir = null;
        if (string.IsNullOrWhiteSpace(_m.Comment))
            _m.Comment = null;
        if (string.IsNullOrWhiteSpace(_m.RomDir))
            _m.RomDir = null;
        if (string.IsNullOrWhiteSpace(_m.Arch))
            _m.Arch = null;
        if (string.IsNullOrWhiteSpace(_m.Build))
            _m.Arch = null;

        Close(DialogResult.Ok);
    }

    private async void btnPathBrowse_click(object sender, RoutedEventArgs e)
    {
        var text = "Select an 86Box executable";

        string path = string.IsNullOrWhiteSpace(DefExePath) ? "" : DefExePath;

        var exe = CurrentApp.IsWindows ? ".exe" : null;
        var file_name = await Dialogs.OpenFile(text, path, "", this, exe);

        if (file_name != null)
        {
            if (!Platforms.Manager.IsExecutable(file_name))
            {
                string name = string.IsNullOrWhiteSpace(file_name) ? "" : System.IO.Path.GetFileName(file_name);
                var res = await this.ShowQuestion("The file is not executable, do you wish to add it anyway?", 
                                                 $"File {name} is not a program.");

                if (res == DialogResult.No)
                    file_name = null;
            }

            _m.ExePath = file_name; 
        }
    }

    private async void btnRomBrowse_click(object sender, RoutedEventArgs e)
    {
        var text = "Select a ROM folder for 86Box";

        string path = string.IsNullOrWhiteSpace(DefExePath) ? "" : DefExePath;
        var folder_name = await Dialogs.SelectFolder(path, text, this);

        if (folder_name != null)
        {
            _m.RomDir = folder_name;
        }
    }

    /// <summary>
    /// Obtains the 86Box version from 86Box executable
    /// </summary>
    private void Get86BoxVersion()
    {
        _m.ExeValid = false;
        _m.ExeError = false;
        _m.ExeWarn = false;
        _m.ExeVersion = "86Box executable not found!";
        _m._sugested_name = null;
        _m._sugested_ver = null;

        try
        {
            var vi = Platforms.Manager.Get86BoxInfo(_m.ExePath, out _);
            if (vi != null)
            {
                //Todo: this code is now in three locations.
                var ver_str = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}";

                if (vi.FilePrivatePart >= 3541) //Officially supported builds
                {
                    _m.ExeVersion = $"{ver_str}.{vi.FilePrivatePart} - fully compatible";
                    _m.ExeValid = true;
                }
                else if (vi.FilePrivatePart >= 3333) //Should mostly work...
                {
                    _m.ExeVersion = $"{ver_str}.{vi.FilePrivatePart} - partially compatible";
                    _m.ExeWarn = true;
                }
                else if (vi.FilePrivatePart >= 2000) //Should work...
                {
                    _m.ExeVersion = $"{ver_str} - limited compatibility";
                    _m.ExeWarn = true;
                }
                else //Completely unsupported, since version info can't be obtained anyway
                {
                    _m.ExeVersion = "Unknown - may not be compatible";
                    _m.ExeError = true;
                }

                _m._sugested_name = $"86Box {ver_str} - build {vi.FilePrivatePart}";
                _m._sugested_ver = $"{ver_str}";
                _m.Name = _m._sugested_name;
                _m.Version = _m._sugested_ver;

                _m.Arch = vi.Arch;
                _m.Build = "" + vi.FilePrivatePart;
            }
        }
        catch { }

        _m.RaisePropertyChanged(nameof(dlgAddExeModel.ExeError));
        _m.RaisePropertyChanged(nameof(dlgAddExeModel.ExeWarn));
        _m.RaisePropertyChanged(nameof(dlgAddExeModel.ExeValid));
        _m.RaisePropertyChanged(nameof(dlgAddExeModel.NameMark));
        _m.RaisePropertyChanged(nameof(dlgAddExeModel.VerMark));
    }
}

internal class dlgAddExeModel : ReactiveObject
{
    dlgAddExeModel _me;
    private string _rom_dir, _exe_path, _exe_ver;
    private string _name, _version, _comment, _arch, _build;
    internal string _sugested_name, _sugested_ver;

    const string NO_PATH_WATERMARK = "< Select a path, please >";
    const string PATH_WATERMARK = "< Fill this out, please >";

    public bool ExeValid { get; set; }
    public bool ExeWarn { get; set; }
    public bool ExeError { get; set; }

    public string NameMark
    {
        get
        {
            return _sugested_name == null ? (HasPath ? PATH_WATERMARK : NO_PATH_WATERMARK) : _sugested_name;
        }
    }

    public string Name 
    { 
        get => _name;
        set
        {
            if (value != _name)
            {
                this.RaiseAndSetIfChanged(ref _name, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        } 
    }

    public string VerMark
    {
        get
        {
            return _sugested_ver == null ? (HasPath ? PATH_WATERMARK : NO_PATH_WATERMARK) : _sugested_ver;
        }
    }

    public string Version 
    { 
        get => _version;
        set
        {
            if (value != _version)
            {
                this.RaiseAndSetIfChanged(ref _version, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string Comment 
    { 
        get => _comment;
        set
        {
            if (value != _comment)
            {
                this.RaiseAndSetIfChanged(ref _comment, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string Arch 
    { 
        get => _arch; 
        set
        {
            if (value != _arch)
            {
                this.RaiseAndSetIfChanged(ref _arch, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        } 
    }

    public string Build
    {
        get => _build;
        set
        {
            if (value != _build)
            {
                this.RaiseAndSetIfChanged(ref _build, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool HasPath => _exe_path != null;

    public bool HasChanges
    {
        get
        {
            if (_me == null)
                return false;

            return _me._exe_path != _exe_path ||
                   _me._rom_dir != _rom_dir ||
                   _me._arch != _arch ||
                   _me._build != _build ||
                   _me._name != _name ||
                   _me._comment != _comment ||
                   _me._version != _version;
        }
    }

    public string ExePath
    {
        get => _exe_path;
        set
        {
            if (_exe_path != value)
            {
                this.RaiseAndSetIfChanged(ref _exe_path, value);
                this.RaisePropertyChanged(nameof(HasChanges));
                this.RaisePropertyChanged(nameof(HasPath));
            }
        }
    }

    public string ExeVersion
    {
        get => _exe_ver;
        set
        {
            if (_exe_ver != value)
            {
                this.RaiseAndSetIfChanged(ref _exe_ver, value);
            }
        }
    }

    public string RomDir
    {
        get => _rom_dir;
        set
        {
            if (_rom_dir != value)
            {
                this.RaiseAndSetIfChanged(ref _rom_dir, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }


    public void Commit()
    {
        _me = (dlgAddExeModel)MemberwiseClone();

        this.RaisePropertyChanged(nameof(HasChanges));
    }
}