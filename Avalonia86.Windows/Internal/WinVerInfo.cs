﻿using System.Diagnostics;
using Avalonia86.API;

namespace Avalonia86.Windows.Internal;

internal sealed class WinVerInfo : IVerInfo
{
    private readonly FileVersionInfo _info;

    public WinVerInfo(FileVersionInfo info)
    {
        _info = info;
    }

    public int FilePrivatePart => _info.FilePrivatePart;
    public int FileMajorPart => _info.FileMajorPart;
    public int FileMinorPart => _info.FileMinorPart;
    public int FileBuildPart => _info.FileBuildPart;

    public string Arch { get; set; }

    public override string ToString() => _info.ToString();
}