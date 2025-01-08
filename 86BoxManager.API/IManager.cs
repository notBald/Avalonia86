using System;
using System.IO;

namespace _86BoxManager.API
{
    public interface IManager
    {
        bool IsFirstInstance(string name);

        IntPtr RestoreAndFocus(string title, string handleTitle);

        bool IsProcessRunning(string name);

        IVerInfo GetBoxVersion(string exeDir);

        string Find(string[] folders, string[] exeNames);

        IMessageLoop GetLoop(IMessageReceiver callback);

        IMessageSender GetSender();

        IExecutor GetExecutor();
    }
}