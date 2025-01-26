using Avalonia86.Xplat;
using Avalonia.Controls;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

#if MSDB
using SQLiteConnection = Microsoft.Data.Sqlite.SqliteConnection;
using SQLiteCommand = Microsoft.Data.Sqlite.SqliteCommand;
using SQLiteDataReader = Microsoft.Data.Sqlite.SqliteDataReader;
using SQLiteParameter = Microsoft.Data.Sqlite.SqliteParameter;
#else
using System.Data.SQLite;
#endif

namespace Avalonia86.Tools;

internal static class HWDB
{
    private readonly static SQLiteConnection _db;

    static HWDB()
    {
        try
        {
            var startupPath = CurrentApp.StartupPath;
            var db = Path.Combine(startupPath, "Resources", "HWDB.sqlite");
            if (File.Exists(db))
            {
#if MSDB
                _db = new SQLiteConnection("Data Source=" + db);
#else
                _db = new SQLiteConnection("URI=file:" + db);
#endif
                _db.Open();
            }
            else
            {
                throw new Exception("Failed to find HWDB.sqlite: "+ Path.Combine(startupPath, "Resources", "HWDB.sqlite"));
            }
        }
        catch (Exception)
        {

#if DEBUG
            if (Design.IsDesignMode)
            {
                //Create a dummy DB
                _db = new SQLiteConnection("Data Source=:memory:");
                _db.Open();

                InitDB(_db);
            }
            else
#endif
            {
                throw;
            }
        }

    }
#if DEBUG
    private static bool InitDB(SQLiteConnection db)
    {
        try
        {
            var commands = CreateDefaultDB().Split("--§");

            foreach (var command in commands)
            {
                using var cmd = new SQLiteCommand(command, db);
                cmd.ExecuteNonQuery();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string CreateDefaultDB(string script = "/TestHWDB.sql")
    {
        using (var strm = Tools.Resources.FindResource(script))
        {
            using (var sr = new StreamReader(strm))
                return sr.ReadToEnd();
        }
    }
#endif

    internal static bool HasDatabase => _db != null;

    internal static void CloseDatabase()
    {
        if (_db != null)
        {
            _db.Close();
            _db.Dispose();
        }
    }

    //Written by me

    //public readonly static Dictionary<string, string> FDD = new Dictionary<string, string>
    //{
    //    { "none", "None" },
    //    { "525_1dd", "5.25\" 180k" },
    //    { "525_2dd", "5.25\" 360k" },
    //    { "525_2qd", "5.25\" 720k" },
    //    { "525_2hd", "5.25\" 1.2M" },
    //    { "525_2hd_dualrpm", "5.25\" 1.2M" }, // 300/360 RPM
    //    { "35_1dd", "3.5\" 360k" },
    //    { "35_2dd", "3.5\" 720k" },
    //    { "35_2hd", "3.5\" 1.44M" },
    //    { "35_2hd_nec", "3.5\" 1.25M" }, //PC-98
    //    { "35_2hd_3mode", "3.5\" 1.44M" }, //300/360 RPM
    //    { "35_2ed", "3.5\" 2.88M" },
    //    { "525_2hd_ps2", "5.25\" 1.2M" }, //PS/2
    //    { "35_2hd_ps2", "3.5\" 1.44M" }, //PS/2
    //};

    //========================= Generated using CreateHWDB

    public readonly static DBIndex Cdrom = new DBIndex("hw_cdrom", "value");
    public readonly static DBIndex Chipset = new DBIndex("hw_chipset", "value");
    public readonly static DBIndex Device = new DBIndex("hw_device", "value");
    public readonly static DBIndex Disk = new DBIndex("hw_disk", "value");
    public readonly static DBIndex Floppy = new DBIndex("hw_floppy", "value");
    public readonly static DBIndex FDD = new DBIndex("hw_fdd", "value");
    public readonly static DBIndex Game = new DBIndex("hw_game", "value");
    public readonly static DBIndex Machine = new DBIndex("hw_machine", "value");
    public readonly static DBIndex Mem = new DBIndex("hw_mem", "value");
    public readonly static DBIndex Network = new DBIndex("hw_network", "value");
    public readonly static DBIndex Scsi = new DBIndex("hw_scsi", "value");
    public readonly static DBIndex Sio = new DBIndex("hw_sio", "value");
    public readonly static DBIndex Sound = new DBIndex("hw_sound", "value");
    public readonly static DBIndex Video = new DBIndex("hw_video", "value");
    public readonly static DBIndexVT Machines = new DBIndexVT("hw_machines", "value", "type");
    public readonly static DBIndexVT Cpus = new DBIndexVT("hw_cpus", "value", "manufacturer");

    public class DBIndex : IEnumerable<KeyValuePair<string, string>>
    {
        private readonly string _q;

        public DBIndex(string table, string field)
        {
            _q = $"select {field} from {table} where field = @f";
        }

        public string this[string key]
        {
            get
            {
                using (var cmd = new SQLiteCommand(_q, _db))
                {
                    cmd.Parameters.Add(new SQLiteParameter("@f", key));

                    using (SQLiteDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                            return r.GetString(0);
                    }
                }

                return null;
            }
        }

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            var q = _q.Split("where")[0];
            q = q.Substring(0, 7) + "field, " + q.Substring(7);

            using (var cmd = new SQLiteCommand(q, _db))
            {
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        yield return new KeyValuePair<string, string>(r.GetString(0), r.GetString(1));
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, string>>)this).GetEnumerator();
        }

        public bool TryGetValue(string key, out string value)
        {
            value = this[key];
            return value != null;
        }
    }

    public class DBIndexVT : IEnumerable<KeyValuePair<string, ValueType>>
    {
        private readonly string _q;

        public DBIndexVT(string table, string field, string field2)
        {
            _q = $"select {field}, {field2} from {table} where field = @f";
        }

        public ValueType? this[string key]
        {
            get
            {
                using (var cmd = new SQLiteCommand(_q, _db))
                {
                    cmd.Parameters.Add(new SQLiteParameter("@f", key));

                    using (SQLiteDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                            return new ValueType(r.GetString(0), r.GetString(1));
                    }
                }

                return null;
            }
        }

        IEnumerator<KeyValuePair<string, ValueType>> IEnumerable<KeyValuePair<string, ValueType>>.GetEnumerator()
        {
            var q = _q.Split("where")[0];
            q = q.Substring(0, 7) + "field, " + q.Substring(7);

            using (var cmd = new SQLiteCommand(q, _db))
            {
                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        yield return new KeyValuePair<string, ValueType>(r.GetString(0),
                            new ValueType(r.GetString(1), r.GetString(2)));
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, string>>)this).GetEnumerator();
        }

        public bool TryGetValue(string key, out ValueType value)
        {
            var v = this[key];
            value = v.Value;
            return v.HasValue;
        }
    }

    public struct ValueType
    {
        public readonly string Value;
        public readonly string Type;

        public ValueType(string value, string type)
        {
            Value = value;
            Type = type;
        }
    }
}
