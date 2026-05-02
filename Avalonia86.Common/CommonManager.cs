#define NEW_CHECK
using Avalonia86.API;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Avalonia86.Common;

public abstract class CommonManager : IManager
{
    private IntPtr _lastEnemy;

#if NEW_CHECK
    //Static to prevent the GC from removing it.
    private static System.Threading.Mutex _appMutex;
#endif

    public virtual bool IsFirstInstance(string name)
    {
        //Keeping the old code for now. Mutex can probably be used to restore the app, but
        //I have not yet looked into it. I am also unsure if the restore feature even works
        //on Linux.
        var entry = Process.GetCurrentProcess().MainModule.FileName;
        if (entry != null)
        {
            var exeName = Path.GetFileNameWithoutExtension(entry);
            var myProcId = Environment.ProcessId;
            var processes = Process.GetProcessesByName(exeName);
            foreach (var proc in processes)
                if (proc.Id != myProcId)
                {
                    _lastEnemy = new IntPtr(proc.Id);
#if !NEW_CHECK
                    return false;
#else
                    break;
#endif
                }
        }


#if NEW_CHECK

        // This is a unique name that hopefully no other application uses
        const string MutexName = @"Global\Avalonia86_SingleInstanceLock";

        // Flag to indicate if this is the first instance
        bool isFirstInstance;

        try
        {
            // Try to create the Mutex. If it already exists, it but will return a reference to the existing one.
            _appMutex = new System.Threading.Mutex(true, MutexName, out isFirstInstance);
        }
        catch (Exception)
        {
            isFirstInstance = false;
        }

        return isFirstInstance;
#else
        return true;
#endif      
    }

    public virtual IntPtr RestoreAndFocus(string title, string handleTitle)
    {
        return _lastEnemy;
    }

    public virtual bool IsProcessRunning(string name)
    {
        var processes = Process.GetProcessesByName(name);
        return processes.Length > 0;
    }

    public virtual string FindFolderFor86Box(string[] folders, string[] exeNames)
    {
        foreach (var folder in folders)
            foreach (var exeName in exeNames)
            {
                var exePath = Path.Combine(folder, exeName);
                if (!File.Exists(exePath))
                    continue;
                return folder;
            }
        return null;
    }

    public virtual string[] List86BoxExecutables(string path)
    {
        if (Directory.Exists(path))
        {
            var di = new DirectoryInfo(path);
            var files = new List<string>();
            foreach(var exeName in di.GetFiles("86box*", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false }))
            {
                if (IsExecutable(exeName))
                    files.Add(exeName.FullName);
            }
            foreach(var exeName in di.GetFiles("pcbox*", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false }))
            {
                if (IsExecutable(exeName))
                    files.Add(exeName.FullName);
            }

            return files.ToArray();
        }

        return null;
    }

    public bool IsExecutable(string path)
    {
        return path != null && File.Exists(path) && IsExecutable(new FileInfo(path));
    }

    protected abstract bool IsExecutable(FileInfo fi);
    public abstract IVerInfo Get86BoxInfo(string path, out bool bad_image);
    public virtual IVerInfo Get86BoxInfo(Stream file)
    {
        throw new NotImplementedException();
    }

    public abstract IVerInfo GetBoxVersion(string exeDir);
    public abstract IMessageLoop GetLoop(IMessageReceiver callback);
    public abstract IMessageSender GetSender();
    public abstract IExecutor GetExecutor();
}