using _86BoxManager.Tools;
using _86BoxManager.ViewModels;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;

namespace _86BoxManager.Core
{
    /// <summary>
    /// Gathers all information needed for a machine on a background thread
    /// </summary>
    internal class Machine : IDisposable
    {
        const string BOX_CONF = "86box.cfg";
        const string BOX_SCREENSHOTS = "screenshots";
        const string BOX_PRINTER = "printer";

        private BackgroundExecutor _worker = new();

        /// <summary>
        /// One annoying aspect of FileSystemWatcher is that it does not generate notifications
        /// if the watched folder is renamed or moved. It will give an error on deletion, though.
        /// 
        /// Anyway, the workarround is to watch the parent folder for change events, then
        /// check if the folder exists.
        /// </summary>
        private readonly FileSystemWatcher _fsw = new();
        private readonly FileSystemWatcher _pfsw = new();
        private readonly MainModel _m;

        /// <summary>
        /// We throttle the events from file changer 
        /// </summary>
        /// <remarks>
        /// Why 75ms? Well, we don't need instant UI updates every time the folder changes.
        /// 
        /// This delay is still so short it is basically instant, so an even longer
        /// delay might not be such a bad idea.
        /// </remarks>
        private readonly Timer _throttle_timer = new Timer(75) { AutoReset = false };

        /// <summary>
        /// Until the timer above elapses, all events are collected onto this queue.
        /// </summary>
        private readonly ConcurrentQueue<UpdateEvent> _event_queue = new();

        /// <summary>
        /// Used to update the time elapses values
        /// </summary>
        private readonly Timer _minutte_clock = new Timer(30 * 1000);

        /// <summary>
        /// We don't want these operations happening too often, so we throttle them
        /// further when the VM is running.
        /// </summary>
        /// <remarks>
        /// These fields can be accessed from another thread, so always lock
        /// to _throttle_timer before accessing them.
        /// </remarks>
        private System.Threading.Timer _fld_size_timer = null;
        private DateTime _last_fld_chk = DateTime.MinValue;

        /// <summary>
        /// We want to avoid excessive folder checking, so we use a timer. This delays
        /// for 1 minutte when the VM is active or not.
        /// </summary>
        private readonly TimeSpan _sizeCheckInterval_running = TimeSpan.FromMinutes(1);
        private bool _pendingSizeCheck, _pendingFolderCheck, _pendingConfigCheck;
        private string _86box_conf_path, _86box_sshots_path, _86box_prt_path;

        private int _current_job_id = 0;
        private VMVisual _current;
        private BackgroundTask _current_task;

        public Machine(MainModel m)
        {
            _m = m;

            _fsw.NotifyFilter =  NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 //| NotifyFilters.Attributes
                                 //| NotifyFilters.CreationTime
                                 ;

            _fsw.IncludeSubdirectories = true;
            _fsw.Changed += _fsw_Changed;
            _fsw.Created += _fsw_Changed;
            _fsw.Deleted += _fsw_Changed;
            _fsw.Renamed += _fsw_Renamed;
            _fsw.Error += _fsw_Error;
            _pfsw.Renamed += _pfsw_Renamed;
            _pfsw.Deleted += _pfsw_Deleted;
            _pfsw.NotifyFilter = NotifyFilters.DirectoryName;

            _throttle_timer.Elapsed += HandleChangeEvents;
            _minutte_clock.Elapsed += _minutte_clock_Elapsed;
            _minutte_clock.Start();

            //Linux does not get the waiting/paused events from 86Box.
            if (!Program.IsLinux)
            {
                _throttle_timer.Interval = 250;
            }
        }

        private void _pfsw_Deleted(object sender, FileSystemEventArgs e)
        {
            if (!Directory.Exists(_fsw.Path))
                _fsw_Error(null, null);
        }

        private void _pfsw_Renamed(object sender, RenamedEventArgs e)
        {
            if (!Directory.Exists(_fsw.Path))
                _fsw_Error(null, null);
        }

        private void _fsw_Error(object sender, ErrorEventArgs e)
        {
            _fsw.EnableRaisingEvents = false;
            _pfsw.EnableRaisingEvents = false;
            if (_current != null)
            {
                var has_path = _current.Path;
            }
        }

        private void _fsw_Renamed(object sender, RenamedEventArgs e)
        {
            _fsw_Changed(sender, new FileSystemEventArgs(e.ChangeType, Path.GetDirectoryName(e.FullPath), e.Name));
        }

        private void _fsw_Changed(object _, FileSystemEventArgs e)
        {
            //1. Check if folders exist
            // - Lightweigth operation.
            //   Do when: rename, created or deleted
            //2. Check 86Box.cfg
            //   When: changed, rename or created, check if it's 86Box.cfg. If so, parse it.
            //   When deleted, don't parse but create an empty settings dictionary
            //   Otherwise ignore
            //3. Check size of folder
            //   When: Any changes besides rename
            //   If it's not running, calc
            //   If machine is running, only calc once every 10 miniutes, so that we don't catch every HD write.

            //Ignore events when... doing a job and the three types match. 
            // - Oftentimes multiple events are fired in short succesion. Handeling each might bog down the app.

            //I'm thinking, three boolean variables "folder check", "config check" and "size check".
            //When an event comes in, figure out which one of these are needed. If a job is running that is
            //handeling the same bools, then ignore until the job is done. If they differ, schedule a new job.
            //
            //Note, the background worker is queue based, so it will only do one job at the time, so there will
            //never be multiple jobs running concurrently.

            //Keep in mind.
            //
            //The user might click on a different folder while a job is running. There is already logic
            //in place to discard the results, but the bools needs to be reset every time a folder is changed.
            //
            //If a machine is running, and 10 minutes haven't passed, then always skip the "size check".
            // However, once those ten minutes are up or the machine is stopped, then the ignored size check
            // must be done. So the check must be delayed, not outright ignored.
            //
            //When a machine is selected by the user, the above checks are always done. While this happens,
            //the change events are ignored. _fsw.EnableRaisingEvents = false;

            //If we don't have current, then there's nothing to do
            if (_current == null)
                return;

            bool parse_config = false;
            bool check_folders = false;
            bool check_size = false;

            if (string.Equals(e.FullPath, _86box_conf_path, StringComparison.InvariantCultureIgnoreCase))
            {
                //86Box makes many changes to 86Box.conf, so we take care to only parse it
                //after a running machine has "waited" or when the machine isn't running
                //
                //The "_current.Tag.Status == VM.STATUS_RUNNING" is here because 86Box will
                //modify 86Box.conf while paused, in some way, which does not include the changes,
                //se we wait until the emu is actually running.
                //
                //This is quite dependent on quirks of 86Box, making it fragile. A more robust
                //solution is probably to set the throttle_timer to a much higher value.
                parse_config = (!_current.IsRunning || _current.Status == MachineStatus.RUNNING && _current.ClearWaiting()) 
                    || Program.IsLinux || _current.IsConfig;
                check_size = parse_config;
            }
            else if (string.Equals(e.FullPath, _86box_prt_path, StringComparison.InvariantCultureIgnoreCase) ||
                e.FullPath.StartsWith(_86box_sshots_path, StringComparison.InvariantCultureIgnoreCase))
            {
                //A change has happened to the screenshots or printer folder.
                // Using "startswith" for screenshots, as there is a difference between debug and release builds.
                // On both Windows and Linux, the release build only sends "Created" for the second file created
                // in the screenshot folder. Fortunatly, startswith catches them and since events are throlled, it
                // dosn't matter that we catch more than one such event. 
                check_folders = true;
                check_size = true;
            }
            else if (e.ChangeType == WatcherChangeTypes.Renamed)
                //Something got renamed. Perhaps the "screenshot" folder got renamed, for instance.
                // (Deletion/Creation is handled above)
                check_folders = true;
            else
            {
                check_size = true;
            }

            //Debug.WriteLine(" -0-");
            //Debug.WriteLine($"Path: {e.FullPath}, Change: {e.ChangeType}");
            //Debug.WriteLine($"Config: {parse_config}, Folders: {check_folders}, Size: {check_size}, Type: {e.ChangeType}");

            _event_queue.Enqueue(new UpdateEvent(parse_config, check_folders, check_size));
            if (_throttle_timer.Enabled)
                return;

            if (!_throttle_timer.Enabled)
                _throttle_timer.Start();
        }

        private class UpdateEvent
        {
            public readonly bool Config;
            public readonly bool Folders;
            public readonly bool Size;

            public UpdateEvent(bool c, bool f, bool s)
            {
                Config = c;
                Folders = f;
                Size = s;
            }
        }

        private void _minutte_clock_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_current != null)
                {
                    _current.UpdateClocks(DateTime.Now);
                }
            });
        }

        private void HandleChangeEvents(object _, ElapsedEventArgs evt)
        {
            Dispatcher.UIThread.Post(() =>
            {
                //On MT: This function will never execute after a machine has been
                //switched, as the method that does the switch is on the UI thread
                //and disables the timer (which is also the UI thread)

                bool parse_config = false;
                bool check_folders = false;
                bool check_size = false;

                while (!_event_queue.IsEmpty)
                {
                    if (_event_queue.TryDequeue(out UpdateEvent e))
                    {
                        parse_config |= e.Config;
                        check_size |= e.Size;
                        check_folders |= e.Folders;
                    }
                }

                //If is working, check if the bool types above match, if so ignore this event.
                if (_pendingConfigCheck) parse_config = false;
                if (_pendingSizeCheck) check_size = false;
                if (_pendingFolderCheck) check_folders = false;

                if (!parse_config && !check_size && !check_folders)
                    return;

                var mi = new MachineInfo(_current, _current_job_id);
                var work = new BackgroundTask(
                    () =>
                    {
                        if (parse_config)
                            mi.Config = ReadConfig(mi.ConfigFile);
                        if (check_size)
                        {
                            var size = TryCheckFldSize(mi);
                            if (size != null)
                                mi.VMSize = size;
                            else
                            {
                                //This will now be handled later
                                check_size = false;
                            }
                        }

                        if (check_folders)
                            CheckFolders(mi);

                        return mi;
                    },
                    o =>
                    {
                        if (mi.JobID == _current_job_id)
                        {
                            if (parse_config)
                            {
                                _pendingConfigCheck = false;
                                mi.VM.VMConfig = mi.Config;
                                _m.RaisePropertyChanged(nameof(VMConfig));
                            }

                            if (check_size)
                            {
                                _pendingSizeCheck = false;
                                mi.VM.VMSize = mi.VMSize;
                            }

                            if (check_folders)
                            {
                                _pendingFolderCheck = false;
                                mi.VM.HasPrintTray = mi.HasPrinterFolder;
                                mi.VM.Images = mi.Images;
                            }
                        }
                    }
                );

                _pendingSizeCheck |= check_size;
                _pendingFolderCheck |= check_folders;
                _pendingConfigCheck |= parse_config;

                if (_worker != null)
                    _worker.Post(work);
            });
        }

        private string TryCheckFldSize(MachineInfo mi)
        {
            DateTime last;
            lock (_throttle_timer)
            {
                if (_fld_size_timer != null)
                    return null;
                last = _last_fld_chk;
                _last_fld_chk = DateTime.Now;
            }

            var time_since_last_chk = DateTime.Now - last;
            if (time_since_last_chk > _sizeCheckInterval_running)
            {
                return FolderSizeCalculator.GetFolderSizeAsStr(mi.VMPath);
            }
            else
            {
                // Query the value on the UI thread
                bool is_vm_running = Dispatcher.UIThread.Invoke(() =>
                {
                    return mi.VM.Status != MachineStatus.STOPPED;
                });

                if (is_vm_running)
                {
                    var callback = new System.Threading.TimerCallback(_fld_size_timer_Elapsed);

                    lock (_throttle_timer)
                    {
                        _fld_size_timer = new System.Threading.Timer(callback, mi, _sizeCheckInterval_running - time_since_last_chk, System.Threading.Timeout.InfiniteTimeSpan);
                    }
                }
                else
                {
                    return FolderSizeCalculator.GetFolderSizeAsStr(mi.VMPath);
                }
            }

            return null;
        }

        private void _fld_size_timer_Elapsed(object state)
        {
            var mi = (MachineInfo)state;

            if (_current_job_id == mi.JobID)
            {
                //Note, we are in a background thread that's entierly independent of anything.
                // We assume that the MI object will not be modified in a conflicting way by the owner thread,
                // so that we don't need to lock it.
                mi.VMSize = FolderSizeCalculator.GetFolderSizeAsStr(mi.VMPath);

                //We wait with this until we're done calculating everything, as this opens up
                //the possibility of a new timer being created.
                lock (_throttle_timer)
                {
                    if (_current_job_id == mi.JobID && _fld_size_timer != null)
                    {
                        _fld_size_timer.Dispose();
                        _last_fld_chk = DateTime.Now;
                    }
                    _fld_size_timer = null;
                }

                //Now we update the VMRow on the UI thread.
                Dispatcher.UIThread.Post(() =>
                {
                    if (_current_job_id == mi.JobID)
                    {
                        _pendingSizeCheck = false;
                        mi.VM.VMSize = mi.VMSize;
                    }
                });
            }
        }

        /// <summary>
        /// This method is for updating what VM/folder to view
        /// </summary>
        /// <param name="m">New VM</param>
        public void Update(VMVisual m)
        {
            //Ignore if it's already being viewed.
            if (ReferenceEquals(_current, m)) return;

            if (_current != null)
                _current.PropertyChanged -= _current_PropertyChanged;
            _fsw.EnableRaisingEvents = false;
            _pfsw.EnableRaisingEvents = false;
            _current = m;

            if (_current_task != null)
                _current_task.Canceled = true;

            var mi = new MachineInfo(m, ++_current_job_id);

            var work = _current_task = new BackgroundTask(() => { Update(mi); return mi; }, UiUpdate);

            //Ensure all flags are reset
            _pendingSizeCheck = false;
            _pendingFolderCheck = false;
            _pendingConfigCheck = false;
            _86box_conf_path = Path.GetFullPath(mi.ConfigFile);
            _86box_sshots_path = Path.GetFullPath(mi.SShotFld);
            _86box_prt_path = Path.GetFullPath(mi.PrtFld);

            _event_queue.Clear();

            _throttle_timer.Enabled = false;
            lock (_throttle_timer)
            {
                if (_fld_size_timer != null)
                {
                    _fld_size_timer.Dispose();
                    _fld_size_timer = null;
                }
                _last_fld_chk = DateTime.MinValue;
            }

            _current.CalcTime();

            if (!string.IsNullOrEmpty(mi.VMPath) && _worker != null)
                _worker.Post(work);
        }

        internal void ClearCurrent()
        {
            _fsw.EnableRaisingEvents = false;
            _pfsw.EnableRaisingEvents = false;
            if (_current != null)
                _current.PropertyChanged -= _current_PropertyChanged;
            _current = null;
        }

        private void _current_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VMVisual.StatusText))
            {
                //Note that when the VMRow ovject is "edited", it's actually recreated. Because of this we don't
                //need to worry about Path changes and such. 

                if (_pendingSizeCheck == true && _current.Status != MachineStatus.RUNNING)
                {
                    //Do size check right away
                    lock (_throttle_timer)
                    {
                        if (_fld_size_timer != null)
                        {
                            _fld_size_timer.Dispose();
                            _fld_size_timer = null;
                            _pendingSizeCheck = false;

                            _fsw_Changed(sender, new FileSystemEventArgs(WatcherChangeTypes.Changed, _current.Path, ""));
                        }
                    }
                }
            }
        }

        private void Update(MachineInfo mi)
        {
            mi.Config = ReadConfig(mi.ConfigFile);
            mi.VMSize = FolderSizeCalculator.GetFolderSizeAsStr(mi.VMPath);
            CheckFolders(mi);

            lock (_throttle_timer) { _last_fld_chk = DateTime.Now; }
        }

        private void CheckFolders(MachineInfo mi)
        {
            mi.HasPrinterFolder = Directory.Exists(Path.Combine(mi.VMPath, "printer"));
            var images = Path.Combine(mi.VMPath, "screenshots");

            if (Directory.Exists(images))
            {
                List<string> imageFileNames = new List<string>();
                string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };

                foreach (var file in Directory.GetFiles(images))
                {
                    foreach (var extension in allowedExtensions)
                    {
                        if (file.EndsWith(extension, true, null))
                        {
                            imageFileNames.Add(file.Replace('\\', '/'));
                            break;
                        }
                    }
                }

                if (imageFileNames.Count > 0)
                    mi.Images = imageFileNames;
            }
        }

        private RawConfig ReadConfig(string path)
        {
            RawConfig config = new RawConfig();

            try
            {
                using (var f = File.OpenRead(path))
                using (var sr = new StreamReader(f, Encoding.UTF8, true, 4096))
                {
                    //Sanity check. If the file is this big, it's probably not a config file
                    if (f.Length > 16384)
                        throw new IOException("File too big");

                    //This is an INI like format with section headers like this: [Header name]
                    //Then the section is followed by a list of key = value pairs.
                    Dictionary<string, string> currentSection = null;
                    string line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim();

                        // Ignore empty lines
                        if (string.IsNullOrEmpty(line))
                            continue;

                        // Check for section header
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            string sectionName = line.Substring(1, line.Length - 2);
                            currentSection = new Dictionary<string, string>();
                            config[sectionName] = currentSection;
                        }
                        else if (currentSection != null)
                        {
                            // Read key = value pairs
                            var keyValue = line.Split(['='], 2);
                            if (keyValue.Length == 2)
                            {
                                string key = keyValue[0].Trim();
                                string value = keyValue[1].Trim();
                                currentSection[key] = value;
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
                //If the config file can not be read, then we simply use the default values.
            }
            return config;
        }

        private void UiUpdate(object o)
        {
            var mi = (MachineInfo)o;

            //Quit if the current VMRow is different from the one we're updating
            if (_current_job_id != mi.JobID)
                return;

            //We're now on the UI thread, which means the VMRow object can be touched.
            mi.VM.VMConfig = mi.Config;
            mi.VM.VMSize = mi.VMSize;
            mi.VM.HasPrintTray = mi.HasPrinterFolder;
            mi.VM.Images = mi.Images;

            //We hook up the propery event of VMRow so that we can react to changes
            _current.PropertyChanged += _current_PropertyChanged;

            //Enable watching the folder for changes
            try
            {
                _fsw.Path = mi.VMPath;
                _fsw.EnableRaisingEvents = true;
                var p_path = FolderHelper.GetParentFolderPath(mi.VMPath);
                if (p_path != null)
                {
                    _pfsw.Path = p_path;
                    _pfsw.EnableRaisingEvents = true;
                }
            }
            catch { /* Can happen when "VMPath" does not exist */ }

            _m.RaisePropertyChanged(nameof(VMConfig));
        }

        public void Dispose()
        {
            _fsw.Dispose();
            _worker.Stop();

            //Set worker to null to signal already posted work to stop. This works
            //because those jobs always executes on the UI thread.
            _worker = null;
            
            lock (_throttle_timer)
            {
                if (_fld_size_timer != null)
                    _fld_size_timer.Dispose();
                _fld_size_timer = null;
            }
            _throttle_timer.Dispose();
        }

        private class MachineInfo
        {
            private readonly string _path;

            public RawConfig Config { get; set; }
            public string VMSize { get; set; }

            public bool HasPrinterFolder { get; set; }

            /// <summary>
            /// Object that is owned by the UI thread. Do not touch.
            /// </summary>
            public VMVisual VM { get; private set; }

            /// <summary>
            /// List over images
            /// </summary>
            public List<string> Images;

            public int JobID { get; private set; }

            public string ConfigFile => Path.Combine(_path, BOX_CONF);
            public string SShotFld => Path.Combine(_path, BOX_SCREENSHOTS);
            public string PrtFld => Path.Combine(_path, BOX_PRINTER);
            public string VMPath => _path;

            public MachineInfo(VMVisual row, int job_id)
            {
                //We grab the path, as the "VMrow" object can be accessed by the UI thread.
                // We are currently in the UI thread.
                _path = row.Path;
                VM = row;
                JobID = job_id;
            }
        }
    }

    public class RawConfig : Dictionary<string, Dictionary<string, string>>
    {

    }
}