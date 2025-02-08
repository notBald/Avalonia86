using Avalonia86.API;

namespace Avalonia86.Common;

public sealed class CommonVerInfo : IVerInfo
{
    public int FilePrivatePart { get; set; }

    public int FileMajorPart { get; set; }

    public int FileMinorPart { get; set; }

    public int FileBuildPart { get; set; }

    public string Arch { get; set; }

    public override string ToString()
    {
        return $"86Box v{FileMajorPart}.{FileMinorPart}.{FileBuildPart} - Build: {FilePrivatePart} for {Arch}";
    }
}