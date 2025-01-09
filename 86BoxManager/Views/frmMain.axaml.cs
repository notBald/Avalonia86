using System;
using _86BoxManager.Models;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using _86BoxManager.API;
using _86BoxManager.Core;
using _86BoxManager.Tools;
using _86BoxManager.ViewModels;
using _86BoxManager.Xplat;
using IOPath = System.IO.Path;
using ButtonsType = MsBox.Avalonia.Enums.ButtonEnum;
using MessageType = MsBox.Avalonia.Enums.Icon;
using ResponseType = MsBox.Avalonia.Enums.ButtonResult;
using System.Threading;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Windows.Input;
using Avalonia.Styling;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Threading.Channels;
using System.Threading.Tasks;
using MsBox.Avalonia.Dto;
using System.IO;
using ReactiveUI;
using Avalonia.Controls.Documents;
using System.Collections.Generic;
using _86BoxManager.Converters;
using System.Reactive.Linq;

namespace _86BoxManager.Views
{
    public partial class frmMain : Window
    {
        #region Menu Commands

        public void ExitApplicationCmd()
        {
            //Will cause Window_OnClosing to be called.
            Close();
        }

        public void AddVMCmd()
        {
            btnAdd_Click(null, null);
        }

        public void DelVMCmd()
        {
            btnDelete_Click(null, null);
        }

        public void StartVMCmd()
        {
            btnStart_Click(null, null);
        }

        public void SoftResetVMCmd()
        {
            btnCtrlAltDel_Click(null, null);
        }

        public void ResetVMCmd()
        {
            btnReset_Click(null, null);
        }

        public void ConfigVMCmd()
        {
            btnConfigure_Click(null, null);
        }

        public void ConfigAppCmd()
        {
            btnSettings_Click(null, null);
        }

        public void EditVMCmd()
        {
            btnEdit_Click(null, null);
        }

        #endregion

        #region Private fields

        private Size RestoreSize;
        private PixelPoint OldPos, NewPos;

        #endregion

        internal MainModel Model => (MainModel)DataContext;

        internal AppSettings Settings { get; private set; }

        public static string WindowTitle
        {
            get
            {
                string title = "Avalonia 86";

                if (!Design.IsDesignMode)
                {

                    title += " - " + CurrentApp.VersionString;
                }

                return title;
            }
        }

        public frmMain()
        {
            //Version number and debug string is applied to the window title, so we set it in code behind.
            Title = WindowTitle;

            //Restore size is a feature missing in Avalonia, so we do it ourselves. The basic problem is that
            //we need to know the size of the window before it was maximized when saving the window size.
            RestoreSize = new Size(Width, Height);
            OldPos = NewPos = Position;

            //Presumably this will never actually change, but just in case
            DataContextChanged += FrmMain_DataContextChanged;

            InitializeComponent();
            DataContext = new MainModel();

            //This binding must be done after DataContext is set. I don't want to set DataContext before InitializeComponent,
            //as that causes issues in ctrlMachineInfo
            //
            // Binding replaced: #lstVMs.((vm:MainModel)DataContext).CompactList
            // In a future version of Avalonia, this code might be replaced with:
            //  #lstVMs.((vm:MainModel)DataContext)?.CompactList
            //
            // https://github.com/AvaloniaUI/Avalonia/issues/17029
            if (lstVMs.ItemTemplate is MachineTemplateSelector mts)
            {
                
                var dc = (MainModel)DataContext;
                mts.CompactMachine = dc.CompactList;

                dc.PropertyChanged += (o, e) =>
                {
                    if (e.PropertyName == nameof(MainModel.CompactList))
                        mts.CompactMachine = dc.CompactList;
                };
            }

            //This is where the window size is restored
            try
            {
                if (!Design.IsDesignMode)
                    SetWindowSize();
            } catch { }

            //We need to catch changes in window state.
            PropertyChanged += FrmMain_PropertyChanged;

            //Multibinding can not be set on classes, so we do it here instead.
            //
            //This is in effect this code:
            //<Button.Classes.stop>
            //  <MultiBinding Converter = "{x:Static BoolConverters.And}">
            //      <Binding Path = "VmIsSelected"/>
            //      <Binding Path = "SelVmRunning"/>
            //      <Binding Path = "!SelVmWating"/>
            //      <Binding Path = "!SelVmPaused"/>
            //  </MultiBinding>
            //</Button.Classes.stop>
            var mb = new MultiBinding()
            {
                Bindings = [new Binding("VmIsSelected"), new Binding("SelVmRunning"), new Binding("!SelVmWating"), new Binding("!SelVmPaused")],
                Converter = BoolConverters.And
            };
            btnStart.BindClass("stop", mb, null);
            //Now we do the "resume" class
            mb = new MultiBinding()
            {
                Bindings = [new Binding("VmIsSelected"), new Binding("SelVmRunning"), new Binding("!SelVmWating"), new Binding("SelVmPaused")],
                Converter = BoolConverters.And
            };
            btnStart.BindClass("resume", mb, null);


            //Workaround for missing RestoreSize property in Avalonia
            // https://github.com/AvaloniaUI/Avalonia/issues/5285#issuecomment-1764175742
            //In addition to this, we also need the old window position. We take advantage of the fact that the
            //window will not move once maximized, so we add a handler that always saves away an "oldpos".
            //
            //One problem with this implementation is that we don't handle events where the screen layout
            //changes, such as when a screen is removed. There aren't really any good ways of handling
            //this in Avalonia.
            this.GetPropertyChangedObservable(ClientSizeProperty).AddClassHandler<Visual>((t, args) =>
            {
                if (WindowState == WindowState.Normal && args.OldValue is Size rs)
                {
                    //Note that we store the "old value". This way we get the position before the
                    //window was maximized, as this event will fire with the Max size in NewValue
                    //and "WindowState == WindowState.Normal", annoyingly enough.
                    RestoreSize = rs;
                }
            });
            PositionChanged += (s, e) =>
            {
                OldPos = NewPos;
                NewPos = e.Point;
            };
        }

        /// <summary>
        /// Sets the window size, but makes sure not to set the window in a bad location.
        /// </summary>
        private void SetWindowSize()
        {
            var size = Core.DBStore.FetchWindowSize();

            //If size for some reason fails to load, we do nothing. The window will the open with
            //default size.
            if (size != null && size.Width > 50 && size.Height > 50)
            {
                var left_pos = new PixelPoint((int)size.Left, (int)size.Top);
                var right_pos = new PixelPoint((int)(size.Left + size.Width), (int)size.Top);
                var windowRect = new PixelRect(left_pos, new PixelSize((int)size.Width, (int)size.Height));
                double windowArea = windowRect.Width * windowRect.Height * 0.5;
                double totalIntersectionArea = 0;
                bool isPositionValid = false;

                //What we want is to furfill two conditions.
                // 1. That the top/left position on the window is visible on at least one screen.
                //    The goal here is to avoid situations where the top of the window is above
                //    the screen.
                // 2. At least 50% of the window is visible on all screens combinded. Maybe we can
                //    reduse this number, as what we want is a decent chunk of the app visible.
                foreach (var screen in Screens.All)
                {
                    var intersection = screen.Bounds.Intersect(windowRect);
                    totalIntersectionArea += intersection.Width * intersection.Height;

                    if (screen.Bounds.Contains(left_pos) || screen.Bounds.Contains(right_pos))
                        isPositionValid = true;
                }

                //Note that "windowArea" referes to the size of the app's window, and we've halved it
                //so that we'll pass the check with half the window intersecting with all screens.
                if (totalIntersectionArea >= windowArea && isPositionValid)
                {
                    Position = left_pos;
                    Width = size.Width;
                    Height = size.Height;
                    if (size.Maximized)
                        WindowState = WindowState.Maximized;

                    var iw = Settings.ListWidth;
                    if (iw.HasValue)
                        gridSplitListMain.ColumnDefinitions[0].Width = new GridLength(iw.Value);
                    iw = Settings.InfoWidth;
                    if (iw.HasValue)
                        gridSplitInfoStats.ColumnDefinitions[2].Width = new GridLength(iw.Value);
                }
            }
        }

        /// <summary>
        /// Sets itself to the data context, so that command bindings are easier to implement.
        /// </summary>
        private void FrmMain_DataContextChanged(object sender, EventArgs e)
        {
            var dc = DataContext as MainModel;
            if (dc != null)
            {
                dc.UI = this;
                Settings = dc.Settings;
                UpdateState();

                dc.PropertyChanged += Dc_PropertyChanged;
            }
        }

        private void Dc_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainModel.MachineIndex))
            {
                UpdateState();
            }
            else if (e.PropertyName == nameof(MainModel.CompactList))
            {
                //Hack. I don't know how to properly do this. What we do is
                //      forcing a property changed event on ItemTemplate
                var hold = lstVMs.ItemTemplate;
                lstVMs.ItemTemplate = null;
                lstVMs.ItemTemplate = hold;
            }
        }

        //private static bool CheckRunningEmulator()
        //{
        //    return Platforms.Manager.IsProcessRunning("86box") ||
        //                    Platforms.Manager.IsProcessRunning("86Box");
        //}

        private async void Main_OnOpened(object sender, EventArgs e)
        {
            //It would be a good idea to open the dialog before building up the window. Problem is, Avalonia dosn't like Windows before the app has loaded.
            //Setting this window to not visible works, but looks weird. There are probably other options, but looking into details on how Avalonia starts
            //up isn't worth it for one single message. So, we do a dialog that locks the main window before loading in VMs. 
            //if (CheckRunningEmulator())
            //{
            //    var result = await Dialogs.ShowMessageBox("At least one instance of 86Box is already running. It's\n" +
            //                                        "not recommended that you run 86Box directly outside of\n" +
            //                                        "Manager. Do you want to continue at your own risk?",
            //        MessageType.Warning, this, ButtonsType.YesNo, "Warning");
            //    if (result == ResponseType.No)
            //    {
            //        Close();
            //        return;
            //    }
            //}

            if (Program.Root == null)
            {
                Program.Root = this;
                await frmMain_Load(sender, e);
            }
        }
        
        private async Task<bool> frmMain_Load(object sender, EventArgs e)
        {
            if (!Design.IsDesignMode)
            {
                if (DBStore.InMemDB)
                {
                    await Dialogs.ShowMessageBox("VMs and Settings will not be saved.", MessageType.Warning, this, ButtonsType.Ok, "Failed to create DataBase for settings.");
                }
            }

            PrepareUi();
            VMCenter.CountRefresh();

            msgHandler = new VMHandler();
            msgSink = Platforms.Manager.GetLoop(msgHandler);
            var handle = msgSink.GetHandle();

            //Convert the current window handle to a form that's expected by 86Box
            hWndHex = $"{handle.ToInt64():X}";
            hWndHex = hWndHex.PadLeft(16, '0');

            //Check if command line arguments for starting a VM are OK
            if (Program.GetVmArg(Program.Args, out var invVmName))
            {
                //Find the VM with given name
                var ids = Model.Settings.NameToIds(invVmName);

                //Then select and start it if it's found
                if (ids != null && ids.Length > 0)
                {
                    //Goes with the first hit, as names are not unique
                    var v = Model.Settings.RefreshVisual(ids[0]);

                    if (v != null)
                    {
                        //Starts the machine
                        VMCenter.Start(v, this);

                        //Selects the machine
                        Model.Machine = v;

                        return true;
                    }
                }

                await Dialogs.ShowMessageBox($@"The virtual machine ""{invVmName}"" could not be found. " +
                                       "It may have been removed or the specified name is incorrect.",
                    MessageType.Error, this, ButtonsType.Ok, "Virtual machine not found");
            }

            return true;
        }

        private void BringToFront()
        {
            this.BringIntoView();
        }

        private void Screenshots_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            VMCenter.OpenScreenshotsFolder(Model.Machine, this);
        }

        private async void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            await this.RunDialog(new dlgAddVM());
        }

        private async void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            await this.RunDialog(new dlgEditVM() { VM = Model.Machine });
        }

        private void btnTray_Click(object sender, RoutedEventArgs e)
        {
            VMCenter.OpenTrayFolder(Model.Machine, this);
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var vis = Model.Machine;
            if (vis.Status == MachineStatus.STOPPED)
            {
                VMCenter.Start(this);
            }
            else if (vis.Status == MachineStatus.RUNNING)
            {
                VMCenter.RequestStop(vis, this);
            }
            else if (vis.Status == MachineStatus.PAUSED)
            {
                VMCenter.Resume(vis, this);
            }
        }

        private async void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            await this.RunDialog(new dlgSettings());
        }

        /// <summary>
        /// Updates the state used by the UI to enable / disable buttons and menues.
        /// </summary>
        public void UpdateState()
        {
            //Code used after selecting a VM
            // Disable relevant buttons if no VM is selected
            var select = (ListBox)lstVMs;
            var dc = DataContext as MainModel;
            if (dc == null) return;

            if (Model.MachineIndex == -1)
            {
                dc.VmIsSelected = false;
                dc.SelVmRunning = false;
                dc.SelVmPaused = false;
                dc.SelVmWating = false;
                return;
            }
            else
            {
                dc.VmIsSelected = true;

                //Disable relevant buttons if VM is running
                var vm = Model.Machine;
                if (vm.Status == MachineStatus.RUNNING)
                {
                    dc.SelVmRunning = true;
                    dc.SelVmPaused = false;
                    dc.SelVmWating = false;
                }
                else if (vm.Status == MachineStatus.STOPPED)
                {
                    dc.SelVmRunning = false;
                    dc.SelVmPaused = false;
                    dc.SelVmWating = false;
                }
                else if (vm.Status == MachineStatus.PAUSED)
                {
                    dc.SelVmRunning = true;
                    dc.SelVmPaused = true;
                    dc.SelVmWating = false;
                }
                else if (vm.Status == MachineStatus.WAITING)
                {
                    //This condition happens when the configuration dialog is opened in the emulator
                    dc.SelVmRunning = true;
                    dc.SelVmPaused = vm.IsPaused;
                    dc.SelVmWating = true;
                }
                return;
            }
        }

        internal string hWndHex = ""; //Window handle of this window

        private IMessageReceiver msgHandler;
        private IMessageLoop msgSink;

        private void btnConfigure_Click(object sender, RoutedEventArgs e)
        {
            VMCenter.Configure();
        }

        bool do_close = false;

        private async void Window_OnClosing(object sender, WindowClosingEventArgs e)
        {
            // Closing 86Box Manager before closing all the VMs can lead to weirdness if 86Box Manager is then restarted. 
            // So let's warn the user just in case and request confirmation.

            //Close to tray
            if (Settings != null && Settings.CloseTray)
            {
                e.Cancel = true;
                if (trayIcon != null)
                    trayIcon.MakeVisible(true);
                Hide();

                return;
            }

            //If there are running VMs, display the warning and stop the VMs if user says so
            if (VMCenter.IsWatching && !do_close)
            {
                //It's important that the event is canceld before we call await, otherwise the close will proceed.
                e.Cancel = true;
                var result = await Dialogs.ShowMessageBox("Some virtual machines are still running. It's " +
                                                    "recommended you stop them first before closing " +
                                                    "86Box Manager. Do you want to stop them now?",
                    MessageType.Warning, this, ButtonsType.YesNo, "Virtual machines are still running");
                if (result == ResponseType.Yes)
                {
                    await VMCenter.CloseAllWindows(this);

                    //With all VMs hopefully closed, we do a proper close. This will result in this method being called again.
                    do_close = true;
                    Close();

                    return;
                }
            }

            using (var t = Settings.BeginTransaction())
            {
                if (WindowState == WindowState.Maximized)
                    DBStore.UpdateWindow(OldPos.Y, OldPos.X, RestoreSize.Height, RestoreSize.Width, true);
                else
                    DBStore.UpdateWindow(Position.Y, Position.X, Height, Width, false);

                try
                {
                    Settings.ListWidth = gridSplitListMain.ColumnDefinitions[0].Width.Value;
                    Settings.InfoWidth = gridSplitInfoStats.ColumnDefinitions[2].Width.Value;
                }
                catch { }

                t.Commit();
            }
        }

        /// <summary>
        /// Called from the Exit event handler. Do not call this methid directly, instead call
        /// "App.Quit();"
        /// </summary>
        internal void Exit()
        {
            Model.Dispose();
            trayIcon.MakeVisible(false);
        }

        private void btnNextImg_Click(object sender, RoutedEventArgs e)
        {
            var vm = Model.Machine;
            if (vm != null)
                vm.SelectedImageIndex++;
        }

        private void btnPrevImg_Click(object sender, RoutedEventArgs e)
        {
            var vm = Model.Machine;
            if (vm != null)
                vm.SelectedImageIndex--;
        }

        private void btnCtrlAltDel_Click(object sender, RoutedEventArgs e)
        {
            VMCenter.CtrlAltDel(Model.Machine, this);
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            VMCenter.HardReset(Model.Machine);
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            var vis = Model.Machine;
            if (vis.Status == MachineStatus.PAUSED)
            {
                VMCenter.Resume(Model.Machine, this);
            }
            else if (vis.Status == MachineStatus.RUNNING)
            {
                VMCenter.Pause(Model.Machine, this);
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            VMCenter.Remove(Model.Machine, this);
        }

        private async void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var text = "Select the folder where the virtual machine's (configs, nvr folders, etc.) is located";
            long uid = Model.Machine.Tag.UID;

            var fldName = await Dialogs.SelectFolder(Model.Settings.CFGdir, text, parent: this);

            if (!string.IsNullOrWhiteSpace(fldName) && Directory.Exists(fldName))
            {
                var name = Model.Settings.PathToName(fldName);
                if (name != null)
                {
                    await Dialogs.ShowMessageBox($"The folder you selected is already used by the VM \"{name}\"", MessageType.Error, this, ButtonsType.Ok, "Folder already in use");
                }
                else
                {
                    var vm = Model.Settings.RefreshVisual(uid);
                    vm.Path = fldName;

                    Model.RefreshMachine();
                }
            }
        }

        private void openConfigFileToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = Model.Machine;
            VMCenter.OpenConfig(selected, this);
        }

        private async void killToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = Model.Machine;
            await VMCenter.Kill(selected, this);
        }

        private async void wipeToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = Model.Machine;
            await VMCenter.Wipe(selected, this);
        }

        private async void cloneToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var vm = Model.Machine;

            await this.RunDialog(new dlgCloneVM(vm.Path));
        }

        private void pauseToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var vm = Model.Machine;
            if (vm.Status == MachineStatus.PAUSED)
            {
                VMCenter.Resume(Model.Machine, this);
            }
            else if (vm.Status == MachineStatus.RUNNING)
            {
                VMCenter.Pause(Model.Machine, this);
            }
        }

        private void hardResetToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            VMCenter.HardReset(Model.Machine);
        }

        private void deleteToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            VMCenter.Remove(Model.Machine, this);
        }

        private async void editToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await this.RunDialog(new dlgEditVM() { VM = Model.Machine });
        }

        private void openFolderToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = Model.Machine;
            VMCenter.OpenFolder(selected, this);
        }

        private void configureToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            VMCenter.Configure();
        }

        private void resetCTRLALTDELETEToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            VMCenter.CtrlAltDel(Model.Machine, this);
        }

        // Start VM if it's stopped or stop it if it's running/paused
        private void startToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var vm = Model.Machine;
            if (vm.Status == MachineStatus.STOPPED)
            {
                VMCenter.Start(this);
            }
            else if (vm.Status == MachineStatus.RUNNING || vm.Status == MachineStatus.PAUSED)
            {
                VMCenter.RequestStop(Model.Machine, this);
            }
        }

        private async void createADesktopShortcutToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var vm = Model.Machine.Tag;
            try
            {
                var desktop = Platforms.Env.Desktop;
                var shortcutAddress = IOPath.Combine(desktop, $"{vm.Name}.lnk");
                var shortcutDesc = vm.Desc;
                var vmName = vm.Name;
                var startupPath = CurrentApp.StartupPath;

                Platforms.Shell.CreateShortcut(shortcutAddress, vmName, shortcutDesc, startupPath);

                await Dialogs.ShowMessageBox($@"A desktop shortcut for the virtual machine ""{vm.Name}"" " +
                                        "was successfully created.",
                    MessageType.Info, this, ButtonsType.Ok, "Success");
            }
            catch
            {
                await Dialogs.ShowMessageBox($@"A desktop shortcut for the virtual machine ""{vm.Name}"" could" +
                                        " not be created.",
                    MessageType.Error, this, ButtonsType.Ok, "Error");
            }
        }

        internal void open86BoxManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            BringToFront();
            trayIcon.MakeVisible(false);
        }

        internal async void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            BringToFront();
            trayIcon.MakeVisible(false);

            await this.RunDialog(new dlgSettings());
        }

        internal async void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // If there are running VMs, display the warning and stop the VMs if user says so
            if (VMCenter.IsWatching)
            {
                var result = await Dialogs.ShowMessageBox("Some virtual machines are still running. " +
                                                    "It's recommended you stop them first before " +
                                                    "closing 86Box Manager. Do you want to stop them now?",
                    MessageType.Warning, this, ButtonsType.YesNo, "Virtual machines are still running");
                if (result == ResponseType.Yes)
                {
                    await VMCenter.CloseAllWindows(this);

                }
                else if (result == ResponseType.Cancel)
                {
                    return;
                }
            }
            App.Quit();
        }

        // Handles things when WindowState changes
        private void FrmMain_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == WindowStateProperty)
            {
                if (WindowState == WindowState.Minimized && Settings.MinimizeToTray)
                {
                    trayIcon.MakeVisible(true);
                    Hide();
                    return;
                }

                if (WindowState == WindowState.Normal)
                {
                    Show();
                    trayIcon.MakeVisible(false);
                }
            }
        }

        private TrayIcon trayIcon;
        private NativeMenu cmsTrayIcon;

        private void PrepareUi()
        {
            var app = Application.Current;
            trayIcon = app?.GetValue(TrayIcon.IconsProperty).FirstOrDefault();
            if (trayIcon is { Menu: { } })
            {
                cmsTrayIcon = trayIcon.Menu;
            }
        }

        internal void trayIcon_MouseClick(object sender, EventArgs e)
        {
            if (IsVisible)
                return;

            //Restore the window and hide the tray icon
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            BringToFront();
            trayIcon.MakeVisible(false);
        }

        /// <summary>
        /// Starts/stops selected VM when enter is pressed
        /// </summary>
        private void lstVMs_KeyDown(object o, KeyEventArgs e)
        {
            var isEnter = e.Key is Key.Return or Key.Enter;
            if (isEnter && Model.MachineIndex != -1)
            {
                var vm = Model.Machine;
                if (vm.Status == MachineStatus.RUNNING)
                {
                    VMCenter.RequestStop(Model.Machine, this);
                }
                else if (vm.Status == MachineStatus.STOPPED)
                {
                    VMCenter.Start(this);
                }
            }
            var isDelete = e.Key is Key.Delete;
            if (isDelete && Model.MachineIndex != -1)
            {
                VMCenter.Remove(Model.Machine, this);
            }
        }

        /// <summary>
        /// For double clicking an item, do something based on VM status
        /// </summary>
        private void lstVMs_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (sender is ListBox box)
            {
                if (e.Source is Control ctrl && ctrl.DataContext is VMVisual v)
                {
                    if (v.Status == MachineStatus.STOPPED)
                    {
                        VMCenter.Start(v, this);
                    }
                    else if (v.Status == MachineStatus.RUNNING)
                    {
                        VMCenter.RequestStop(v, this);
                    }
                    else if (v.Status == MachineStatus.PAUSED)
                    {
                        VMCenter.Resume(v, this);
                    }
                }
            }
        }
    }
}