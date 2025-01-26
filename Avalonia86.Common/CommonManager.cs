using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using Avalonia86.API;

namespace Avalonia86.Common;

public abstract class CommonManager : IManager
{
    private IntPtr _lastEnemy;

    public virtual bool IsFirstInstance(string name)
    {
        var entry = Process.GetCurrentProcess().MainModule.FileName;//Assembly.GetEntryAssembly()?.Location;
        if (entry != null)
        {
            var exeName = Path.GetFileNameWithoutExtension(entry);
            var myProcId = Environment.ProcessId;
            var processes = Process.GetProcessesByName(exeName);
            foreach (var proc in processes)
                if (proc.Id != myProcId)
                {
                    _lastEnemy = new IntPtr(proc.Id);
                    return false;
                }
        }
        return true;
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

            return files.ToArray();
        }

        return null;
    }

    public bool IsExecutable(string path)
    {
        return path != null && File.Exists(path) && IsExecutable(new FileInfo(path));
    }

    protected abstract bool IsExecutable(FileInfo fi);
    public abstract IVerInfo Get86BoxInfo(string path);
    public virtual IVerInfo Get86BoxInfo(Stream file)
    {
        throw new NotImplementedException();
    }

    public abstract IVerInfo GetBoxVersion(string exeDir);
    public abstract IMessageLoop GetLoop(IMessageReceiver callback);
    public abstract IMessageSender GetSender();
    public abstract IExecutor GetExecutor();
}