using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using _86BoxManager.ViewModels;
using _86BoxManager.Core;
using _86BoxManager.Models;
using _86BoxManager.Tools;
using IOPath = System.IO.Path;
using ButtonsType = MsBox.Avalonia.Enums.ButtonEnum;
using MessageType = MsBox.Avalonia.Enums.Icon;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;
using Avalonia.Platform;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using ReactiveUI;
using Mono.Unix.Native;
using System.Xml.Linq;
using System.IO;

namespace _86BoxManager.Views
{
    public partial class dlgEditVM : Window
    {
        /// <summary>
        /// VM to be edited
        /// </summary>
        private VMVisual _vm;
        private readonly dlgEditModel _m;

        internal VMVisual VM { get { return _vm; } set { _vm = value; } }

        public dlgEditVM()
        {
            InitializeComponent();
            txtName.OnTextChanged(txtName_TextChanged);
            _m = new dlgEditModel(Program.Root != null ? Program.Root.Settings : null);
            DataContext = _m;
        }

        private void btnLeftImg_Click(object sender, RoutedEventArgs e)
        {
            var dc = DataContext as dlgEditModel;
            if (dc != null)
                dc.PrevIndex();
        }

        private void btnRightImg_Click(object sender, RoutedEventArgs e)
        {
            var dc = DataContext as dlgEditModel;
            if (dc != null)
                dc.NextIndex();
        }

        // Load the data for selected VM
        private void dlgEditVM_Load(object sender, EventArgs e)
        {
            if (_vm != null)
            {
                txtName.Text = _vm.Name;
                txtDesc.Text = _vm.Desc;
                txtComment.Text = _vm.Comment;
                _m.Category = _vm.Category;
                lblPath1.Text = _vm.Path;

                var dc = DataContext as dlgEditModel;
                if (dc != null)
                    dc.SetIcon(_vm.IconPath);
            }
            
        }

        private void txtName_TextChanged(object sender, TextInputEventArgs e)
        {
            // Check for empty strings etc.
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                btnApply.IsEnabled = false;
                return;
            }

            var cfgPath = Program.Root.Settings.CFGdir;
            btnApply.IsEnabled = true;
            lblPath1.Text = cfgPath + txtName.Text;
            lblPath1.SetToolTip(cfgPath + txtName.Text);
        }

        private async void btnApply_Click(object sender, RoutedEventArgs e)
        {
            var dc = DataContext as dlgEditModel;

            try
            {
                VMCenter.Edit(_vm.Tag.UID, txtName.Text, txtDesc.Text, _m.Category, dc?.VMIcon, txtComment.Text, this);
            }
            catch (Exception ex)
            {
                await Dialogs.ShowMessageBox($@"Unable to save edit: "+ex.Message, MessageType.Error, this);
            }

            //await Dialogs.ShowMessageBox($@"Virtual machine ""{name}"" was successfully modified.",
            //    MessageType.Info, parent, ButtonsType.Ok, "Success");

            Close(ResponseType.Ok);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close(ResponseType.Cancel);
        }
    }

    internal class dlgEditModel : ReactiveObject
    {
        private readonly List<string> _img_list;
        private ExeModel _exeModel;

        private int _index = -1;

        private string _cat;

        public string DefaultCategory { get; private set; }
        public string Default86BoxFolder { get; private set; }
        public string Default86BoxRoms { get; private set; }

        public string Category { get => _cat; set => this.RaiseAndSetIfChanged(ref _cat, value); }

        public List<string> Categories { get; } = new();
        public List<ExeModel> ExeFiles { get; } = new();

        public ExeModel SelectedItem
        {
            get => _exeModel;
            set
            {
                if (!ReferenceEquals(value, _exeModel))
                {
                    this.RaiseAndSetIfChanged(ref _exeModel, value);
                    this.RaisePropertyChanged(nameof(SelExeRomDir));
                    this.RaisePropertyChanged(nameof(SelExePath));
                }
            }
        }

        public int SelectedIndex { get; set; }

        public string VMIcon
        {
            get
            {
                return _index != -1 ? _img_list[_index] : AppSettings.DefaultIcon;
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

        public void SetIcon(string path)
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

        public dlgEditModel(AppSettings s)
        {
            _img_list = AppSettings.GetIconAssets();
            var exeModel = new ExeModel()
            {
                Name = "Default 86Box executable"
            };
            ExeFiles.Add(exeModel);

            if (s == null)
            {
                //Add a few for the sake of the desiger.
                DefaultCategory = "All machines";
                Categories.Add(DefaultCategory);
                Categories.Add("DOS machines");
                Categories.Add("OS/2 machines");

                ExeFiles.Add(new ExeModel()
                {
                    Name = "86Box 3.11"
                });
            }
            else
            {
                foreach (var cat in s.Categories.Items)
                    Categories.Add(cat.Name);

                Categories.Sort();

                DefaultCategory = s.DefaultCat.Name;
                Default86BoxFolder = s.EXEdir;
                Default86BoxRoms = s.ROMdir;

                foreach (var r in s.ListExecutables())
                {
                    ExeFiles.Add(new ExeModel()
                    {
                        ID = (long)r["ID"],
                        Name = r["Name"] as string,
                        VMExe = r["VMExe"] as string,
                        VMRoms = r["VMRoms"] as string,
                        Version = r["Version"] as string,
                        Comment = r["Comment"] as string,
                        Arch = r["Arch"] as string,
                    });
                }

                //Todo: find default 86Box...
                // 1. Get the default entery from the Exe table, if it exists
                // 2. Get the list of EXE from the default path
                // 3. Get the first executable

                //Todo: find the default 86Box rompath
                // 1. Get the default entery from the Exe table, if it exists
                // 2. Check if there's a rom path in the 86Box folder
                // 3. Get the default rom path, if it exits
                // 4. Get the rom path of the default 86Box exe (even if another default exe has been choosen)
            }

            SelectedItem = exeModel;
        }

        public string SelExePath
        {
            get
            {
                try
                {
                    if (SelectedItem != null)
                    {
                        if (!string.IsNullOrWhiteSpace(SelectedItem.VMExe))
                            return SelectedItem.VMExe;

                        if (!string.IsNullOrWhiteSpace(Default86BoxFolder))
                        {
                            return Path.Combine(Default86BoxFolder, "< 86Box >");
                        }
                    }
                }
                catch { }

                return "";
            }
        }

        public string SelExeRomDir
        {
            get
            {
                try
                {
                    if (SelectedItem != null)
                    {
                        if (!string.IsNullOrWhiteSpace(SelectedItem.VMRoms))
                            return SelectedItem.VMRoms;

                        if (!string.IsNullOrWhiteSpace(SelectedItem.VMExe))
                        {
                            var dir = Path.Combine(Path.GetDirectoryName(SelectedItem.VMExe), "roms");
                            if (Directory.Exists(dir))
                                return dir;
                        }

                        if (!string.IsNullOrWhiteSpace(Default86BoxRoms))
                        {
                            if (Directory.Exists(Default86BoxRoms))
                                return Default86BoxRoms;
                        }

                        if (!string.IsNullOrWhiteSpace(Default86BoxFolder))
                        {
                            var dir = Path.Combine(Path.GetDirectoryName(Default86BoxFolder), "roms");
                            if (Directory.Exists(dir))
                                return dir;
                        }
                    }
                } catch { }

                return "";
            }
        }

        public class ExeModel
        {
            public long? ID { get; set; }
            public string Name { get; set; }
            public string Version { get; set; }
            public string VMExe { get; set; }
            public string VMRoms { get; set; }
            public string Arch {  get; set; }
            public string Comment { get; set; }
        }
    }
}