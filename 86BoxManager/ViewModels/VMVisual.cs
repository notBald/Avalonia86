using _86BoxManager.Core;
using _86BoxManager.Models;
using _86BoxManager.Tools;
using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace _86BoxManager.ViewModels
{
    internal sealed class VMVisual : ReactiveObject
    {
        private readonly VM _vm;
        private readonly AppSettings _s;
        private bool _has_valid_path = true;
        private MachineStatus _status;

        /// <summary>
        /// Used to determine whenever 86Box.conf should be parsed when the VM is running.
        /// </summary>
        /// <remarks>86Box makes changes to it's conf file all the time, so we need to filter out most changes</remarks>
        private bool _has_waited;

        //These are used to determine how often to update the time counters. The spans are 
        //short to ensure that time updates don't skip a minutte or hour.
        private static readonly TimeSpan _minutte = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _hour = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan _days = TimeSpan.FromDays(7);

        private TimeSpan _since_created = TimeSpan.MinValue / 10;
        private TimeSpan _since_run = TimeSpan.MinValue / 10;
        private TimeSpan _duration, _total_duration;
        private DateTime _created, _last_run;

        internal VM Tag => _vm;
        private TimeFields TM = new();

        /// <summary>
        /// Used when sorting categories
        /// </summary>
        internal VMCategory VMCat { get; set; }

        /// <summary>
        /// Used when sorting
        /// </summary>
        internal int OrderIndex = 0;

        private int _run_count;

        /// <summary>
        /// Linked VMs are not owned by Avalonia86 and has to be treated a little differently here and there.
        /// </summary>
        private bool IsLinked
        {
            get => _s.GetIsLinked(Tag.UID);
        }

        public int RunCount
        {
            get => _run_count;
            set => this.RaiseAndSetIfChanged(ref _run_count, value);
        }

        public RawConfig VMConfig { get; set; }

        public bool HasValidPath
        {
            get => _has_valid_path;
            private set => this.RaiseAndSetIfChanged(ref _has_valid_path, value);
        }

        /// <summary>
        /// Path to the folder where the VM is stored, returns "" if the directory does not exist.
        /// </summary>
        public string Path 
        {
            get
            {
                var rp = RawPath;

                if (!string.IsNullOrWhiteSpace(rp) && !Directory.Exists(rp))
                {
                    if (!IsLinked)
                    {
                        var folder_name = new DirectoryInfo(rp).Name;
                        var alt_path = System.IO.Path.Combine(_s.CFGdir, folder_name);
                        if (Directory.Exists(alt_path))
                        {
                            HasValidPath = true;
                            _s.SavePath(_vm.UID, alt_path, true);
                            return alt_path;
                        }
                    }

                    HasValidPath = false;
                    return "";
                }

                HasValidPath = true;
                return rp;
            }
            set
            {
                var path = System.IO.Path.GetFullPath(value);
                bool is_linked = !FolderHelper.IsDirectChild(_s.CFGdir, path);

                _s.SavePath(_vm.UID, value, is_linked);
            }
        }

        /// <summary>
        /// The Path as it exists in the DataBase
        /// </summary>
        public string RawPath
        {
            get => _s.IdToPath(_vm.UID);
        }

        public long? ExeID
        {
            get => _s.IdToExeId(_vm.UID);
        }

        public string Name { get => _vm.Name; set => this.RaiseAndSetIfChanged(ref _vm.Name, value); }
        /// <summary>
        /// What sort of machine to categorize this as
        /// </summary>
        public string Category { get => _vm.Category; set => this.RaiseAndSetIfChanged(ref _vm.Category, value); }

        /// <summary>
        /// Path to icon used to represent the VM
        /// </summary>
        public string IconPath { get => _vm.IconPath; set => this.RaiseAndSetIfChanged(ref _vm.IconPath, value); }

        /// <summary>
        /// For cases where the VM is both paused and waiting
        /// </summary>
        public bool IsPaused { get; private set; }

        public MachineStatus Status
        {
            get => _status;
            set
            {
                if (value == MachineStatus.PAUSED)
                    IsPaused = true;
                else if (value != MachineStatus.WAITING)
                    IsPaused = false;

                if (_status != value)
                {
                    _status = value;
                    this.RaisePropertyChanged(nameof(Status));
                }
            }
        }

        /// <summary>
        /// Returns a lovely status string for use in UI
        /// </summary>
        public string StatusText
        {
            get
            {
                switch (_status)
                {
                    case MachineStatus.STOPPED: return "Stopped";
                    case MachineStatus.RUNNING: return "Running";
                    case MachineStatus.PAUSED: return "Paused";
                    case MachineStatus.WAITING: return "Waiting";
                    default: return "Invalid status";
                }
            }
        }
        public bool IsRunning => Status != MachineStatus.STOPPED;

        public bool IsConfig { get; set; }

        public string VMIcon
        {
            get
            {
                return IconPath ?? AppSettings.DefaultIcon;
            }
        }

        public string Desc
        {
            get => _s.FetchSetting(_vm.UID, "description", "");
            set
            {
                var old = Desc;
                var val = value ?? "";
                if (!string.Equals(old, val))
                {
                    _s.SetSetting(_vm.UID, "description", val);
                    this.RaisePropertyChanged();
                }
            }
        }

        public string Comment
        {
            get => _s.FetchSetting(_vm.UID, "comment", "");
            set
            {
                var old = Comment;
                if (!string.Equals(old, value))
                {
                    _s.SetSetting(_vm.UID, "comment", value);
                    this.RaisePropertyChanged();
                }
            }
        }

        private string _vm_size = null;
        public string VMSize { get => _vm_size; set => this.RaiseAndSetIfChanged(ref _vm_size, value); }

        private bool _has_print = false;
        public bool HasPrintTray { get => _has_print; set => this.RaiseAndSetIfChanged(ref _has_print, value); }

        private string _str_since_created, _str_since_run;
        private TimeDifferenceResult _uptime = new TimeDifferenceResult("None", "", "", TimeSpan.Zero);
        public string SinceCreated { get => _str_since_created; set => this.RaiseAndSetIfChanged(ref _str_since_created, value); }
        public string SinceRun { get => _str_since_run; set => this.RaiseAndSetIfChanged(ref _str_since_run, value); }
        public TimeDifferenceResult Uptime 
        { 
            get => _uptime;
            set
            {
                if (value != null && _uptime != null && value.TimeDifference < _uptime.TimeDifference)
                {
                    //We ignore the update
                    return;
                }

                this.RaiseAndSetIfChanged(ref _uptime, value);
            }
        }

        private string _sel_image;
        private Bitmap _cached_image;
        public Bitmap SelectedImage
        {
            get
            {
                if (_cached_image == null && _sel_image != null)
                {
                    try { _cached_image = new Bitmap(_sel_image); }
                    catch { }
                }
                return _cached_image;
            }
        }

        public int SelectedImageIndex
        {
            get
            {
                if (_images == null || _images.Count == 0)
                    return -1;

                var idx_str = _s.FetchSetting(Tag.UID, "selected_image_idx", "-1");
                if (int.TryParse(idx_str, out int idx) && idx >= 0)
                {
                    if (idx >= Images.Count)
                        return Images.Count - 1;

                    return idx;
                }

                return -1;
            }
            set
            {
                int old_value;
                if (!int.TryParse(_s.FetchSetting(Tag.UID, "selected_image_idx", "-1"), out old_value))
                    old_value = -1;
                
                int idx;

                if (_images == null || _images.Count == 0)
                    idx = -1;
                else if (value < 0)
                    idx = _images.Count - 1;
                else if (value >= _images.Count)
                    idx = 0;
                else
                    idx = value;

                if (old_value != idx)
                {
                    this.RaisePropertyChanged();
                    _cached_image = null;
                    if (idx >= 0)
                        _sel_image = _images[idx];
                    else
                        _sel_image = null;
                    this.RaisePropertyChanged(nameof(SelectedImage));

                    _s.SetSetting(this.Tag.UID, "selected_image_idx", idx.ToString());
                }
            }
        }

        public bool HasMultipleImages
        {
            get => _images != null && _images.Count > 1;
        }

        private List<string> _images;
        public List<string> Images
        {
            get => _images;
            set
            {
                var old_count = HasMultipleImages;
                _images = value;
                int sii;
                if (!int.TryParse(_s.FetchSetting(Tag.UID, "selected_image_idx", "-1"), out sii))
                    sii = -1;

                if (value != null && value.Count > 0)
                {
                    if (sii < 0 || sii >= _images.Count)
                        SelectedImageIndex = 0;
                    else
                    {
                        var old_image = _sel_image;
                        _sel_image = _images[sii];

                        if (_sel_image != old_image)
                            this.RaisePropertyChanged(nameof(SelectedImage));
                    }
                }
                else
                {
                    if (_sel_image != null)
                    {
                        _sel_image = null;
                        _cached_image = null;
                        this.RaisePropertyChanged(nameof(SelectedImage));
                    }

                    if (sii != -1)
                        _s.RemoveSetting(Tag.UID, "selected_image_idx");
                }

                if (old_count != HasMultipleImages)
                    this.RaisePropertyChanged(nameof(HasMultipleImages));
            }
        }

        public VMVisual(AppSettings s, long id, string name, string cat, string icon) 
        {
            if (s == null) throw new ArgumentNullException();
            _vm = new VM(id);

            _vm.Name = name;
            _vm.Category = cat;
            _vm.IconPath = icon;
            _s = s;
        }

        /// <summary>
        /// https://github.com/AvaloniaUI/Avalonia/issues/17029
        /// </summary>
        internal VMVisual()
        {
            _vm = new VM(-1);
        }

        public void Unselected()
        {
            _cached_image = null;
        }

        /// <remarks>
        /// Todo: this function should be replaced by value converters
        /// @see StatusToColorConverter
        /// </remarks>
        public void RefreshStatus()
        {
            this.RaisePropertyChanged(nameof(StatusText));
            this.RaisePropertyChanged(nameof(Tag));
            this.RaisePropertyChanged(nameof(IsRunning));

            if (Status == MachineStatus.WAITING)
                _has_waited = true;
        }

        public bool ClearWaiting()
        {
            var r = _has_waited;
            _has_waited = false;
            return r;
        }

        public void RefreshNameAndIcon()
        {
            this.RaisePropertyChanged(nameof(Name));
            this.RaisePropertyChanged(nameof(VMIcon));
        }

        public void SetDates(DateTime created, DateTime? last_run, TimeSpan? uptime)
        {
            TM.Created = created;
            TM._last_run = last_run;
            TM.Uptime = uptime;
        }

        public void CalcTime()
        {
            _created = TM.Created.HasValue ? TM.Created.Value : DateTime.MaxValue;
            _last_run = TM.LastRun.HasValue ? TM.LastRun.Value : DateTime.MaxValue;
            _duration = TM.Uptime.HasValue ? TM.Uptime.Value : TimeSpan.Zero;

            UpdateClocks(DateTime.Now);
        }

        public void SetLastRun(DateTime lastRun)
        {
            var now = DateTime.Now;
            TM.LastRun = lastRun;
            _last_run = lastRun;
            _since_run = DateTime.Now - _last_run;
            SinceRun = TimeDifferenceFormatter.FormatTimeDifference(_since_run, "Just now", "ago").Full;

            if (IsRunning)
            {
                _total_duration = TimeSpan.MinValue / 10;
                UpdateClocks(now);
            }

            _s.UpdateStartTime(Tag.UID, _last_run, RunCount);
        }

        /// <summary>
        /// Update the "time since" calculations
        /// </summary>
        public void UpdateClocks(DateTime now)
        {
            {
                var since_created = now - _created;
                var time_since_ui_update = since_created - _since_created;
                if (DoTimeUpdate(since_created, time_since_ui_update))
                {
                    _since_created = since_created;
                    SinceCreated = TimeDifferenceFormatter.ShortFormatTimeDifference(since_created, "Just created");
                }
            }
            var since_run = now - _last_run;
            {
                since_run = now - _last_run;
                var time_since_ui_update = since_run - _since_run;
                if (DoTimeUpdate(since_run, time_since_ui_update))
                {
                    _since_run = since_run;
                    SinceRun = TimeDifferenceFormatter.FormatTimeDifference(since_run, "Just now", "ago").Full;
                }
            }
            if (IsRunning)
            {
                var total_duration = _duration + since_run;
                var time_since_ui_update = total_duration - _total_duration;
                if (DoTimeUpdate(total_duration, time_since_ui_update))
                {
                    _total_duration = total_duration;

                    //This means the VM is "running", but is only showing the settings screen. In this case we skip updating
                    //the uptime clock.
                    if (TM.WillCommitUptime)
                        Uptime = TimeDifferenceFormatter.FormatTimeDifference(total_duration, "Just started", "");
                }
            }
            else
            {
                if (_total_duration < _duration)
                {
                    Uptime = TimeDifferenceFormatter.FormatTimeDifference(_duration, "Just started", "");
                    _total_duration = _duration;
                }
            }
        }

        private bool DoTimeUpdate(TimeSpan since, TimeSpan time_since_ui_update)
        {
            if (since < _days)
            {
                //We update every minutte
                return time_since_ui_update > _minutte;
            }

            //We update every hour
            return time_since_ui_update > _hour;
        }

        private struct TimeFields
        {
            /// <summary>
            /// Time this VM was created
            /// </summary>
            public DateTime? Created { get; set; }

            internal DateTime? _last_run;
            internal bool _commit;

            /// <summary>
            /// When this VM was last run
            /// </summary>
            public DateTime? LastRun
            {
                get => _last_run;
                set
                {
                    _last_run = value;

                    _commit = value != null;
                }
            }

            /// <summary>
            /// How long this VM has been running in total
            /// </summary>
            public TimeSpan? Uptime { get; set; }

            public bool WillCommitUptime => _commit;
        }

        public void CommitUptime(DateTime now)
        {
            if (TM._commit && TM._last_run != null)
            {
                TM._commit = false;
                if (!TM.Uptime.HasValue)
                    TM.Uptime = TimeSpan.Zero;
                TM.Uptime += (now - _last_run);

                _s.UpdateUptime(Tag.UID, TM.Uptime.Value);
            }
        }

        /// <summary>
        /// Prevents updtime from being commited.
        /// </summary>
        public void CancelUptime()
        {
            TM._commit = false;
        }
    }
}
