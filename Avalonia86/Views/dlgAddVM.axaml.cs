using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia86.Core;
using Avalonia86.DialogBox;
using Avalonia86.Tools;
using Avalonia86.Xplat;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia86.Views;

public partial class dlgAddVM : Window
{
    private readonly dlgAddVMModel _m;
    private readonly AppSettings _s;
    CancellationTokenSource cts = new CancellationTokenSource();

    public dlgAddVM()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
            _m = new dlgAddVMModel(null);
        else
        {
            var m = Program.Root.Model;

            _m = new dlgAddVMModel(m.Settings);

            if (m.CategoryIndex > 0)
                _m.Category = m.CategoryName;
        }

        DataContext = _m;
        _s = AppSettings.Settings;

        Closing += DlgAddVM_Closing;
        if (!Design.IsDesignMode)
            Loaded += DlgAddVM_Loaded;

        //Windows 10 workarround
        NativeMSG.SetDarkMode(this);
    }

    private async void DlgAddVM_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= DlgAddVM_Loaded;

        //Put focus into the name field
        tbName.Focus();

        //Check if the VM folder exists
        if (!_m.HasPath)
        {
            //We know there is no base path, so we can safely assume that the user has not set a VM folder.
            //But instead of showing an error, we check if we can create a VM folder

            var dir = CurrentApp.StartupPath;
            if (Directory.Exists(dir))
            {
                bool is_writable = false;

                dir = Path.Combine(dir, "VMs");
                if (!Directory.Exists(dir))
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                        is_writable = true;
                    }
                    catch { }
                }
                else
                {
                    is_writable = FolderHelper.IsDirectoryWritable(dir);
                }

                if (!is_writable)
                {
                    await this.ShowError("You must select a default location for virtual machines, do it in settings.", "No VM folder");
                    Close();
                }
                else
                {
                    _s.CFGdir = dir;
                    _m.BasePath = dir;
                    _m.RaisePropertyChanged(nameof(dlgAddVMModel.InstallPath));
                }
            }           
        }
    }

    private void DlgAddVM_Closing(object sender, WindowClosingEventArgs e)
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
    }

    private async void btnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var initDir = Platforms.Env.MyComputer;
        if (!string.IsNullOrWhiteSpace(txtImportPath.Text))
            initDir = txtImportPath.Text;
        var text = "Select a folder that will be searched for virtual machines";

        var fileName = await Dialogs.SelectFolder(initDir, text, this);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            txtImportPath.Text = fileName;
            var bag = new ConcurrentBag<string>();
            _m.Imports.Clear();
            _m.IsWorking = true;

            var empty_bag = () =>
            {
                //Hearthbeat
                var list = new List<dlgAddVMModel.ImportVM>();
                while (!bag.IsEmpty)
                {
                    if (bag.TryTake(out var path))
                    {
                        try
                        {
                            list.Add(new dlgAddVMModel.ImportVM() { Name = new DirectoryInfo(path).Name, Path = path });
                        }
                        catch { }
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var item in list)
                    {
                        _m.Imports.Add(item);
                    }
                });
            };

            FolderHelper.SearchFolders(fileName, "86box.cfg", () =>
            {
                empty_bag();

                Dispatcher.UIThread.Post(async () =>
                {
                    _m.IsWorking = false;

                    if (_m.Imports.Count == 0)
                    {
                        await this.ShowMsg("Sorry, didn't find any Virtual Machines.");
                    }
                });
            }, bag, empty_bag, cts.Token);
        }
    }

    // Check if VM with this name already exists, and send the data to the main form for processing if it doesn't
    private async void btnAdd_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tab = tbc.SelectedItem as TabItem;
            switch (tab.Name)
            {
                case "tbiNew":
                    await AddVM();
                    return;
                case "tbiImport":
                    await AddVMs();
                    return;
                default:
                    throw new NotSupportedException();
            }
        }
        catch (Exception ex)
        {
            await DialogBox.DialogBox.ShowError(Owner as Window, $@"An error occured: "+ex.Message, ex, "Failure");
        }
    }

    private async Task<bool> AddVMs()
    {
        if (string.IsNullOrWhiteSpace(_s.CFGdir))
        {
            await this.ShowError("Please select a folder for imported virtual machines in the program settings.");

            return false;
        }

        var to_import = new List<dlgAddVMModel.ImportVM>(_m.Imports.Count);
        foreach (var vm in _m.Imports)
        {
            if (vm.Import && !String.IsNullOrWhiteSpace(vm.Name))
                to_import.Add(vm);
        }

        if (to_import.Count == 0)
        {
            await this.ShowMsg("You have not selected any virtual machines to import.");
            
            return false;
        }

        int was_imported = 0;
        int got_imported = 0;
        var s = AppSettings.Settings;

        foreach (var vm in to_import)
        {
            var p = Path.GetFullPath(vm.Path);
            var name = s.PathToName(p);
            if (name != null)
            {
                was_imported++;
            }
            else
            {
                var created = FolderHelper.FetchACreationDate(p);

                VMCenter.Add(vm.Name, p, null, null, null, created, _m.ExeModel.SelectedItem.ID, false, false, Owner as Window);

                got_imported++;
            }
        }

        var w = Owner as Window;
        if (w != null)
            IsVisible = false;
        else
            w = this;

        string got_imp = $"Imported {got_imported} virtual machine" + (got_imported == 1 ? "" : "s");

        string was_imp = "";
        if (was_imported > 0)
            was_imp = (was_imported == 0) ? "" : $@" , {was_imported} machine" + (was_imported == 1 ? " was" : "s were") + " already importet.";

        await DialogBox.DialogBox.ShowMsg(w, got_imp + was_imp, "Success");

        Close(DialogResult.Ok);

        //Done for async
        return true;
    }

    private async Task<bool> AddVM()
    {
        if (!_m.HasPath)
        {
            await this.ShowError($@"You need to set a VM folder in settings or select a folder.", "No VM Folder");
            return false;
        }

        string ip = _m.InstallPath;

        ip = Path.GetFullPath(ip);
        string name = _s.PathToName(ip);
        if (name != null)
        {
            await this.ShowError($"The folder you selected is already used by the VM \"{name}\"", "Folder already in use");
            return false;
        }

        var created = FolderHelper.FetchACreationDate(ip);
        var icon = _m.VMIcon;
        if (icon == AppSettings.DefaultIcon)
            icon = null;

        using (var t = _s.BeginTransaction())
        {
            var dc = (dlgAddVMModel)DataContext;
            string cat = _m.Category;
            if (cat == dc.DefaultCategory)
                cat = null;

            VMCenter.Add(_m.VMName, _m.InstallPath, txtDescription.Text, cat, icon, created, _m.ExeModel.SelectedItem.ID, cbxOpenCFG.IsActive(), cbxStartVM.IsActive(), Owner as Window);
            t.Commit();
        }

        var w = Owner as Window;
        if (w != null)
            IsVisible = false;
        else
            w = this;

        //await Dialogs.ShowMessageBox($@"Virtual machine ""{_m.VMName}"" was successfully created!",
        //        MessageType.Info, w, ButtonsType.Ok, "Success");

        Close(DialogResult.Ok);
        return true;

        //Exists:
        //    await Dialogs.ShowMessageBox($"A virtual machine on this path already exists. It has the name {name}.",
        //                MessageType.Error, this, ButtonsType.Ok, "Error");
        //    return false;
    }

    private void btnCancel_OnClick(object sender, RoutedEventArgs e)
    {
        Close(DialogResult.Cancel);
    }

    private async void btnSelFld_OnClick(object sender, RoutedEventArgs e)
    {
        var initDir = _m.InstallPath;
        var text = "Select a folder where the 86Box files are (to be) located";

        var fldName = await Dialogs.SelectFolder(initDir, text, parent: this);

        if (!string.IsNullOrWhiteSpace(fldName))
        {
            _m.InstallPath = fldName;
        }
    }

    private void btnLeftImg_Click(object sender, RoutedEventArgs e)
    {
        _m.PrevIndex();
    }

    private void btnRightImg_Click(object sender, RoutedEventArgs e)
    {
        _m.NextIndex();
    }
}

internal class dlgAddVMModel : ReactiveObject
{
    private string _name, _install_path;
    private int _tab_idx = 0;
    private readonly List<string> _img_list;
    private int _index = 0;

    private bool _is_working = false;

    public ctrlSetExecutableModel ExeModel { get; private set; }
    public bool IsWorking 
    { 
        get => _is_working; set
        {
            if (_is_working != value)
            {
                this.RaiseAndSetIfChanged(ref _is_working, value);
                this.RaisePropertyChanged(nameof(CanAdd));
            }
        }
    }

    internal string BasePath { get; set; }

    public string VMName
    { 
        get 
        { 
            if (_name == null && !string.IsNullOrWhiteSpace(_install_path))
                return new DirectoryInfo(_install_path).Name;
            return _name; 
        }
        set 
        {
            if (_name != value)
            {
                this.RaiseAndSetIfChanged(ref _name, value);
                this.RaisePropertyChanged(nameof(InstallPath));
                this.RaisePropertyChanged(nameof(CanAdd));
            }
        }
    }

    public int TabIndex
    {
        get { return _tab_idx; }
        set 
        {
            if (_tab_idx != value)
            {
                this.RaiseAndSetIfChanged(ref _tab_idx, value);
                this.RaisePropertyChanged(nameof(CanAdd));
            }
        }
    }

    public bool HasPath
    {
        get => !string.IsNullOrWhiteSpace(_install_path) || 
            !string.IsNullOrWhiteSpace(BasePath);
    }

    public string InstallPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_install_path))
                return _install_path;

            return string.IsNullOrWhiteSpace(_name) ? BasePath :
                FolderHelper.EnsureUniqueFolderName(BasePath, _name);
        }
        set
        {
            if (_install_path != value)
            {
                this.RaiseAndSetIfChanged(ref _install_path, value);
                this.RaisePropertyChanged(nameof(VMName));
                this.RaisePropertyChanged(nameof(CanAdd));
            }
        }
    }

    public bool CanAdd
    {
        get
        {
            if (_tab_idx == 0)
            {
                return !String.IsNullOrWhiteSpace(VMName);
            }
            else if (_tab_idx == 1)
            {
                return !IsWorking && Imports.Count > 0;
            }

            return false;
        }
    }

    public string DefaultCategory { get; private set; }

    public string Category { get; set; }

    public List<string> Categories { get; } = new();

    public ObservableCollection<ImportVM> Imports { get; } = new();

    public string VMIcon
    {
        get
        {
            return _img_list[_index];
        }
    }

    public void NextIndex()
    {
        _index++;
        if (_index == _img_list.Count)
            _index = 0;
        this.RaisePropertyChanged(nameof(VMIcon));
    }
    public void PrevIndex()
    {
        _index--;
        if (_index < 0)
            _index = _img_list.Count - 1;
        this.RaisePropertyChanged(nameof(VMIcon));
    }

    private void SetIcon(string path)
    {
        for (int c = 0; c < _img_list.Count; c++)
        {
            if (_img_list[c] == path)
            {
                _index = c;
                this.RaisePropertyChanged(nameof(VMIcon));
                break;
            }
        }
    }

    public dlgAddVMModel(AppSettings s)
    {
        _img_list = AppSettings.GetIconAssets();
        SetIcon(AppSettings.DefaultIcon);
        ExeModel = new ctrlSetExecutableModel(s);

        if (s == null)
        {
            //Add a few for the sake of the desiger.
            DefaultCategory = "All machines";
            Categories.Add(DefaultCategory);
            Categories.Add("DOS machines");
            Categories.Add("OS/2 machines");

            BasePath = "c:\\86Box\\VMs\\";
        }
        else
        {
            foreach (var cat in s.Categories.Items)
                Categories.Add(cat.Name);

            Categories.Sort();

            DefaultCategory = s.DefaultCat.Name;
            BasePath = s.CFGdir ?? "";
        }
    }

    public class ImportVM
    {
        public bool Import { get; set; } = true;
        public string Name { get; set; }
        public string Path { get; set; }
    }
}