using System;
using System.IO;

namespace Avalonia86.API;

public interface IManager
{
    bool IsFirstInstance(string name);

    IntPtr RestoreAndFocus(string title, string handleTitle);

    bool IsProcessRunning(string name);

    IVerInfo Get86BoxInfo(string path, out bool bad_image);

    IVerInfo Get86BoxInfo(Stream file);

    IVerInfo GetBoxVersion(string exeDir);

    bool IsExecutable(string path);

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