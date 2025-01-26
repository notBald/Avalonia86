using Avalonia86.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System.Collections.Generic;
using System.IO;

namespace Avalonia86.Views;

public partial class ctrlSetExecutable : UserControl
{
    public ctrlSetExecutable()
    {
        InitializeComponent();
    }
}

public class ctrlSetExecutableModel : ReactiveObject
{
    private ExeModel _exeModel;

    public string Default86BoxFolder { get; private set; }
    public string Default86BoxRoms { get; private set; }
    internal List<ExeModel> ExeFiles { get; } = new();

    public string SelExePath
    {
        get
        {
            try
            {
                if (SelectedItem != null)
                {
                    if (!string.IsNullOrWhiteSpace(SelectedItem.VMExe))
                        return SelectedItem.VMExe;

                    if (!string.IsNullOrWhiteSpace(Default86BoxFolder))
                    {
                        return Path.Combine(Default86BoxFolder, "< 86Box >");
                    }
                }
            }
            catch { }

            return "";
        }
    }

    public string SelVersion
    {
        get
        {
            if (SelectedItem != null)
            {
                var v = SelectedItem.Version;

                if (string.IsNullOrWhiteSpace(v))
                {
                    var b = SelectedItem.Build;

                    if (!string.IsNullOrWhiteSpace(b))
                        return b;
                }
                else
                {
                    var b = SelectedItem.Build;

                    if (!string.IsNullOrWhiteSpace(b))
                        return $"{v} - Build {b}";

                    return v;
                }
            }
            return "";
        }
    }

    public string SelExeRomDir
    {
        get
        {
            try
            {
                if (SelectedItem != null)
                {
                    if (!string.IsNullOrWhiteSpace(SelectedItem.VMRoms))
                        return SelectedItem.VMRoms;

                    if (!string.IsNullOrWhiteSpace(SelectedItem.VMExe))
                    {
                        var dir = Path.Combine(Path.GetDirectoryName(SelectedItem.VMExe), "roms");
                        if (Directory.Exists(dir))
                            return dir;
                    }

                    if (!string.IsNullOrWhiteSpace(Default86BoxRoms))
                    {
                        if (Directory.Exists(Default86BoxRoms))
                            return Default86BoxRoms;
                    }

                    if (!string.IsNullOrWhiteSpace(Default86BoxFolder))
                    {
                        var dir = Path.Combine(Path.GetDirectoryName(Default86BoxFolder), "roms");
                        if (Directory.Exists(dir))
                            return dir;
                    }
                }
            }
            catch { }

            return "";
        }
    }

    internal ExeModel SelectedItem
    {
        get => _exeModel;
        set
        {
            if (!ReferenceEquals(value, _exeModel))
            {
                this.RaiseAndSetIfChanged(ref _exeModel, value);
                this.RaisePropertyChanged(nameof(SelExeRomDir));
                this.RaisePropertyChanged(nameof(SelExePath));
                this.RaisePropertyChanged(nameof(SelVersion));
            }
        }
    }

    public ctrlSetExecutableModel() : this(null)
    { }

    internal void SetSelectedExe(long uid, AppSettings s)
    {
        if (s != null)
        {
            var id = s.IdToExeId(uid);
            foreach (var exe in ExeFiles)
            {
                if (exe.ID == id)
                {
                    SelectedItem = exe;
                    break;
                }
            }
        }
    }

    internal ctrlSetExecutableModel(AppSettings s)
    {
        var exeModel = new ExeModel()
        {
            Name = "Default 86Box executable"
        };
        ExeFiles.Add(exeModel);

        if (s == null)
        {
            //For the designer
            ExeFiles.Add(new ExeModel()
            {
                Name = "86Box 3.11"
            });
        }
        else
        {
            Default86BoxFolder = s.EXEdir;
            Default86BoxRoms = s.ROMdir;

            foreach (var r in s.ListExecutables())
            {
                ExeFiles.Add(new ExeModel()
                {
                    ID = (long)r["ID"],
                    Name = r["Name"] as string,
                    VMExe = r["VMExe"] as string,
                    VMRoms = r["VMRoms"] as string,
                    Version = r["Version"] as string,
                    Comment = r["Comment"] as string,
                    Build = r["Build"] as string,
                    Arch = r["Arch"] as string,
                });
            }

            bool has_def = false;
            foreach (var r in s.GetDefExe())
            {
                exeModel.Build = r["Build"] as string;
                exeModel.Version = r["Version"] as string;
                exeModel.Arch = r["Arch"] as string;
                exeModel.VMExe = (string)r["VMExe"];
                exeModel.Comment = r["comment"] as string;

                has_def = true;
                break;
            }

            if (!has_def)
            {
                var (exe, info) = VMCenter.GetPathExeInfo();
                if (info != null)
                {
                    if (info.FilePrivatePart > 0)
                        exeModel.Build = "" + info.FilePrivatePart;
                    if (info.FileMajorPart > 0)
                        exeModel.Version = $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}";
                    if (!string.IsNullOrWhiteSpace(info.Arch))
                        exeModel.Arch = info.Arch;
                    exeModel.VMExe = exe;
                }
            }
        }

        SelectedItem = exeModel;
    }
}