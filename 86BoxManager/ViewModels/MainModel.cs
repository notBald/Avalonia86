using _86BoxManager.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DynamicData;
using DynamicData.Binding;
using Avalonia.Threading;
using _86BoxManager.Core;

namespace _86BoxManager.ViewModels
{
    internal class MainModel : ReactiveObject, IDisposable
    {
        /// <summary>
        /// Needed util this feature gets implemented:
        /// https://github.com/AvaloniaUI/Avalonia/issues/17029
        /// </summary>
        public static readonly VMVisual Dummy = new VMVisual();

        private readonly Machine _machine;

        #region State information

        private bool _vm_is_sel = false, _sel_vm_run = false, _sel_vm_waiting = false, _sel_vm_paused = false;

        public bool VmIsSelected 
        { 
            get => _vm_is_sel;
            //Use of Dispatcher here is a workaround for a Avalonia layout bug. This means that layout that
            //depends on this particular flag will be done on the next cycle. Unfortunatly, this means that
            //no code should depend on this flag, as it might not have been updated yet.
            //set => Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _vm_is_sel, value));

            //The correct code:
            set => this.RaiseAndSetIfChanged(ref _vm_is_sel, value);
        }
        public bool SelVmRunning { get => _sel_vm_run;
            //set => Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _sel_vm_run, value));
            set => this.RaiseAndSetIfChanged(ref _sel_vm_run, value); 
        }
        public bool SelVmWating { get => _sel_vm_waiting;
            //set => Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _sel_vm_waiting, value));
            set => this.RaiseAndSetIfChanged(ref _sel_vm_waiting, value); 
        }
        public bool SelVmPaused { get => _sel_vm_paused;
            //set => Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _sel_vm_paused, value));
            set => this.RaiseAndSetIfChanged(ref _sel_vm_paused, value);
        }

        #endregion

        public IEnumerable<VMVisual> AllMachines => Settings.Machines.Items;
        private readonly ReadOnlyObservableCollection<VMVisual> _filtered_machines;

        public ReadOnlyObservableCollection<VMVisual> Machines => _filtered_machines;

        private VMCategory DefaultCat { get => Settings.DefaultCat; }
        private readonly ReadOnlyObservableCollection<VMCategory> _filtered_categories;

        public ReadOnlyObservableCollection<VMCategory> Categories { get => _filtered_categories; }

        private VMConfig _current_config = new VMConfig();

        public AppSettings Settings => AppSettings.Settings;

        /// <summary>
        /// Reactive object that is used by the frontend to extract data from RawDict
        /// </summary>
        /// <remarks>
        /// This implementation is a little odd.
        /// 
        /// Every time you click on a machine, the view is naturally changed to that machine.
        /// However, the info panel about the machine will not be updated(*). This is perhaps
        /// not ideal, but we know that there is a background thread collecting the latest
        /// configuration data.
        /// 
        /// Once that data is collected, it will set a new RawConfig dictionary on the VMRow
        /// object and raise a property changed on this object for this property.
        /// 
        /// Now, to get the frontend the refresh all its bindings, we replace the VMConfig.
        /// The alternative is to raise "property changed" events on all its properties.
        /// 
        /// One drawback with this design is that if anything goes amiss in the background
        /// thread, the info table will not be updated
        ///  - Is this a real problem? Keep in mind that if the info table can't be updated,
        ///    it's hardly a big deal.
        ///    
        /// (*) A Property changed "VMConfig" can be raised in the function MainModel_PropertyChanged,
        /// right after the "Update" call. However, this will cause the Info panel to refresh
        /// twice over in most cases. (Unless you change things so that this function don't 
        /// recreate VMConfig but raise individual property changed events, however that does
        /// not change the fact that the data might be wrong, causing the info panel first to
        /// be updated with wrong data, then right data, as opposed to simply waiting for the
        /// right data to come along, as we're doing now.)
        /// </remarks>
        public VMConfig VMConfig
        {
            get
            {
                var con = _selected_machine.VMConfig;
                if (con != null)
                {
                    //IsSameDict checks if the internal RawConfig is the same as the one we
                    //got. If it's the same, it'd be a waste to have the InfoPanel refresh.
                    if (!_current_config.IsSameDict(con))
                        _current_config = new VMConfig(con);                        
                }

                return _current_config;
            }
        }

        private int _vm_idx = -1;
        public int MachineIndex 
        { 
            get => _vm_idx;
            set
            {
                if (value < 0 || value >= Settings.Machines.Count)
                    _selected_machine = Dummy;
                else
                    _selected_machine = Machines[value];
                if (_vm_idx != value)
                    this.RaisePropertyChanged(nameof(Machine));
                this.RaiseAndSetIfChanged(ref _vm_idx, value);
            }
        }

        private VMVisual _selected_machine = Dummy;
        public VMVisual Machine
        {
            get => _selected_machine;
            set { MachineIndex = Machines.IndexOf(value); }
        }

        private int _cat_idx = 0;
        public int CategoryIndex { get => _cat_idx; set => this.RaiseAndSetIfChanged(ref _cat_idx, value); }

        public string CategoryName 
        { 
            get
            {
                if (_cat_idx < -1 || _cat_idx > Categories.Count)
                    return null;
                return Categories[_cat_idx].Name;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    CategoryIndex = 0;
                else
                {
                    var co = Settings.Categories.Lookup(value);
                    if (!co.HasValue)
                        CategoryIndex = 0;
                    else
                    {
                        CategoryIndex = Categories.IndexOf(co.Value);
                        if (_cat_idx == -1)
                            CategoryIndex = 0;
                    }
                }
            }
        }

        public bool CompactList
        {
            get => Settings.CompactMachineList;
        }

        public bool IsTrayEnabled { get => Settings.IsTrayEnabled; }

        public string ApplicationTheme { get => Settings.Theme.ToString(); }

        private frmMain _ui;
        public frmMain UI {  get => _ui; set { if (_ui == null) _ui = value; } }

        private string _vmCount = "# of virtual machines:";

        public string VmCount
        {
            get => _vmCount;
            set
            {
                _vmCount = value;
                this.RaisePropertyChanged();
            }
        }

        private int _all_vms, _all_running_vms, _running_vms, _paused_vms, _waiting_vms, _stopped_vms;

        public int AllVmCount { get => _all_vms; set => this.RaiseAndSetIfChanged(ref _all_vms, value); }
        public int AllRunningVMs { get => _all_running_vms; set => this.RaiseAndSetIfChanged(ref _all_running_vms, value); }
        public int RunningVMs { get => _running_vms; set => this.RaiseAndSetIfChanged(ref _running_vms, value); }
        public int PausedVMs { get => _paused_vms; set => this.RaiseAndSetIfChanged(ref _paused_vms, value); }
        public int WaitingVMs { get => _waiting_vms; set => this.RaiseAndSetIfChanged(ref _waiting_vms, value); }
        public int StoppedVMs { get => _stopped_vms; set => this.RaiseAndSetIfChanged(ref _stopped_vms, value); }

        public MainModel()
        {
            _machine = new Machine(this);
            Settings.RefreshVMs();
            Settings.RefreshCats();

            Settings.Machines.Connect()
                .Filter(x => _cat_idx == 0 || x.VMCat.IsChecked)
                .Sort(SortExpressionComparer<VMVisual>.Ascending(x => x.OrderIndex))
                .Bind(out _filtered_machines)
                .Subscribe();

            var cat2 = Settings.Categories;

            cat2.AddOrUpdate(DefaultCat);

            cat2.Connect()
                .Sort(SortExpressionComparer<VMCategory>.Ascending(x => x.OrderIndex))
                .Bind(out _filtered_categories)
                .Subscribe();

            this.PropertyChanged += MainModel_PropertyChanged;
            Settings.PropertyChanged += Settings_PropertyChanged;
        }

        public void Dispose()
        {
            _machine.Dispose();
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppSettings.CompactMachineList))
            {
                this.RaisePropertyChanged(nameof(CompactList));
            }
            else if (e.PropertyName == nameof(AppSettings.IsTrayEnabled))
            {
                this.RaisePropertyChanged(nameof(IsTrayEnabled));
            }
            else if (e.PropertyName == nameof(AppSettings.Theme))
            {
                this.RaisePropertyChanged(nameof(ApplicationTheme));
            }
        }

        /// <summary>
        /// Refreshes the machine currently being viewed.
        /// </summary>
        public void RefreshMachine()
        {
            _machine.ClearCurrent();
            this.RaisePropertyChanged(nameof(MachineIndex));
        }

        private void MainModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MachineIndex) && _vm_idx >= 0) 
            {
                Settings.RefreshVisual(Machine.Tag.UID);
                Settings.RefreshTime(Machine);
                _machine.Update(Machine);
            }
            else if (_cat_idx >= 0 && e.PropertyName == nameof(CategoryIndex))
            {
                foreach (var cat in Settings.Categories.Items)
                    cat.IsChecked = false;

                var c = Settings.Categories.Lookup(CategoryName);
                if (c.HasValue)
                    c.Value.IsChecked = true;

                Settings.Machines.Refresh();
            }
        }

        #region Menu commands

        public void ExitApplicationCmd()
        {
            //Will cause Window_OnClosing to be called.
            _ui.ExitApplicationCmd();
        }

        public void AddVMCmd()
        {
            _ui.AddVMCmd();
        }

        public void DelVMCmd()
        {
            _ui.DelVMCmd();
        }

        public void StartVMCmd()
        {
            _ui.StartVMCmd();
        }

        public void SoftResetVMCmd()
        {
            _ui.SoftResetVMCmd();
        }

        public void ResetVMCmd()
        {
            _ui.ResetVMCmd();
        }

        public void ConfigVMCmd()
        {
            _ui.ConfigVMCmd();
        }

        public void ConfigAppCmd()
        {
            _ui.ConfigAppCmd();
        }

        public void EditVMCmd()
        {
            _ui.EditVMCmd();
        }

        #endregion
    }
}