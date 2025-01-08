using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using _86BoxManager.Tools;
using _86BoxManager.Xplat;
using _86BoxManager.Core;
using IOPath = System.IO.Path;
using ButtonsType = MsBox.Avalonia.Enums.ButtonEnum;
using MessageType = MsBox.Avalonia.Enums.Icon;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;
using System.Threading.Tasks;
using System.IO;
using ReactiveUI;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Collections.Concurrent;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Threading;
using MsBox.Avalonia.Enums;

namespace _86BoxManager.Views
{
    public partial class dlgAddVM : Window
    {
        private readonly dlgAddVMModel _m;
        private readonly AppSettings _s;
        CancellationTokenSource cts = new CancellationTokenSource();

        public dlgAddVM()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
                DataContext = new dlgAddVMModel(null);
            else
                DataContext = new dlgAddVMModel(Program.Root.Model.Settings);

            _m = (dlgAddVMModel)DataContext;
            _s = AppSettings.Settings;

            Closing += DlgAddVM_Closing;
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
                            await Dialogs.ShowMessageBox("Sorry, didn't find any Virtual Machines.", MessageType.Info, this);
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
                await Dialogs.ShowMessageBox($@"An error occured: "+ex.Message,
                                    MessageType.Error, this, ButtonsType.Ok, "Failure");
            }
        }

        private async Task<bool> AddVMs()
        {
            if (string.IsNullOrWhiteSpace(_s.CFGdir))
            {
                await Dialogs.ShowMessageBox("Please select a folder for imported virtual machines in the program settings.", MessageType.Error, this);

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
                await Dialogs.ShowMessageBox("You have not selected any virtual machines to import.", MessageType.Info, this);
                
                return false;
            }

            int was_imported = 0;
            int got_imported = 0;
            var s = Program.Root.Settings;

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

                    VMCenter.Add(vm.Name, p, null, null, null, created, false, false, Owner as Window);

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

            await Dialogs.ShowMessageBox(got_imp + was_imp,
                    MessageType.Info, w, ButtonsType.Ok, "Success");

            Close(ResponseType.Ok);

            //Done for async
            return true;
        }

        private async Task<bool> AddVM()
        {
            if (!_m.HasPath)
            {
                await Dialogs.ShowMessageBox($@"You need to set a VM folder in settings or select a folder.",
                    MessageType.Error, this, ButtonsType.Ok, "No VM Folder");
                return false;
            }

            string ip = _m.InstallPath;

            ip = Path.GetFullPath(ip);
            string name = _s.PathToName(ip);
            if (name != null)
            {
                await Dialogs.ShowMessageBox($"The folder you selected is already used by the VM \"{name}\"", MessageType.Error, this, ButtonsType.Ok, "Folder already in use");
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

                VMCenter.Add(_m.VMName, _m.InstallPath, txtDescription.Text, cat, icon, created, cbxOpenCFG.IsActive(), cbxStartVM.IsActive(), Owner as Window);
                t.Commit();
            }

            var w = Owner as Window;
            if (w != null)
                IsVisible = false;
            else
                w = this;

            //await Dialogs.ShowMessageBox($@"Virtual machine ""{_m.VMName}"" was successfully created!",
            //        MessageType.Info, w, ButtonsType.Ok, "Success");

            Close(ResponseType.Ok);
            return true;

            //Exists:
            //    await Dialogs.ShowMessageBox($"A virtual machine on this path already exists. It has the name {name}.",
            //                MessageType.Error, this, ButtonsType.Ok, "Error");
            //    return false;
        }

        private void btnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close(ResponseType.Cancel);
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
        private readonly string _base_path;
        private readonly List<string> _img_list;
        private int _index = 0;

        private bool _is_working = false;

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
                !string.IsNullOrWhiteSpace(_base_path);
        }

        public string InstallPath
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_install_path))
                    return _install_path;

                return string.IsNullOrWhiteSpace(_name) ? _base_path :
                    FolderHelper.EnsureUniqueFolderName(_base_path, _name);
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

            if (s == null)
            {
                //Add a few for the sake of the desiger.
                DefaultCategory = "All machines";
                Categories.Add(DefaultCategory);
                Categories.Add("DOS machines");
                Categories.Add("OS/2 machines");

                _base_path = "c:\\86Box\\VMs\\";
            }
            else
            {
                foreach (var cat in s.Categories.Items)
                    Categories.Add(cat.Name);

                Categories.Sort();

                DefaultCategory = s.DefaultCat.Name;
                _base_path = s.CFGdir ?? "";
            }
        }

        public class ImportVM
        {
            public bool Import { get; set; } = true;
            public string Name { get; set; }
            public string Path { get; set; }
        }
    }
}