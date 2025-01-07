using _86BoxManager.Xplat;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Reflection;

namespace _86BoxManager.Core
{
    /// <summary>
    /// We want to avoid making the app too dependent on System.Data.SQLite, so this serves as a wrapper around the DB
    /// </summary>
    internal sealed class DBStore
    {
        #region Static fields

        private static readonly SQLiteConnection _db;
        private static bool _in_memeory_db;
        const string SettingsFolder = "Avalonia86";
        const string AppName = SettingsFolder;

        public static bool HasDatabase => _db != null;
        public static bool InMemDB => _in_memeory_db;

        static DBStore()
        {
            try 
            { 
                _db = OpenDB();
                using (var cmd = _db.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA foreign_keys = ON";
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Environment.FailFast("Failed to create settings database: "+ex.Message);
            }
        }

        internal static void CloseDatabase()
        {
            if (_db != null)
            {
                _db.Close();
                _db.Dispose();
            }
        }
        private static SQLiteConnection OpenDB()
        {
            string db_name = AppName + ".sqlite";

            //First, we look for a database in the local folder or appdata folder.
            string local_path = Path.Combine(CurrentApp.StartupPath, db_name);
            string app_folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), SettingsFolder);
            string app_path = Path.Combine(app_folder, db_name);
            SQLiteConnection con;

            if (!Design.IsDesignMode)
            {
                if (TryOpenDB(local_path, out con) || TryOpenDB(app_path, out con))
                    return con;

                //Next, we try to create a new settings database.
                if (TryCreateDB(local_path, null, out con) || TryCreateDB(app_path, app_folder, out con))
                    return con;
            }

            //Open DB in memory
            //Message.Snack("Unable to open settings, sorry. Settings will note be saved.");
            con = new SQLiteConnection("Data Source=:memory:");
            con.Open();

            if (con.IsReadOnly(null) || !InitDB(con))
                throw new Exception();
            _in_memeory_db = true;

#if DEBUG
            if (Design.IsDesignMode)
                PopulateDB(con);
#endif

            return con;
        }

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

                //Inserts the version number
                {
                    var version = Assembly.GetExecutingAssembly().GetName().Version;
                    using var cmd = new SQLiteCommand($"INSERT INTO FileInfo(Creator, Version) VALUES('{AppName} {version.Major}.{version.Minor}', 1.0)", db);
                    cmd.ExecuteNonQuery();
                }

                return true;
            }
            catch 
            { 
                return false; 
            }
        }

#if DEBUG
        private static bool PopulateDB(SQLiteConnection db)
        {
            try
            {
                var commands = CreateDefaultDB("/TestDB.sql").Split("--§");

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
#endif

        private static bool TryCreateDB(string path, string folder, out SQLiteConnection db)
        {
            try
            {
                if (folder != null)
                    Directory.CreateDirectory(folder);

                //Creates an empty file
                var r = File.Create(path);
                r.Close();

                if (TryOpenDB(path, out db, false) && InitDB(db))
                {
                    //Message.Log("Using default settings, storing at: " + path);
                    return true;
                }
            }
            catch { }

            db = null;
            return false;
        }

        private static string CreateDefaultDB(string script = "/CreateDB.sql")
        {
            using (var strm = Tools.Resources.FindResource(script))
            {
                //byte[] temp = new byte[strm.Length];
                //strm.Read(temp, 0, temp.Length);
                using (var sr = new StreamReader(strm))
                    return sr.ReadToEnd();
            }
        }

        private static bool TryOpenDB(string path, out SQLiteConnection db, bool test_db = true)
        {
            try
            {
                if (File.Exists(path))
                {
                    //Try open the db
                    db = new SQLiteConnection("URI=file:" + path);
                    db.Open();

                    try
                    {
                        if (!db.IsReadOnly(null))
                        {
                            if (test_db)
                            {
                                var fetch = new SQLiteCommand(db)
                                {
                                    CommandText =
                                        @"select Version from FileInfo"
                                };

                                using (fetch)
                                using (var r = fetch.ExecuteReader())
                                {
                                    //Tests if the file is a Version 1.x format.
                                    if (!r.Read() || r.GetFloat(0) >= 2)
                                        return false;
                                }
                            }
                            
                            //Message.Log("Fetching settings storing at: " + path);
                            return true;
                        }
                    }
                    catch { }

                    db.Close();
                }
            }
            catch { }

            db = null;
            return false;
        }

        internal static void UpdateWindow(double top, double left, double height, double width, bool maximized)
        {
            var update = new SQLiteCommand(_db)
            {
                CommandText =
                    @"UPDATE Window SET Top = @t, ""Left"" = @l, Height = @h, Width = @w, Maximized = @m"
            };

            try
            {
                update.Parameters.AddWithValue("@t", top);
                update.Parameters.AddWithValue("@l", left);
                update.Parameters.AddWithValue("@h", height);
                update.Parameters.AddWithValue("@w", width);
                update.Parameters.AddWithValue("@m", maximized);

                if (update.ExecuteNonQuery() != 1)
                {
                    update.CommandText = @"Insert into Window (Top, ""Left"", Height, Width, Maximized) values (@t, @l, @h, @w, @m)";

                    update.ExecuteNonQuery();
                }
            }
            catch { }
            finally { update.Dispose(); }
        }

        internal static SizeWindow FetchWindowSize()
        {
            var fetch = new SQLiteCommand(_db)
            {
                CommandText =
                    @"select Top, ""Left"", Height, Width, Maximized from Window"
            };

            try
            {
                using (var r = fetch.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new SizeWindow(r.GetFloat(0), r.GetFloat(1), r.GetFloat(2), r.GetFloat(3), r.GetBoolean(4));
                    }
                }
            } 
            catch { }
            finally { fetch.Dispose(); }


            return null;
        }

        internal sealed class SizeWindow
        {
            public readonly double Top, Left, Height, Width;
            public readonly bool Maximized;

            public SizeWindow(double top, double left, double height, double width, bool maximized)
            {
                Top = top;
                Left = left;
                Height = height;
                Width = width;
                Maximized = maximized;
            }
        }

        #endregion

        /// <summary>
        /// Execute the command and return the number of rows inserted/updated affected by it.
        /// </summary>
        /// <returns>The number of rows inserted/updated affected by it.</returns>
        public int Execute(string query, params SQLParam[] parameters)
        {
            using (var cmd = new SQLiteCommand(query, _db))
            {
                foreach (var param in parameters)
                    cmd.Parameters.AddWithValue($"@{param.Name}", param.Value);

                return cmd.ExecuteNonQuery();
            }
        }

        public void SetOrUpdate(string update, string set, params SQLParam[] parameters)
        {
            using (var cmd = new SQLiteCommand(update, _db))
            {
                foreach (var param in parameters)
                    cmd.Parameters.AddWithValue($"@{param.Name}", param.Value);

                if (cmd.ExecuteNonQuery() == 0)
                {
                    cmd.CommandText = set;

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public IEnumerable<DataReader> Query(string query)
        {
            using (var cmd = new SQLiteCommand(query, _db))
            using (SQLiteDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                    yield return new DataReader(r);
            }
        }

        public IEnumerable<DataReader> Query(string query, params SQLParam[] parameters)
        {
            using (var cmd = new SQLiteCommand(query, _db))
            {
                foreach (var param in parameters)
                    cmd.Parameters.AddWithValue($"@{param.Name}", param.Value);

                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        yield return new DataReader(r);
                }
            }
        }

        public Transaction BeginTransaction()
        {
            return new Transaction(_db.BeginTransaction());
        }

        public sealed class DataReader
        {
            readonly SQLiteDataReader _r;

            public object this[int col_index]
            {
                get => _r[col_index];
            }

            public object this[string name]
            {
                get => _r[name];
            }

            public DataReader(SQLiteDataReader r)
            {
                _r = r;
            }
        }

        public class Transaction : IDisposable
        {
            readonly SQLiteTransaction _t;

            internal Transaction(SQLiteTransaction db) { _t = db; }

            public void Dispose() { _t.Dispose(); }
            public void Commit() { _t.Commit(); }
        }
    }

    public struct SQLParam
    {
        public string Name;
        public object Value;

        public SQLParam(string name, object val)
        {
            Name = name;
            Value = val;
        }
    }
}
