using _86BoxManager.Core;
using _86BoxManager.Tools;
using _86BoxManager.Xplat;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ButtonsType = MsBox.Avalonia.Enums.ButtonEnum;
using IOPath = System.IO.Path;
using MessageType = MsBox.Avalonia.Enums.Icon;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;

namespace _86BoxManager.Views
{
    public partial class dlgSettings : Window
    {
        private readonly dlgSettingsModel _m;
        private bool _was_cancled;

        public dlgSettings()
        {
            InitializeComponent();
            Closing += dlgSettings_FormClosing;

            _m = new dlgSettingsModel();
            DataContext = _m;

            _m.PropertyChanged += _m_PropertyChanged;
        }

        private void _m_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(dlgSettingsModel.ExeDir))
            {
                Get86BoxVersion();
            }
        }

        private async void dlgSettings_Load(object sender, EventArgs e)
        {
#if DEBUG
            if (Design.IsDesignMode)
                return;
#endif

            try
            {
                LoadSettings();
                Get86BoxVersion();
            }
            catch
            {
                await Dialogs.ShowMessageBox("Settings could not be loaded, sorry.",
                         MessageType.Error, this, ButtonsType.Ok, "Failure");

                Close();
            }
        }

        private async void dlgSettings_FormClosing(object sender, WindowClosingEventArgs e)
        {
            if (!_m.HasChanges || _was_cancled)
                return;

            e.Cancel = true;

            // Unsaved changes, ask the user to confirm
            var result = await Dialogs.ShowMessageBox(
                "Would you like to save the changes you've made to the settings?",
                MessageType.Question, this, ButtonsType.YesNo, "Unsaved changes");
            if (result == ResponseType.Yes)
            {
                await SaveSettings();
            }

            if (result != ResponseType.None)
            {
                Closing -= dlgSettings_FormClosing;
                Close();
            }
        }

        private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // We want to make the first row (index 0) not editable
            if (e.Row.Index == 0)
            {
                e.Cancel = true;
            }
        }

        private async void btnApply_Click(object sender, RoutedEventArgs e)
        {
            var success = await SaveSettings();
        }

        private async void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (_m.HasChanges)
            {
                await SaveSettings();
            }
            Close(ResponseType.Ok);
        }

        private async void btnBrowse3_Click(object sender, RoutedEventArgs e)
        {
            var dir = Platforms.Env.MyComputer;
            var title = "Select a file where 86Box logs will be saved";
            var filter = "Log files (*.log)|*.log";
            string fileName = null;

            try
            {
                fileName = await Dialogs.SaveFile(title, dir, filter, parent: this, ext: ".log");
            }
            catch
            {
                await Dialogs.ShowMessageBox("Failed to open file dialog.", MessageType.Error, this);
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                _m.LogPath = fileName;
            }
        }

        private async void btnDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = await Dialogs.ShowMessageBox("All settings will be reset to their default values. " +
                                                "Do you wish to continue?",
                MessageType.Warning, this, ButtonsType.YesNo, "Settings will be reset");
            if (result == ResponseType.Yes)
            {
                ResetSettings();
            }
        }

        // Resets the settings to their default values
        private void ResetSettings()
        {
            var (cfgPath, exePath) = VMCenter.FindPaths();
            _m.CFGDir = cfgPath;
            _m.ExeDir = exePath;
            _m.MinOnStart = false;
            _m.EnableConsole = true;
            _m.MinToTray = false;
            _m.CloseToTray = false;
            _m.EnableLogging = false;
            _m.LogPath = "";
            _m.AllowInstances = false;
            _m.CompactList = false;
            _m.RaisePropertyChanged(nameof(dlgSettingsModel.HasChanges));
        }

        private async void btnBrowse1_Click(object sender, RoutedEventArgs e)
        {
            var text = "Select a folder where 86Box program files and the roms folder are located";

            var fldName = await Dialogs.SelectFolder(_m.ExeDir, text, parent: this);

            if (!string.IsNullOrWhiteSpace(fldName))
            {
                _m.ExeDir = fldName.CheckTrail();
            }
        }

        private async void btnBrowse2_Click(object sender, RoutedEventArgs e)
        {
            var text = "Select a folder where your virtual machines (configs, nvr folders, etc.) will be located";

            var fileName = await Dialogs.SelectFolder(_m.CFGDir, text, parent: this);

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                _m.CFGDir = fileName.CheckTrail();
            }
        }

        // Obtains the 86Box version from 86Box executable
        private void Get86BoxVersion()
        {
            _m.ExeValid = false;
            _m.ExeError = false;
            _m.ExeWarn = false;
            _m.ExePath = "86Box executable not found!";

            try
            {
                var vi = Platforms.Manager.GetBoxVersion(_m.ExeDir);
                if (vi != null)
                {
                    if (vi.FilePrivatePart >= 3541) //Officially supported builds
                    {
                        _m.ExePath = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}.{vi.FilePrivatePart} - fully compatible";
                        _m.ExeValid = true;
                    }
                    else if (vi.FilePrivatePart >= 3333 && vi.FilePrivatePart < 3541) //Should mostly work...
                    {
                        _m.ExePath = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}.{vi.FilePrivatePart} - partially compatible";
                        _m.ExeWarn = true;
                    }
                    else //Completely unsupported, since version info can't be obtained anyway
                    {
                        _m.ExePath = "Unknown - may not be compatible";
                        _m.ExeError = true;
                    }
                }
            }
            catch { }

            _m.RaisePropertyChanged(nameof(dlgSettingsModel.ExeError));
            _m.RaisePropertyChanged(nameof(dlgSettingsModel.ExeWarn));
            _m.RaisePropertyChanged(nameof(dlgSettingsModel.ExeValid));
        }

        // Save the settings to the registry
        private async Task<bool> SaveSettings()
        {
            if (_m.EnableLogging && string.IsNullOrWhiteSpace(_m.LogPath))
            {
                var result = await Dialogs.ShowMessageBox(
                    "Using an empty or whitespace string for the log path will " +
                    "prevent 86Box from logging anything. Are you sure you want to use" +
                    " this path?",
                    MessageType.Warning, this, ButtonsType.YesNo, "Warning");
                if (result == ResponseType.No)
                {
                    return false;
                }
            }

            //var exeName = Platforms.Env.ExeNames.First();
            //var boxExe = IOPath.Combine(_m.ExeDir, exeName);
            //if (!File.Exists(boxExe) && _m.ExeDirChanged)
            //{
            //    var result = await Dialogs.ShowMessageBox(
            //        "86Box executable could not be found in the directory you specified, so " +
            //        "you won't be able to use any virtual machines. Are you sure you want " +
            //        "to use this path?",
            //        MessageType.Warning, this, ButtonsType.YesNo, "Warning");
            //    if (result == ResponseType.No)
            //    {
            //        return false;
            //    }
            //}

            try
            {
                //Store the new values, close the key, changes are saved
                var s = Program.Root.Settings;
                using (var t = s.BeginTransaction())
                {
                    string dir = _m.ExeDir;
                    if (dir != null)
                        dir = dir.CheckTrail();
                    s.EXEdir = dir;
                    dir = _m.CFGDir;
                    if (dir != null)
                        dir = dir.CheckTrail();
                    s.CFGdir = dir;
                    s.MinimizeOnVMStart = _m.MinOnStart;
                    s.ShowConsole = _m.EnableConsole;
                    s.MinimizeToTray = _m.MinToTray;
                    s.CloseTray = _m.CloseToTray;
                    s.EnableLogging = _m.EnableLogging;
                    s.LogPath = _m.LogPath;
                    s.AllowInstances = _m.AllowInstances;
                    s.CompactMachineList = _m.CompactList;

                    t.Commit();
                }

                //This will set of a chain of events.
                // - MainModel is listening and will notify if CompactMachineList has changed
                // - frmMain is listening to MainModel and will update the list if the CompactList
                //   property have changed.
                _m.NotifyPropertyChange(s);

                _m.Commit();
            }
            catch (Exception ex)
            {
                await Dialogs.ShowMessageBox("An error has occurred. Please provide the following information" +
                                       $" to the developer:\n{ex.Message}\n{ex.StackTrace}",
                    MessageType.Error, this, ButtonsType.Ok, "Error");
                return false;
            }
            finally
            {
                Get86BoxVersion(); //Get the new exe version in any case
            }
            return true;
        }

        /// <summary>
        /// Read the settings from the database
        /// </summary>
        private void LoadSettings()
        {
            var s = Program.Root.Settings;

            _m.ExeDir = s.EXEdir;
            _m.CFGDir = s.CFGdir;
            _m.LogPath = s.LogPath;
            _m.MinOnStart = s.MinimizeOnVMStart;
            _m.EnableConsole = s.ShowConsole;
            _m.MinToTray = s.MinimizeToTray;
            _m.CloseToTray = s.CloseTray;
            _m.EnableLogging = s.EnableLogging;
            _m.AllowInstances = s.AllowInstances;
            _m.CompactList = s.CompactMachineList;

            if (string.IsNullOrWhiteSpace(_m.ExeDir) || string.IsNullOrWhiteSpace(_m.CFGDir))
            {
                var (cfgPath, exePath) = VMCenter.FindPaths();
                if (string.IsNullOrWhiteSpace(_m.ExeDir))
                {
                    _m.ExeDir = exePath;
                }
                if (string.IsNullOrWhiteSpace(_m.CFGDir))
                {
                    _m.CFGDir = cfgPath;
                }
            
            }

            _m.Commit();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            _was_cancled = true;
            Close(ResponseType.Cancel);
        }

        private async void btnAddExe_Click(object sender, RoutedEventArgs e)
        {
            var win = new dlgAddExe();
            await Tools.Dialogs.RunDialog(this, win);
        }
    }

    internal class dlgSettingsModel : ReactiveObject
    {
        private string _exe_dir, _exe_path, _cfg_dir;
        private bool _min_start, _min_tray, _close_tray;
        private bool _enable_logging;
        private string _log_path;
        private bool _allow_instances, _enable_console;
        private bool _compact_list;

        dlgSettingsModel _me;

        public bool ExeValid { get; set; }
        public bool ExeWarn { get; set; }
        public bool ExeError { get; set; }

        public string Version { get => CurrentApp.VersionString; }

        public string Lisence
        {
            get
            {
                try
                {
                    var startupPath = CurrentApp.StartupPath;
                    var txt = Path.Combine(startupPath, "Resources", "LICENSE");
                    return File.ReadAllText(txt);
                }
                catch { return "MIT Lisence file not loaded."; }
            }
        }

        public string Authors
        {
            get
            {
                try
                {
                    var startupPath = CurrentApp.StartupPath;
                    var txt = Path.Combine(startupPath, "Resources", "AUTHORS");
                    return File.ReadAllText(txt);
                }
                catch { return "Author file not loaded."; }
            }
        }

        public bool HasChanges
        {
            get 
            {
                if (_me == null)
                    return false;

                return _me.ExeDir != ExeDir ||
                       _me.CFGDir != CFGDir ||
                       _me.MinToTray != MinToTray ||
                       _me.MinOnStart != MinOnStart ||
                       _me.EnableLogging != EnableLogging ||
                       _me.EnableConsole != EnableConsole ||
                       _me.AllowInstances != AllowInstances ||
                       _me.LogPath != LogPath ||
                       _me.CompactList != CompactList;
            }
        }

        public bool ExeDirChanged
        {
            get
            {
                return _me.ExeDir != ExeDir || _me.ExePath != ExePath;
            }
        }

        public string ExeDir
        {
            get => _exe_dir;
            set
            {
                if (_exe_dir != value)
                {
                    this.RaiseAndSetIfChanged(ref _exe_dir, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
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

        public string CFGDir
        {
            get => _cfg_dir;
            set
            {
                if (_cfg_dir != value)
                {
                    this.RaiseAndSetIfChanged(ref _cfg_dir, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
            }
        }

        public bool MinToTray
        {
            get => _min_tray;
            set
            {
                if (_min_tray != value)
                {
                    this.RaiseAndSetIfChanged(ref _min_tray, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
            }
        }

        public bool MinOnStart
        {
            get => _min_start;
            set
            {
                if (_min_start != value)
                {
                    this.RaiseAndSetIfChanged(ref _min_start, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
            }
        }

        public bool CloseToTray
        {
            get => _close_tray;
            set
            {
                if (_close_tray != value)
                {
                    this.RaiseAndSetIfChanged(ref _close_tray, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
            }
        }

        public bool CompactList
        {
            get => _compact_list;
            set
            {
                if (_compact_list != value)
                {
                    this.RaiseAndSetIfChanged(ref _compact_list, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
            }
        }

        public bool EnableLogging
        {
            get => _enable_logging;
            set
            {
                if (_enable_logging != value)
                {
                    this.RaiseAndSetIfChanged(ref _enable_logging, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
            }
        }

        public bool AllowInstances
        {
            get => _allow_instances;
            set
            {
                if (_allow_instances != value)
                {
                    this.RaiseAndSetIfChanged(ref _allow_instances, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
            }
        }

        public bool EnableConsole
        {
            get => _enable_console;
            set
            {
                if (_enable_console != value)
                {
                    this.RaiseAndSetIfChanged(ref _enable_console, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
            }
        }

        public string LogPath
        {
            get => _log_path;
            set
            {
                if (_log_path != value)
                {
                    this.RaiseAndSetIfChanged(ref _log_path, value);
                    this.RaisePropertyChanged(nameof(HasChanges));
                }
            }
        }

        public void NotifyPropertyChange(AppSettings s)
        {
            if (s.PropertyChanged != null)
            {
                if (_me.CompactList != CompactList)
                    s.PropertyChanged(s, new PropertyChangedEventArgs(nameof(AppSettings.CompactMachineList)));
            }
        }

        public void Commit()
        {
            _me = (dlgSettingsModel) MemberwiseClone();

            this.RaisePropertyChanged(nameof(HasChanges));
        }
    }
}