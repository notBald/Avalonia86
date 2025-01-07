using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using _86BoxManager.Core;
using _86BoxManager.Tools;
using IOPath = System.IO.Path;
using ButtonsType = MsBox.Avalonia.Enums.ButtonEnum;
using MessageType = MsBox.Avalonia.Enums.Icon;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;
using ReactiveUI;
using System.Xml.Linq;

namespace _86BoxManager.Views
{
    public partial class dlgCloneVM : Window
    {
        // Path of the VM to be cloned
        private readonly string _oldPath;

        private readonly dlgCloneModel _m;
        private readonly AppSettings _s;
        private readonly long _uid;

        public dlgCloneVM()
        {
            InitializeComponent();
            Loaded += dlgCloneVM_Load;
        }

        public dlgCloneVM(string oldPath) : this()
        {
            _oldPath = oldPath;

            _m = new dlgCloneModel();
            DataContext = _m;
            var r = Program.Root;
            if (r != null)
            {
                _s = r.Settings;

                _m.OrgName = _s.PathToName(oldPath);
                _uid = _s.PathToId(oldPath);
            }
        }

        private async void dlgCloneVM_Load(object sender, EventArgs e)
        {
            //For the designer
            if (_s == null)
                return;
            
            if (_m.OrgName == null)
            { 
                await Dialogs.ShowMessageBox("Fatal error, failed to find machine that was to be cloned", MessageType.Error, this);
                Close();
                return;
            }
        }

        private async void btnClone_Click(object sender, RoutedEventArgs e)
        {
            var cfgpath = _s.CFGdir;
            if (string.IsNullOrWhiteSpace(cfgpath))
            {
                await Dialogs.ShowMessageBox($@"You need to set a VM folder in settings!",
                    MessageType.Error, this, ButtonsType.Ok, "No VM Folder");
                return;
            }

            var new_path = FolderHelper.EnsureUniqueFolderName(cfgpath, _m.CloneName);

            if (await VMCenter.Clone(_uid, _m.CloneName, new_path, this))
                Close(ResponseType.Ok);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close(ResponseType.Cancel);
        }
    }

    internal class dlgCloneModel : ReactiveObject
    {
        private string _clone_name, _org_name;

        public string OrgName { get => _org_name; set => this.RaiseAndSetIfChanged(ref _org_name, value); }

        public string CloneName 
        { 
            get => _clone_name;
            set
            {
                if (_clone_name != value)
                {
                    this.RaiseAndSetIfChanged(ref _clone_name, value);
                    this.RaisePropertyChanged(nameof(HasName));
                }
            }
        }

        public bool HasName { get => !string.IsNullOrWhiteSpace(_org_name); }
    }
}