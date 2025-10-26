using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.ComponentModel;
using Avalonia86.API;
using Avalonia86.Core;
using Avalonia86.Tools;
using Avalonia86.ViewModels;
using Avalonia86.Xplat;
using IOPath = System.IO.Path;
using Avalonia;
using Avalonia.Input;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Threading.Tasks;
using System.IO;
using ReactiveUI;
using Avalonia86.Converters;
using Avalonia86.DialogBox;

namespace Avalonia86.Views;

public partial class frmMain : BaseWindow
{
    #region Menu Commands

    public void ExitApplicationCmd()
    {
        //Will cause Window_OnClosing to be called.
        Close();
    }

    public async void Update86Cmd()
    {
        await this.RunDialog(new dlgUpdater());
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

    internal MainModel Model => (MainModel)DataContext;

    private AppSettings Settings { get => AppSettings.Settings; }

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

    public frmMain() : base("main")
    {
        //Version number and debug string is applied to the window title, so we set it in code behind.
        Title = WindowTitle;

        //Presumably this will never actually change, but just in case
        DataContextChanged += FrmMain_DataContextChanged;

        InitializeComponent();
        BaseInit();
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
    }

    protected override void SetWindowParams()
    {
        var iw = Settings.ListWidth;
        if (iw.HasValue)
            gridSplitListMain.ColumnDefinitions[0].Width = new GridLength(iw.Value);
        iw = Settings.InfoWidth;
        if (iw.HasValue)
            gridSplitInfoStats.ColumnDefinitions[2].Width = new GridLength(iw.Value);
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
        else if (e.PropertyName == nameof(MainModel.IsTrayEnabled))
        {
            trayIcon.MakeVisible(Model.IsTrayEnabled);
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
                await this.ShowError("VMs and Settings will not be saved.", "Failed to create DataBase for settings.");
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

            await this.ShowError($@"The virtual machine ""{invVmName}"" could not be found. " +
                                   "It may have been removed or the specified name is incorrect.",
                                   "Virtual machine not found");
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


    private async void btnExe_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new dlgSettings();
        if (dlg.DataContext is dlgSettingsModel m)
        {
            m.SelectedTabIdx = 2;
            m.RaisePropertyChanged(nameof(dlgSettingsModel.SelectedTabIdx));
        }
        await this.RunDialog(dlg);
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
        if (Settings != null && Settings.CloseTray && Settings.IsTrayEnabled)
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
            var result = await this.ShowQuestion("Some virtual machines are still running. It's " +
                                                 "recommended you stop them first before closing " +
                                                 "86Box Manager.\n\nDo you want to stop them now?",
                                                 "Virtual machines are still running", "Do you want to stop them now?");
            if (result != DialogResult.None)
            {
                if (result == DialogResult.Yes)
                    await VMCenter.CloseAllWindows(this);

                //With all VMs hopefully closed, we do a proper close. This will result in this method being called again.
                do_close = true;
                Close();

                return;
            }
        }
    }

    protected override void SaveWindowParams()
    {
        Settings.ListWidth = gridSplitListMain.ColumnDefinitions[0].Width.Value;
        Settings.InfoWidth = gridSplitInfoStats.ColumnDefinitions[2].Width.Value;
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

    private async void btnTest_Click(object sender, RoutedEventArgs e)
    {
        //var r = await this.ShowQuestion("Delete VM XXX", "Remove virtual machine", "Delete XXX");
        var r = await new DialogBoxBuilder(this).WithButtons(DialogButtons.YesNo)
            .WithCheckbox("Also delete files")
            .WithIcon(DialogIcon.Question)
            .WithHeader("Remove virtual machine", "Delete XXX")
            .WithMessage("Here's my message")
            .ShowDialog();
        if (r == DialogResult.Yes)
        {
            NativeMSG.Msg("Was YES", "OK");
        }
        if (r == DialogResult.YesChecked)
        {
            await this.ShowMsg("Was Checked", "OK");
        }
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
                await this.ShowError($"The folder you selected is already used by the VM \"{name}\"", "Folder already in use");
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

            await this.ShowMsg($@"A desktop shortcut for the virtual machine ""{vm.Name}"" " +
                                    "was successfully created.");
        }
        catch
        {
            await this.ShowError($@"A desktop shortcut for the virtual machine ""{vm.Name}"" could" +
                                   " not be created.");
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
            var result = await this.ShowQuestion("Some virtual machines are still running. " +
                                                 "It's recommended you stop them first before " +
                                                 "closing 86Box Manager. Do you want to stop them now?",
                                                 "Virtual machines are still running");
            if (result == DialogResult.Yes)
            {
                await VMCenter.CloseAllWindows(this);

            }
            else if (result == DialogResult.None)
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
            if (WindowState == WindowState.Minimized && Settings.MinimizeToTray && Settings.IsTrayEnabled)
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
        var icons = app?.GetValue(TrayIcon.IconsProperty);
        trayIcon = (icons == null || icons.Count == 0) ? null : icons[0];
        if (trayIcon is { Menu: { } })
        {
            cmsTrayIcon = trayIcon.Menu;

            if (Settings.IsTrayEnabled)
                trayIcon.MakeVisible(true);
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
    private void lstVMs_MouseDoubleClick(object sender, TappedEventArgs e)
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