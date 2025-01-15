using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;
using ReactiveUI;
using _86BoxManager.Tools;
using _86BoxManager.Xplat;
using System.ComponentModel;

namespace _86BoxManager.Views;

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
        Close(ResponseType.Cancel);
    }

    private void btnOK_Click(object sender, RoutedEventArgs e)
    {
        Close(ResponseType.Ok);
    }

    private async void btnPathBrowse_click(object sender, RoutedEventArgs e)
    {
        var text = "Select an 86Box executable";

        string path = string.IsNullOrWhiteSpace(DefExePath) ? "" : DefExePath;

        var exe = NativeMSG.IsWindows ? ".exe" : null;
        var file_name = await Dialogs.OpenFile(text, path, "", this, exe);

        if (file_name != null)
        {
            if (!Platforms.Manager.IsExecutable(file_name))
            {
                string name = string.IsNullOrWhiteSpace(file_name) ? "" : System.IO.Path.GetFileName(file_name);
                var res = await Dialogs.ShowMessageBox("The file is not executable, do you wish to add it anyway?", MsBox.Avalonia.Enums.Icon.Question, this, 
                    MsBox.Avalonia.Enums.ButtonEnum.YesNo, $"File {name} is not a program.");

                if (res == ResponseType.No)
                    file_name = null;
            }

            _m.ExePath = file_name; 
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
            var vi = Platforms.Manager.Get86BoxInfo(_m.ExePath);
            if (vi != null)
            {
                var ver_str = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}";

                if (vi.FilePrivatePart >= 3541) //Officially supported builds
                {
                    _m.ExeVersion = $"{ver_str}.{vi.FilePrivatePart} - fully compatible";
                    _m.ExeValid = true;
                }
                else if (vi.FilePrivatePart >= 3333 && vi.FilePrivatePart < 3541) //Should mostly work...
                {
                    _m.ExeVersion = $"{ver_str}.{vi.FilePrivatePart} - partially compatible";
                    _m.ExeWarn = true;
                }
                else //Completely unsupported, since version info can't be obtained anyway
                {
                    _m.ExeVersion = "Unknown - may not be compatible";
                    _m.ExeError = true;
                }

                _m._sugested_name = $"86Box {ver_str} - build {vi.FilePrivatePart}";
                _m._sugested_ver = $"{ver_str}";
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

    public string VerMark
    {
        get
        {
            return _sugested_ver == null ? (HasPath ? PATH_WATERMARK : NO_PATH_WATERMARK) : _sugested_ver;
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
                   _me._rom_dir != _rom_dir;
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