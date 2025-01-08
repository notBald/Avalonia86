using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using _86BoxManager.API;

namespace _86BoxManager.Common
{
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

        public virtual string Find(string[] folders, string[] exeNames)
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

        public abstract IVerInfo GetBoxVersion(string exeDir);
        public abstract IMessageLoop GetLoop(IMessageReceiver callback);
        public abstract IMessageSender GetSender();
        public abstract IExecutor GetExecutor();
    }
}