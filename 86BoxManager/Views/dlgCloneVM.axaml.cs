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
using System.Threading;
using Avalonia.Threading;
using Mono.Unix.Native;
using System.Security.Cryptography;

namespace _86BoxManager.Views
{
    public partial class dlgCloneVM : Window
    {
        // Path of the VM to be cloned
        private readonly string _oldPath;

        private readonly dlgCloneModel _m;
        private readonly AppSettings _s;
        private readonly long _uid;
        private bool _stop_cloning = false;

        public dlgCloneVM()
        {
            InitializeComponent();
            Loaded += dlgCloneVM_Load;

            //Windows 10 workarround
            NativeMSG.SetDarkMode(this);

            Loaded += DlgCloneVM_Loaded;
        }

        private void DlgCloneVM_Loaded(object sender, RoutedEventArgs e)
        {
            tbName.Focus();
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

            Closing += DlgCloneVM_Closing;
        }

        private async void DlgCloneVM_Closing(object sender, WindowClosingEventArgs e)
        {
            if (_m.IsWorking && !_stop_cloning)
            {
                e.Cancel = true;
                var resp = await Dialogs.ShowMessageBox("Cloning is in progress, do you really want to cancel it?", MessageType.Question, this, ButtonsType.YesNo);

                if (resp == ResponseType.Yes)
                {
                    _stop_cloning = true;
                }
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
            string old_path = null;

            try
            {
                var vis = _s.RefreshVisual(_uid);

                old_path = vis.Path;
            } catch { }

            if (old_path == null)
            {
                await Dialogs.ShowMessageBox($@"There was an error reading from the database, try again.",
                    MessageType.Error, this, ButtonsType.Ok, "Import failed");

                return;
            }

            _m.ProgressValue = 0;
            _m.IsWorking = true;
            _stop_cloning = false;
            ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadProc), new StartParams(new_path, old_path, _m.CloneName));
        }

        void ThreadProc(Object stateInfo)
        {
            var sp = (StartParams)stateInfo;
            bool importFailed = true;
            string error = null;

            try
            {
                FolderHelper.CopyFilesAndFolders(sp.OldPath, sp.Path, 1, (p) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _m.ProgressValue = p;
                    });

                    return !_stop_cloning;
                });
                importFailed = false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            Dispatcher.UIThread.Post(async () =>
            {
                _m.IsWorking = false;

                if (importFailed)
                {
                    await Dialogs.ShowMessageBox($@"Virtual machine could not be imported: {error}",
                        MessageType.Error, this, ButtonsType.Ok, "Import failed");

                    return;
                }

                try
                {
                    var vis = _s.RefreshVisual(_uid);

                    VMCenter.Add(sp.Name, sp.Path, vis.Desc, vis.Category, vis.IconPath, DateTime.Now, _s.IdToExeId(_uid), false, false, this);

                    var new_vis = _s.RefreshVisual(_s.PathToId(sp.Path));

                    new_vis.Comment = vis.Comment;
                }
                catch (Exception ex) { importFailed = true; error = ex.Message; }

                if (importFailed)
                {
                    await Dialogs.ShowMessageBox($@"Virtual machine was copied but could not be registered: {error}",
                        MessageType.Error, this, ButtonsType.Ok, "Import failed");

                    return;
                }

                await Dialogs.ShowMessageBox($@"Virtual machine ""{sp.Name}"" was successfully created, files " +
                                    "were imported. Remember to update any paths pointing to disk images in " +
                                    "your config!",
                MessageType.Info, this, ButtonsType.Ok, "Success");

                Close(ResponseType.Ok);
            });
        }

        private class StartParams
        {
            public readonly string OldPath;
            public readonly string Path;
            public readonly string Name;

            public StartParams(string path, string old_path, string name)
            {
                Path = path;
                OldPath = old_path;
                Name = name;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close(ResponseType.Cancel);
        }
    }

    internal class dlgCloneModel : ReactiveObject
    {
        private string _clone_name, _org_name;
        private bool _is_working;
        private int _p_value;

        public int ProgressValue
        {
            get => _p_value;
            set => this.RaiseAndSetIfChanged(ref _p_value, value);
        }

        public string OrgName { get => _org_name; set => this.RaiseAndSetIfChanged(ref _org_name, value); }

        public bool IsWorking { get => _is_working; set => this.RaiseAndSetIfChanged(ref _is_working, value); }

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