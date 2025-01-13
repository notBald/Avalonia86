using System;
using System.IO;

namespace _86BoxManager.API
{
    public interface IManager
    {
        bool IsFirstInstance(string name);

        IntPtr RestoreAndFocus(string title, string handleTitle);

        bool IsProcessRunning(string name);

        ExeInfo Get86BoxInfo(string path);

        IVerInfo GetBoxVersion(string exeDir);

        /// <summary>
        /// List paths to executables. 
        /// </summary>
        /// <param name="path">Taget folder</param>
        /// <returns>A list of executables that is potentially 86Box, or null if the target folder is not found</returns>
        string[] List86BoxExecutables(string path);

        string FindFolderFor86Box(string[] folders, string[] exeNames);

        IMessageLoop GetLoop(IMessageReceiver callback);

        IMessageSender GetSender();

        IExecutor GetExecutor();
    }
}