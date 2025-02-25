using System;
using System.IO;
using System.Net;

namespace Avalonia86.Tools;

internal class NetworkShareDetector
{
    public static bool IsNetworkShare(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        try
        {
            Uri uri = new Uri(path);
            if (uri.IsUnc)
            {
                return true;
            }

            DriveInfo drive = new DriveInfo(Path.GetPathRoot(path));
            return drive.DriveType == DriveType.Network;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    public static string GetNetworkSharePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        try
        {
            Uri uri = new Uri(path);
            if (uri.IsUnc)
            {
                return uri.LocalPath;
            }

            DriveInfo drive = new DriveInfo(Path.GetPathRoot(path));
            if (drive.DriveType == DriveType.Network)
            {
                return drive.RootDirectory.FullName;
            }

            return path;
        }
        catch (UriFormatException)
        {
            return path;
        }
    }
}