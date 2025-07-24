using Avalonia86.Core;
using Avalonia86.Models;
using Avalonia86.Tools;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia86.ViewModels;

/// <summary>
/// The parsed configuration for the machine
/// </summary>
/// <remarks>
/// These objects are short-lived/recreated when swaping between machines. 
/// </remarks>
public class VMConfig : ReactiveObject
{
    /// <remarks>
    /// Object uowned by the UI thread.
    /// </remarks>
    private readonly VMVisual _vis;
    private bool _show_desc = false, _show_com = false;

    private readonly RawConfig _config;
    Dictionary<string, string> _machine, _other, _video, _sound, _floppy, _hdd, _input;

    public bool IsDefault => _config.Count == 0;

    public VMConfig() { _config = new RawConfig(); CreateDicts(); }
    internal VMConfig(VMVisual vis)
    {
        _vis = vis;
        _config = vis.VMConfig;
        CreateDicts();
    }

    private void CreateDicts()
    {
        if (!_config.TryGetValue("Machine", out _machine))
            _machine = new Dictionary<string, string>();
        if (!_config.TryGetValue("Other peripherals", out _other))
            _other = new Dictionary<string, string>();
        if (!_config.TryGetValue("Video", out _video))
            _video = new Dictionary<string, string>();
        if (!_config.TryGetValue("Sound", out _sound))
            _sound = new Dictionary<string, string>();
        if (!_config.TryGetValue("Floppy and CD-ROM drives", out _floppy))
            _floppy = new Dictionary<string, string>();
        if (!_config.TryGetValue("Hard disks", out _hdd))
            _hdd = new Dictionary<string, string>();
        if (!_config.TryGetValue("Input devices", out _input))
            _input = new Dictionary<string, string>();
    }

    public string SystemDescription
    {
        get
        {
            //We are on the UI thread.
            if (_vis != null)
            {
                var desc = _vis.Desc;
                this.RaiseAndSetIfChanged(ref _show_desc, !string.IsNullOrEmpty(desc), nameof(ShowDescription));
                return desc;
            }
            return "";
        }
    }

    public bool ShowDescription 
    {
        get 
        {
            // Quick race condition fix. A proper fix should compute the visibility when this property is requested.
            this.RaisePropertyChanged(nameof(SystemDescription));
            return _show_desc;
        }
    }

    public string SystemComment
    {
        get
        {
            //We are on the UI thread.
            if (_vis != null)
            {
                var com = _vis.Comment;
                this.RaiseAndSetIfChanged(ref _show_com, !string.IsNullOrEmpty(com), nameof(ShowComment));
                return com;
            }
            return "";
        }
    }

    public bool ShowComment
    {
        get
        {
            // Quick race condition fix. A proper fix should compute the visibility when this property is requested.
            this.RaisePropertyChanged(nameof(SystemComment));
            return _show_com;
        }
    }

    private string SystemInternal
    {
        get
        {
            string machine;

            if (!_machine.TryGetValue("machine", out machine))
                machine = "ibmpc";

            return machine;
        }
    }

    public string SystemMachine
    {
        get
        {
            string machine = SystemInternal;

            if (HWDB.Machines.TryGetValue(machine, out var full_name))
                return full_name.Value;

            return machine;
        }
    }

    public string SystemType
    {
        get
        {
            string machine = SystemInternal;

            if (HWDB.Machines.TryGetValue(machine, out var full_name))
                return full_name.Type;

            return "8088";
        }
    }

    public string SystemMemory
    {
        get
        {
            int memory = 1024 * 64;

            if (_machine.TryGetValue("mem_size", out string str_mem) && int.TryParse(str_mem, out int kb_mem) )
            {
                memory = kb_mem * 1024;

                if (SystemType == "8088")
                {
                    foreach(var s in IsaMemory)
                    {
                        if (int.TryParse(s, out int num))
                            memory += num * 1024;
                    }
                }
            }

            return FolderSizeCalculator.ConvertBytesToReadableSize(memory);
        }
    }

    public string CPU
    {
        get
        {
            if (_machine.TryGetValue("cpu_family", out string cpu_str)
                && HWDB.Cpus.TryGetValue(cpu_str, out var vt))
            {
                return $"{vt.Type} {vt.Value}";
            }

            return "Intel 8088";
        }
    }

    public double Clockspeed
    {
        get
        {
            double mhz = 4.77;

            if (_machine.TryGetValue("cpu_speed", out string speed) && int.TryParse(speed, out int mh))
                mhz = mh / 1000000d;

            return mhz;
        }
    }

    public string CPUandMHz
    {
        get
        {
            return $"{CPU} / {Clockspeed:0.##} MHz";
        }
    }

    public string[] IsaMemory
    {
        get
        {
            List<string> list = new List<string>(4);

            for (int c = 0; c < 4; c++)
            {
                if (_other.TryGetValue("isamem" + c + "_type", out string mem_card))
                {
                    if (HWDB.Device.TryGetValue(mem_card, out string full_name) 
                        && _config.TryGetValue(full_name+" #"+(c+1), out var dict)
                        && dict.TryGetValue("size", out string size))
                    {
                        list.Add(size);
                    }
                    else
                    {
                        switch(mem_card)
                        {
                            case "ibmxt":
                                list.Add("128");
                                break;
                            case "ibmxt_64k":
                                list.Add("64");
                                break;
                            case "ibmxt_32k":
                                list.Add("32");
                                break;
                        }
                    }
                }
            }

            return list.ToArray();
        }
    }

    public string System2D
    {
        get
        {
            string int_name = "cga";

            if (_video.TryGetValue("gfxcard", out string gpu))
                int_name = gpu;

            if (HWDB.Video.TryGetValue(int_name, out string full_name))
                return full_name;

            return int_name;
        }
    }

    public string System3D
    {
        get
        {
            string int_name = "None";

            if (_video.TryGetValue("voodoo", out string gpu))
            {
                if (gpu == "1")
                {
                    int_name = "3Dfx Voodoo";

                    if (_config.TryGetValue("3Dfx Voodoo Graphics", out var dict))
                    {
                        if (dict.TryGetValue("type", out string type))
                        {
                            if (type == "1")
                            {
                                int_name = "Obsidian SB50";
                            }
                            else if (type == "2")
                            {
                                int_name += " 2";
                            }
                        }

                        if (dict.TryGetValue("sli", out string sli))
                        {
                            if (sli == "1")
                                int_name += " SLI";
                        }
                    }
                }
            }

            if (HWDB.Video.TryGetValue(int_name, out string full_name))
                return full_name;

            return int_name;
        }
    }

    public string SystemAud
    {
        get
        {
            string int_name = "None";

            if (_sound.TryGetValue("sndcard", out string snd))
                int_name = snd;

            if (HWDB.Sound.TryGetValue(int_name, out string full_name))
                return full_name;

            return int_name;
        }
    }

    public string SystemMidi
    {
        get
        {
            string int_name = "None";

            if (_sound.TryGetValue("midi_device", out string midi))
                int_name = midi;

            if (HWDB.Sound.TryGetValue(int_name, out string full_name))
                return full_name;

            return int_name;
        }
    }

    public string Floppy1
    {
        get
        {
            string int_name = "525_2dd";

            if (_floppy.TryGetValue("fdd_01_type", out string fdd))
                int_name = fdd;

            if (HWDB.FDD.TryGetValue(int_name, out string full_name))
                return full_name;

            return int_name;
        }
    }

    public string Floppy2
    {
        get
        {
            string int_name = "525_2dd";

            if (_floppy.TryGetValue("fdd_02_type", out string fdd))
                int_name = fdd;

            if (HWDB.FDD.TryGetValue(int_name, out string full_name))
                return full_name;

            return int_name;
        }
    }

    public string Floppy
    {
        get
        {
            return $"{Floppy1}, {Floppy2}";
        }
    }

    public string HardDisk
    {
        get
        {
            int hdd_id = 1;
            long bytes = 0;
            while(_hdd.TryGetValue($"hdd_{hdd_id:00}_parameters", out string pars))
            {
                string[] values = pars.Split(',');
                if (values.Length > 3)
                {
                    int s, h, c;
                    if (int.TryParse(values[0], out s) && int.TryParse(values[1], out h) && int.TryParse(values[2], out c))
                    {
                        bytes += s * h * c * 512L;
                    }
                }

                hdd_id++;
            }

            if (bytes != 0)
                return FolderSizeCalculator.ConvertBytesToReadableSize(bytes);

            return "None";
        }
    }

    public string CDROM
    {
        get
        {
            string int_name = "None";

            if (_floppy.TryGetValue("cdrom_01_parameters", out string cd))
            {
                int_name = "Yes";
            }

            return int_name;
        }
    }

    public string Mouse
    {
        get
        {
            string int_name = "None";

            if (_input.TryGetValue("mouse_type", out string mouse))
            {
                int_name = mouse;

                if (HWDB.Device.TryGetValue(int_name, out string full_name))
                    return full_name;
            }

            return int_name;
        }
    }

    public string Joystick
    {
        get
        {
            string int_name = "None";

            if (_input.TryGetValue("joystick_type", out string joystick))
            {
                int_name = joystick;

                if (HWDB.Device.TryGetValue(int_name, out string full_name))
                    return full_name;
            }

            return int_name;
        }
    }

    public bool IsSameDict(RawConfig config) => ReferenceEquals(config, _config);
}
