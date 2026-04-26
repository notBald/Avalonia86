using Avalonia86.Core;
using Avalonia86.Tools;
using ReactiveUI;
using System.Collections.Generic;

namespace Avalonia86.ViewModels;

/// <summary>
/// The parsed configuration for the machine
/// </summary>
/// <remarks>
/// These objects are short-lived/recreated when swaping between machines. 
/// </remarks>
public class VMConfig : ReactiveObject
{
    private readonly RawConfig _config;
    Dictionary<string, string> _machine, _other, _video, _sound, _floppy, _hdd, _input;

    public bool IsDefault => _config.Count == 0;

    public VMConfig() { _config = new RawConfig(); CreateDicts(); }
    internal VMConfig(RawConfig config)
    {
        _config = config;
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
                if (SystemType == "8088" || SystemType == "8086" || SystemType == "ISA")
                {
                    kb_mem = (int) RangeCalculator.Calculate((uint)kb_mem, IsaMemory);
                }

                memory = kb_mem * 1024;
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

    private class MemInfo
    {
        public string start;
        public string size;
        public bool ems;
        public string ems_start;
        public MemInfo(string start, string size, bool ems = false, string ems_start = null)
        {
            this.start = start;
            this.size = size;
            this.ems = ems;
            this.ems_start = ems_start;
        }
    }

    public (string start, string size)[] IsaMemory
    {
        get
        {
            //Known issue:
            // Should look at the start address. If memory overlaps, it should only be counted once.
            List<(string start, string size)> list = new List<(string start, string size)>(4);

            for (int c = 0; c < 4; c++)
            {
                if (_other.TryGetValue("isamem" + c + "_type", out string mem_card))
                {
                    MemInfo def_range = null;

                    switch (mem_card)
                    {
                        case "lotechems": // Lo-tech EMS Board
                            def_range = new(null, "2048", true, "260H");
                            break;
                        case "brxt": // BocaRAM/XT
                            def_range = new(null, "512", true, "268H");
                            break;
                        case "ev159": // Everex EV-159 RAM 3000 Deluxe
                            //Known issue: Ignoring the 268H > 2MB range
                            def_range = new("0", "512", false, "258H");
                            break;
                        case "genericat": // Generic PC/AT Memory Expansion
                        case "ibmat": // IBM PC/AT Memory Expansion
                            def_range = new("1024", "512");
                            break;
                        case "ems5150": // Micro Mainframe EMS-5150(T)
                            //This card is disabled by default
                            def_range = new(null, "256", false, null);
                            break;
                        case "ev165a": // Everex Maxi Magic EV-165A
                            def_range = new("64", "256", false, "258H");
                            break;
                        case "p5pak": // Paradise Systems 5-PAK
                        case "ibmat_128k":
                            def_range = new("512", "128");
                            break;
                        case "ibmxt": // IBM PC/XT 64/256K Memory Expansion Option
                            def_range = new("256", "128");
                            break;
                        case "mplus2": // AST MegaPlus II
                        case "a6pak": // AST SixPakPlus
                            def_range = new("256", "64");
                            break;
                        case "ibmxt_64k": // IBM PC/XT 64K Memory Expansion Option
                            def_range = new("64", "64");
                            break;
                        case "msramcard": // Microsoft RAMCard
                        case "mssystemcard": // Microsoft SystemCard
                            def_range = new("0", "64");
                            break;
                        case "ibmxt_32k": // IBM PC/XT 32K Memory Expansion Option
                            def_range = new("64", "32");
                            break;
                        case "genericxt": // Generic PC/XT Memory Expansion
                            def_range = new("0", "16");
                            break;
                    }

                    if (HWDB.Device.TryGetValue(mem_card, out string full_name) 
                        && _config.TryGetValue(full_name+" #"+(c+1), out var dict))
                    {
                        string start = null;
                        string size = null;
                        bool is_ems = def_range != null && def_range.ems;

                        if (!dict.TryGetValue("size", out size) && def_range != null)
                            size = def_range.size;
                        if (!dict.TryGetValue("start", out start) && def_range != null)
                            start = def_range.start;

                        if (start == null && def_range != null && !def_range.ems)
                        {
                            //This is the ems5150. It is disabled if the base key is not there
                            if (!dict.ContainsKey("base"))
                                size = null;

                            is_ems = true;
                        }

                        
                        if (dict.ContainsKey("ems"))
                            is_ems = dict["ems"] == "1";
                            
                        //For now we blindly add EMS
                        if (is_ems)
                            start = null;

                        if (size != null)
                            list.Add((start, size));
                    }
                    else if (def_range != null)
                    {
                        //The ems5150 is disabled, so we filter it out here
                        if(def_range.start != null && !def_range.ems)
                            list.Add((def_range.start, def_range.size));
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
