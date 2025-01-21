using _86BoxManager.Tools;
using _86BoxManager.ViewModels;
using _86BoxManager.Xplat;
using Avalonia.Platform;
using Avalonia.Styling;
using DynamicData;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using static _86BoxManager.Core.DBStore;

namespace _86BoxManager.Core
{
    internal class AppSettings
    {
        public static readonly AppSettings Settings = new AppSettings(new DBStore());

        private DBStore _store;

        private SourceCache<VMVisual, long> _machines = new(x => x.Tag.UID);
        private SourceCache<VMCategory, string> _categories = new(x => x.Name);

        internal SourceCache<VMVisual, long> Machines => _machines;
        internal SourceCache<VMCategory, string> Categories => _categories;

        internal VMCategory DefaultCat { get; } = new VMCategory("All machines");

        internal readonly static string DefaultIcon = "/Assets/Computers/ibm_at.png";

        public PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Path to 86box.exe and the romset
        /// </summary>
        public string EXEdir
        {
            get => FetchProperty("exe_dir", "");
            set => SetProperty("exe_dir", value);
        }

        /// <summary>
        /// Path to the virtual machines folder (configs, nvrs, etc.)
        /// </summary>
        public string CFGdir
        {
            get => FetchProperty("cfg_dir", "");
            set => SetProperty("cfg_dir", value);
        }

        /// <summary>
        /// Path to roms for 86Box
        /// </summary>
        public string ROMdir
        {
            get => FetchProperty("rom_dir", "");
            set => SetProperty("rom_dir", value);
        }

        /// <summary>
        /// Whenever to use a sys tray or not
        /// </summary>
        public bool IsTrayEnabled
        {
            get => FetchProperty("tray_enabled", false);
            set => SetProperty("tray_enabled", value);
        }

        /// <summary>
        /// Whenever to close the frontent to the sys tray
        /// </summary>
        public bool CloseTray
        {
            get => FetchProperty("close_tray", false);
            set => SetProperty("close_tray", value);
        }

        /// <summary>
        /// Minimize the main window when a VM is started
        /// </summary>
        public bool MinimizeOnVMStart
        {
            get => FetchProperty("min_vm_start", false);
            set => SetProperty("min_vm_start", value);
        }

        /// <summary>
        /// Show the console window when a VM is started
        /// </summary>
        public bool ShowConsole
        {
            get => FetchProperty("show_con", true);
            set => SetProperty("show_con", value);
        }

        /// <summary>
        /// Show the console window when a VM is started
        /// </summary>
        public bool AllowInstances
        {
            get => FetchProperty("allow_instances", false);
            set => SetProperty("allow_instances", value);
        }

        /// <summary>
        /// Minimize the Manager window to tray icon
        /// </summary>
        public bool MinimizeToTray
        {
            get => FetchProperty("min_to_tray", false);
            set => SetProperty("min_to_tray", value);
        }

        /// <summary>
        /// Logging enabled for 86Box.exe (-L parameter)
        /// </summary>
        public bool EnableLogging
        {
            get => FetchProperty("86logg", false);
            set => SetProperty("86logg", value);
        }

        /// <summary>
        /// Rename folders when changing the name of a machine
        /// </summary>
        public bool RenameFolders
        {
            get => FetchProperty("fld_rename", true);
            set => SetProperty("fld_rename", value);
        }

        /// <summary>
        /// Whenever the list should show compact or not
        /// </summary>
        public bool CompactMachineList
        {
            get => FetchProperty("compact_list", false);
            set => SetProperty("compact_list", value);
        }

        public string PreferedCPUArch
        {
            get => FetchProperty("pref_cpu_arch", (string) null);
            set => SetProperty("pref_cpu_arch", value);
        }

        public string PreferedOS
        {
            get => FetchProperty("pref_os", (string)null);
            set => SetProperty("pref_os", value);
        }

        /// <summary>
        /// Prefer New Dynamic Recompiler
        /// </summary>
        public bool PrefNDR
        {
            get => FetchProperty("pref_ndr", false);
            set => SetProperty("pref_ndr", value);
        }

        public bool UpdateROMs
        {
            get => FetchProperty("upt_roms", true);
            set => SetProperty("upt_roms", value);
        }

        public bool PreserveROMs
        {
            get => FetchProperty("save_roms", false);
            set => SetProperty("save_roms", value);
        }

        public string ArchivePath
        {
            get => FetchProperty("archive_path", (string)null);
            set => SetProperty("archive_path", value);
        }

        public ThemeVariant Theme
        {
            get
            {
                switch(FetchProperty("app_theme", "Default"))
                {
                    case "Light":
                        return ThemeVariant.Light;

                    case "Dark":
                        return ThemeVariant.Dark;

                    default:
                        return ThemeVariant.Default;
                }
            }
            set
            {
                if (ReferenceEquals(value, ThemeVariant.Light))
                    SetProperty("app_theme", "Light");
                else if (ReferenceEquals(value, ThemeVariant.Dark))
                    SetProperty("app_theme", "Dark");
                else
                    SetProperty("app_theme", "Default");
            }
        }

        /// <summary>
        /// Path to log file
        /// </summary>
        public string LogPath
        {
            get => FetchProperty("log_dir", "");
            set => SetProperty("log_dir", value);
        }

        public double? ListWidth
        {
            get
            {
                Double? def = null;
                return FetchProperty("machine_width", def);
            }
            set => SetProperty("machine_width", value);
        }

        public double? InfoWidth
        {
            get
            {
                Double? def = null;
                return FetchProperty("stats_width", def);
            }
            set => SetProperty("stats_width", value);
        }

        public void RefreshCats()
        {
            var to_remove = new HashSet<string>(_categories.Count);
            foreach (var val in _categories.Keys)
                to_remove.Add(val);
            to_remove.Remove(DefaultCat.Name);

            int count = 1;
            bool notify = false;

            foreach (var vm in _store.Query("select DISTINCT(category) from VMs where category is not null order by category"))
            {
                var cat_name = (string)vm["category"];

                var v = CreateCategory(cat_name);
                if (v.OrderIndex != count)
                {
                    v.OrderIndex = count;
                    notify = true;
                }
                
                count++;

                to_remove.Remove(cat_name);
            }

            if (to_remove.Count > 0)
                _categories.RemoveKeys(to_remove);
            else if(notify)
                _categories.Refresh();
        }

        public void RefreshVMs()
        {
            var to_remove = new HashSet<long>(_machines.Count);
            foreach (var val in _machines.Keys)
                to_remove.Add(val);

            int count = 0;
            bool notify = false;

            foreach (var vm in _store.Query("select id, name, category, iconpath from VMs order by name"))
            {
                var uid = (long)vm["id"];

                //This function should perhaps be changed to add all machines using a enumeration, to reduce
                //the amount of changes notified to the listener, but in normal circumstances, there are nothing
                //added to the cache here, unless the app is starting, and in that circumstance there's no listener.
                var v = CreateVisual(uid, (string)vm["name"], vm["category"] as string, vm["iconpath"] as string);
                if (v.OrderIndex != count)
                {
                    notify = true;
                    v.OrderIndex = count;
                }

                count++;

                to_remove.Remove(uid);
            }

            if (to_remove.Count > 0)
                _machines.RemoveKeys(to_remove);
            else if (notify)
                _machines.Refresh();
        }

        private AppSettings(DBStore store) 
        { 
            _store = store;
        }

        public static List<string> GetIconAssets()
        {
            string assemblyName = Assembly.GetEntryAssembly().GetName().Name;
            var assetUri = new Uri($"avares://{assemblyName}/Assets/Computers");
            var list = new List<string>(38);

            foreach (var asset in AssetLoader.GetAssets(assetUri, null))
            {
                if (asset.LocalPath.EndsWith(".png"))
                    list.Add(asset.LocalPath);
            }

            return list;
        }

        public DBStore.Transaction BeginTransaction() => _store.BeginTransaction();

        public long RegisterVM(string name, string vm_path, string icon_path, string cat, DateTime created, long? exe_id)
        {
            if (name == null || vm_path == null)
                throw new ArgumentNullException();

            //Effectivly normalizes the paths
            vm_path = System.IO.Path.GetFullPath(vm_path).CheckTrail();

            var created_str = created.ToString(CultureInfo.InvariantCulture.DateTimeFormat.SortableDateTimePattern);

            if (string.IsNullOrWhiteSpace(cat))
                cat = null;

            if (string.IsNullOrWhiteSpace(icon_path) || icon_path == AppSettings.DefaultIcon)
                icon_path = null;

            bool is_linked = !FolderHelper.IsDirectChild(CFGdir, vm_path);

            var r = _store.Execute("insert into VMs (VMPath, IconPath, Name, Created, Category, Linked, ExeID) values (@p, @ip, @n, @cr, @ca, @link, @exe)",
                new SQLParam("p", vm_path), new SQLParam("n", name), new SQLParam("cr", created_str), new SQLParam("ca", cat),
                new SQLParam("link", is_linked), new SQLParam("ip", icon_path), new SQLParam("exe", exe_id));

            if (r != 1)
                throw new Exception("Unexpected result of insert query.");

            var id = PathToId(vm_path);

            if (id < 0)
                throw new Exception("Unexpected result of select query.");

            return id;
        }

        public void EditVM(long id, string name, string cat, string icon_path, long? exe_id)
        {
            if (name == null)
                throw new ArgumentNullException();

            var r = _store.Execute("UPDATE VMs SET Name = @n, Category = @c, iconpath = @icon, ExeID = @exe where id = @id",
                new SQLParam("n", name), new SQLParam("c", cat), 
                new SQLParam("icon", icon_path), new SQLParam("id", id),
                new SQLParam("exe", exe_id));

            if (r != 1)
                throw new Exception("Unexpected result of update query.");
        }

        public void UpdateStartTime(long id, DateTime last_run, int run_count)
        {
            var last_run_str = last_run.ToString(CultureInfo.InvariantCulture.DateTimeFormat.SortableDateTimePattern);

            _store.Execute("UPDATE VMs SET LastRun = @lr, RunCount = @rc where id = @id",
                new SQLParam("lr", last_run_str), new SQLParam("rc", run_count), new SQLParam("id", id));
        }

        public void UpdateUptime(long id, TimeSpan uptime)
        {
            var uptime_str = uptime.ToString("c");

            _store.Execute("UPDATE VMs SET Uptime = @ut where id = @id",
                new SQLParam("ut", uptime_str), new SQLParam("id", id));
        }

        public void RemoveVM(long id)
        {
            var p = new SQLParam("id", id);
            _store.Execute("DELETE FROM TagAttachment where vmid = @id", p);
            _store.Execute("DELETE FROM VMSettings where vmid = @id", p);
            _store.Execute("DELETE FROM VMs where id = @id", p);
        }

        public void RemoveExe(long id)
        {
            var p = new SQLParam("id", id);
            _store.Execute("UPDATE VMs SET ExeID = NULL where ExeID = @id", p);
            _store.Execute("DELETE FROM Executables where ID = @id", p);
        }

        public void AddExe(string name, string vm_exe, string vm_roms, string comment, string version, string arch, string build, bool def)
        {
            _store.Execute("INSERT INTO Executables (IsDef, Name, VMExe, VMRoms, \"Version\", Comment, Arch, Build) " +
                           " VALUES "+
                           "(@def, @name, @vmpath, @vmroms, @vers, @comment, @arch, @build)",
                           new SQLParam("name", name), new SQLParam("vmpath", vm_exe), new SQLParam("vmroms", vm_roms),
                           new SQLParam("comment", comment), new SQLParam("vers", version), new SQLParam("arch", arch),
                           new SQLParam("build", build), new SQLParam("def", def));
        }

        public void UpdateExe(long id, string name, string vm_exe, string vm_roms, string comment, string version, string arch, string build, bool def)
        {
            _store.Execute("UPDATE Executables SET IsDef = @def, Name = @name, VMExe = @vmpath, VMRoms = @vmroms, \"Version\" = @vers, Comment = @comment, Arch = @arch, Build = @build " +
                           "WHERE ID = @id",
                           new SQLParam("name", name), new SQLParam("vmpath", vm_exe), new SQLParam("vmroms", vm_roms),
                           new SQLParam("comment", comment), new SQLParam("vers", version), new SQLParam("arch", arch),
                           new SQLParam("build", build), new SQLParam("def", def), new SQLParam("id", id));
        }

        public IEnumerable<DataReader> ListExecutables()
        {
            return _store.Query("SELECT ID, IsDef, Name, VMExe, VMRoms, \"Version\", Comment, Arch, Build FROM Executables ORDER BY Name");
        }
        public IEnumerable<DataReader> GetDefExe()
        {
            return _store.Query("SELECT ID, Name, VMExe, VMRoms, \"Version\", Comment, Arch, Build FROM Executables WHERE IsDef = TRUE");
        }

        public IEnumerable<DataReader> GetExePaths(long uid)
        {
            return _store.Query("SELECT VMExe, VMRoms, Arch, Build FROM Executables WHERE ID = @id", new SQLParam("id", uid));
        }

        /// <summary>
        /// Refreshes a visual with data from the database
        /// </summary>
        /// <param name="uid">Identity of the visual</param>
        /// <returns>The visual that was refreshed</returns>
        public VMVisual RefreshVisual(long uid)
        {
            foreach(var r in _store.Query("select name, category, iconpath from VMs where id = @id",
                new SQLParam("id", uid)))
            {
                return CreateVisual(uid, r["name"] as string, r["category"] as string, r["iconpath"] as string);
            }

            return null;
        }

        /// <summary>
        /// Refreshes a visual with time data from the database
        /// </summary>
        /// <param name="uid">Identity of the visual</param>
        /// <returns>The visual that was refreshed</returns>
        public void RefreshTime(VMVisual v)
        {
            foreach (var r in _store.Query("select created, lastrun, uptime, runcount from VMs where id = @id",
                new SQLParam("id", v.Tag.UID)))
            {
                var created = DateTime.Parse((string)r["created"]);
                var lastrun_str = r["lastrun"] as string;
                DateTime? lastrun = lastrun_str != null ? DateTime.Parse(lastrun_str) : null;
                var uptime_str = r["uptime"] as string;
                TimeSpan? uptime = uptime_str != null ? TimeSpan.Parse(uptime_str) : null;
                var rc = (long) r["runcount"];

                v.SetDates(created, lastrun, uptime);
                v.RunCount = (int)rc;
            }
        }

        private VMVisual CreateVisual(long id, string name, string cat, string icon)
        {
            var has = _machines.Lookup(id);
            if (has.HasValue)
            {
                var visual = has.Value;

                //Update values
                visual.Name = name;
                visual.Category = cat;
                visual.IconPath = icon;
                visual.VMCat = CreateCategory(cat);

                return visual;
            }

            var nv = new VMVisual(this, id, name, cat, icon) { VMCat = CreateCategory(cat) };
            _machines.AddOrUpdate(nv);
            return nv;
        }

        private VMCategory CreateCategory(string name)
        {
            if (name == null)
                return DefaultCat;

            var has = _categories.Lookup(name);
            if (has.HasValue)
                return has.Value;

            var c = new VMCategory(name);
            _categories.AddOrUpdate(c);
            return c;
        }

        /// <summary>
        /// Gets the name of a VM based on its path
        /// </summary>
        /// <param name="path">Path to VM</param>
        /// <returns>Name of VM, null if there is no named VM on that path</returns>
        public string PathToName(string path)
        {
            if (path != null)
            {
                foreach (var name in _store.Query("select name from VMs where VMPath = @name COLLATE NOCASE", new SQLParam("name", path.CheckTrail())))
                    return name[0] as string;

            }
            return null;
        }

        /// <summary>
        /// Gets the id of a VM based on its path
        /// </summary>
        /// <param name="path">Path to VM</param>
        /// <returns>Name of VM, null if there is no named VM on that path</returns>
        public long PathToId(string path)
        {
            if (path != null)
            {
                foreach (var id in _store.Query("select id from VMs where VMPath = @name COLLATE NOCASE", new SQLParam("name", path.CheckTrail())))
                {
                    var n = id[0];
                    if (n is long l)
                        return l;
                    return (int)n;
                }

            }
            return -1;
        }

        /// <summary>
        /// Gets the id of a VM based on its name
        /// </summary>
        /// <param name="name">Path to VM</param>
        /// <returns>Name of VM, null if there is no named VM on that path</returns>
        public long[] NameToIds(string name)
        {
            List<long> ids = new List<long>(1);
            if (name != null)
            {
                foreach (var id in _store.Query("select id from VMs where Name = @name", new SQLParam("name", name)))
                {
                    var n = id[0];
                    if (n is long l)
                        ids.Add(l);
                }

            }
            return ids.ToArray();
        }

        /// <summary>
        /// Gets the path of a VM based on its id
        /// </summary>
        public string IdToPath(long id)
        {
            foreach (var name in _store.Query("select VMPath from VMs where id = @id", new SQLParam("id", id)))
            {
                return (name[0] as string) ?? "";
            }

            return "";
        }

        /// <summary>
        /// Gets the exe id of a VM based on its id
        /// </summary>
        /// <returns>Id of exe, null if there is no</returns>
        public long? IdToExeId(long id)
        {
            foreach (var name in _store.Query("select ExeID from VMs where id = @id", new SQLParam("id", id)))
            {
                if (name[0] is long i)
                    return i;
            }

            return null;
        }

        /// <summary>
        /// Gets the path of a VM based on its id
        /// </summary>
        /// <param name="path">Path to VM</param>
        /// <returns>Name of VM, null if there is no named VM on that path</returns>
        public bool GetIsLinked(long id)
        {
            foreach (var name in _store.Query("select linked from VMs where id = @id", new SQLParam("id", id)))
            {
                return (bool) name[0];
            }

            return false;
        }

        public void SavePath(long id, string path, bool is_linked)
        {
            _store.Execute($"UPDATE VMs SET vmpath = @v, linked = @l WHERE id = @id",
                new SQLParam("id", id), new SQLParam("v", path.CheckTrail()), new SQLParam("l", is_linked));
        }

        private string FetchProperty(string key, string def)
        {
            if (FetchProperty(key) is string b)
                return b;

            return def;
        }

        public string FetchSetting(long id, string key, string def)
        {
            if (FetchSetting(id, key) is string b)
                return b;

            return def;
        }

        private bool FetchProperty(string key, bool def)
        {
            if (FetchProperty(key) is string b)
                return b == "true";

            return def;
        }

        private double? FetchProperty(string key, double? def)
        {
            if (FetchProperty(key) is string b && double.TryParse(b, out double num))
                return num;

            return null;
        }

        private object FetchProperty(string key)
        {
            foreach (var res in _store.Query($"select value from AppSettings where field = '{key}'"))
                return res[0];

            return null;
        }

        private object FetchSetting(long id, string key)
        {
            foreach (var res in _store.Query($"select value from VMSettings where vmid = {id} and field = '{key}'"))
                return res[0];

            return null;
        }

        private void SetProperty(string key, bool value)
        {
            SetProperty(key, value ? "true" : "false");
        }

        private void SetProperty(string key, double? value)
        {
            SetProperty(key, value.HasValue ? value.Value.ToString() : null);
        }

        private void SetProperty(string key, string value)
        {
            _store.SetOrUpdate($"UPDATE AppSettings SET value = @v WHERE field = @k",
                               $"INSERT INTO AppSettings (field, value) VALUES (@k, @v)",
                               new SQLParam("k", key), new SQLParam("v", value));
        }

        public void SetSetting(long id, string key, string value)
        {
            _store.SetOrUpdate($"UPDATE VMSettings SET value = @v WHERE vmid = @id and field = @k",
                               $"INSERT INTO VMSettings (vmid, field, value) VALUES (@id, @k, @v)",
                               new SQLParam("id", id), new SQLParam("k", key), new SQLParam("v", value));
        }

        public void RemoveSetting(long id, string key)
        {
            _store.Execute("DELETE from VMSettings WHERE vmid = @id and field = @k",
                new SQLParam("id", id), new SQLParam("k", key));
        }
    }
}
