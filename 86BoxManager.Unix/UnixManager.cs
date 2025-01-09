using _86BoxManager.API;
using _86BoxManager.Common;
using System.IO;

namespace _86BoxManager.Unix
{
    public abstract class UnixManager : CommonManager
    {
        private readonly UnixExecutor _exec;

        protected UnixManager(string tempDir)
        {
            _exec = new UnixExecutor(tempDir);
        }

        public override IMessageLoop GetLoop(IMessageReceiver callback)
        {
            var loop = new UnixLoop(callback, _exec);
            return loop;
        }

        public override IMessageSender GetSender()
        {
            var loop = new UnixLoop(null, _exec);
            return loop;
        }

        public override IExecutor GetExecutor()
        {
            return _exec;
        }

        public override string Find(string[] folders, string[] exeNames)
        {
            foreach (var folder in folders)
            {
                try
                {
                    foreach (var exeName in exeNames)
                    {
                        var files = new DirectoryInfo(folder).GetFiles(exeName + "*");
                        if (files.Length > 0)
                            return folder;
                    }
                }
                catch { }
            }
            return null;
        }
    }
}