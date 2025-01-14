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

        try
        {
            var v = Platforms.Manager.Get86BoxInfo(_m.ExePath);
            if (v != null && v.VerInfo != null)
            {
                var vi = v.VerInfo;
                if (vi.FilePrivatePart >= 3541) //Officially supported builds
                {
                    _m.ExeVersion = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}.{vi.FilePrivatePart} - fully compatible";
                    _m.ExeValid = true;
                }
                else if (vi.FilePrivatePart >= 3333 && vi.FilePrivatePart < 3541) //Should mostly work...
                {
                    _m.ExeVersion = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}.{vi.FilePrivatePart} - partially compatible";
                    _m.ExeWarn = true;
                }
                else //Completely unsupported, since version info can't be obtained anyway
                {
                    _m.ExeVersion = "Unknown - may not be compatible";
                    _m.ExeError = true;
                }
            }
        }
        catch { }

        _m.RaisePropertyChanged(nameof(dlgSettingsModel.ExeError));
        _m.RaisePropertyChanged(nameof(dlgSettingsModel.ExeWarn));
        _m.RaisePropertyChanged(nameof(dlgSettingsModel.ExeValid));
    }
}

internal class dlgAddExeModel : ReactiveObject
{
    dlgAddExeModel _me;
    private string _rom_dir, _exe_path, _exe_ver;

    public bool ExeValid { get; set; }
    public bool ExeWarn { get; set; }
    public bool ExeError { get; set; }

    public bool HasChanges
    {
        get
        {
            if (_me == null)
                return false;

            return false;
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
                this.RaisePropertyChanged(nameof(HasChanges));
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