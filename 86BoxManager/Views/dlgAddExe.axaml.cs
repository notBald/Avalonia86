using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;
using ReactiveUI;
using _86BoxManager.Tools;

namespace _86BoxManager.Views;

public partial class dlgAddExe : Window
{
    private readonly dlgAddExeModel _m;

    public dlgAddExe()
    {
        _m = new dlgAddExeModel();

        InitializeComponent();

        DataContext = _m;
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close(ResponseType.Cancel);
    }

    private void btnPathBrowse_click(object sender, RoutedEventArgs e)
    {
        var text = "Select an 86Box executable";

        //            //Todo: adjust for Linux and Mac
        var file_name = Dialogs.OpenFile(text, "", "", this, ".exe");
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