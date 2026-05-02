using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Avalonia86.Localization;

internal static class L
{
    private static IReadOnlyDictionary<string, string> _strings = En;

    private static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["Tray.Show"] = "Show Avalonia86",
        ["Tray.Settings"] = "Settings",
        ["Tray.Exit"] = "Exit",
        ["Menu.File"] = "_File",
        ["Menu.NewVm"] = "New Virtual Machine",
        ["Menu.DeleteVm"] = "Delete Virtual Machine",
        ["Menu.Exit"] = "Exit",
        ["Menu.Machine"] = "_Machine",
        ["Menu.StartMachine"] = "Start Machine",
        ["Menu.StopMachine"] = "Stop Machine",
        ["Menu.CtrlAltDel"] = "Send Ctrl-Alt-Del",
        ["Menu.ResetMachine"] = "Reset Machine",
        ["Menu.Configure"] = "Configure",
        ["Menu.Tools"] = "_Tools",
        ["Menu.ProgramSettings"] = "Program Settings",
        ["Menu.EditVmSettings"] = "Edit VM Settings",
        ["Menu.Update86Box"] = "Update 86Box",
        ["Toolbar.SortOrder"] = "Sort order",
        ["Toolbar.Start"] = "Start",
        ["Toolbar.Stop"] = "Stop",
        ["Toolbar.Resume"] = "Resume",
        ["Toolbar.Settings"] = "Settings",
        ["Toolbar.ExeManager"] = "Exe Manager",
        ["Status.AllVms"] = "All VMs:",
        ["Status.Running"] = "Running:",
        ["Status.Stopped"] = "Stopped:",
        ["Search.Filter"] = "Filter",
        ["Ctx.Pause"] = "Pause",
        ["Ctx.Kill"] = "Kill",
        ["Ctx.WipeConfig"] = "Wipe config",
        ["Ctx.Edit"] = "Edit",
        ["Ctx.Clone"] = "Clone",
        ["Ctx.Remove"] = "Remove",
        ["Ctx.OpenFolder"] = "Open folder in Explorer",
        ["Ctx.OpenConfig"] = "Open config file",
        ["Ctx.CreateShortcut"] = "Create a desktop shortcut",
        ["Info.TotalUptime"] = "Total uptime:",
        ["Info.WasLastRun"] = "Was last run:",
        ["Info.WasStarted"] = "Was started:",
        ["Info.PrinterTray"] = "Printer Tray",
        ["Info.Screenshots"] = "Screenshots:",
        ["Info.VmAge"] = "VM Age",
        ["Info.Unknown"] = "Unknown",
        ["Info.Uptime"] = "Uptime",
        ["Info.PlayCount"] = "Play count",
        ["Info.DiskUsage"] = "Disk usage",
        ["Info.Calculating"] = "Calculating...",
        ["Info.None"] = "(None)",
        ["Info.FolderMissing"] = "Failed to find the VM's folder",
        ["Info.HelpBrowse"] = "Give me a helping hand",
        ["Dialog.RunningVmsTitle"] = "Virtual machines are still running",
        ["Dialog.RunningVmsBody"] = "Some virtual machines are still running. It's recommended you stop them first before closing 86Box Manager.\n\nDo you want to stop them now?",
        ["Dialog.RunningVmsQ"] = "Do you want to stop them now?"
    };

    private static readonly IReadOnlyDictionary<string, string> ZhHans = new Dictionary<string, string>
    {
        ["Tray.Show"] = "显示 Avalonia86",
        ["Tray.Settings"] = "设置",
        ["Tray.Exit"] = "退出",
        ["Menu.File"] = "文件(_F)",
        ["Menu.NewVm"] = "新建虚拟机",
        ["Menu.DeleteVm"] = "删除虚拟机",
        ["Menu.Exit"] = "退出",
        ["Menu.Machine"] = "虚拟机(_M)",
        ["Menu.StartMachine"] = "启动虚拟机",
        ["Menu.StopMachine"] = "停止虚拟机",
        ["Menu.CtrlAltDel"] = "发送 Ctrl-Alt-Del",
        ["Menu.ResetMachine"] = "重置虚拟机",
        ["Menu.Configure"] = "配置",
        ["Menu.Tools"] = "工具(_T)",
        ["Menu.ProgramSettings"] = "程序设置",
        ["Menu.EditVmSettings"] = "编辑虚拟机设置",
        ["Menu.Update86Box"] = "更新 86Box",
        ["Toolbar.SortOrder"] = "排序方式",
        ["Toolbar.Start"] = "启动",
        ["Toolbar.Stop"] = "停止",
        ["Toolbar.Resume"] = "继续",
        ["Toolbar.Settings"] = "设置",
        ["Toolbar.ExeManager"] = "可执行文件管理",
        ["Status.AllVms"] = "虚拟机总数：",
        ["Status.Running"] = "运行中：",
        ["Status.Stopped"] = "已停止：",
        ["Search.Filter"] = "筛选",
        ["Ctx.Pause"] = "暂停",
        ["Ctx.Kill"] = "强制结束",
        ["Ctx.WipeConfig"] = "清除配置",
        ["Ctx.Edit"] = "编辑",
        ["Ctx.Clone"] = "克隆",
        ["Ctx.Remove"] = "移除",
        ["Ctx.OpenFolder"] = "在资源管理器中打开文件夹",
        ["Ctx.OpenConfig"] = "打开配置文件",
        ["Ctx.CreateShortcut"] = "创建桌面快捷方式",
        ["Info.TotalUptime"] = "总运行时长：",
        ["Info.WasLastRun"] = "上次运行：",
        ["Info.WasStarted"] = "启动于：",
        ["Info.PrinterTray"] = "打印托盘",
        ["Info.Screenshots"] = "截图：",
        ["Info.VmAge"] = "虚拟机年龄",
        ["Info.Unknown"] = "未知",
        ["Info.Uptime"] = "运行时长",
        ["Info.PlayCount"] = "运行次数",
        ["Info.DiskUsage"] = "磁盘占用",
        ["Info.Calculating"] = "计算中...",
        ["Info.None"] = "（无）",
        ["Info.FolderMissing"] = "未找到该虚拟机的文件夹",
        ["Info.HelpBrowse"] = "帮我重新定位",
        ["Dialog.RunningVmsTitle"] = "仍有虚拟机在运行",
        ["Dialog.RunningVmsBody"] = "当前仍有虚拟机在运行。建议先停止后再关闭 86Box Manager。\n\n是否现在停止？",
        ["Dialog.RunningVmsQ"] = "是否现在停止？"
    };

    private static readonly IReadOnlyDictionary<string, string> ZhHant = new Dictionary<string, string>
    {
        ["Tray.Show"] = "顯示 Avalonia86",
        ["Tray.Settings"] = "設定",
        ["Tray.Exit"] = "離開",
        ["Menu.File"] = "檔案(_F)",
        ["Menu.NewVm"] = "新增虛擬機",
        ["Menu.DeleteVm"] = "刪除虛擬機",
        ["Menu.Exit"] = "離開",
        ["Menu.Machine"] = "虛擬機(_M)",
        ["Menu.StartMachine"] = "啟動虛擬機",
        ["Menu.StopMachine"] = "停止虛擬機",
        ["Menu.CtrlAltDel"] = "傳送 Ctrl-Alt-Del",
        ["Menu.ResetMachine"] = "重置虛擬機",
        ["Menu.Configure"] = "設定",
        ["Menu.Tools"] = "工具(_T)",
        ["Menu.ProgramSettings"] = "程式設定",
        ["Menu.EditVmSettings"] = "編輯虛擬機設定",
        ["Menu.Update86Box"] = "更新 86Box",
        ["Toolbar.SortOrder"] = "排序方式",
        ["Toolbar.Start"] = "啟動",
        ["Toolbar.Stop"] = "停止",
        ["Toolbar.Resume"] = "繼續",
        ["Toolbar.Settings"] = "設定",
        ["Toolbar.ExeManager"] = "執行檔管理",
        ["Status.AllVms"] = "虛擬機總數：",
        ["Status.Running"] = "執行中：",
        ["Status.Stopped"] = "已停止：",
        ["Search.Filter"] = "篩選",
        ["Ctx.Pause"] = "暫停",
        ["Ctx.Kill"] = "強制結束",
        ["Ctx.WipeConfig"] = "清除設定",
        ["Ctx.Edit"] = "編輯",
        ["Ctx.Clone"] = "複製",
        ["Ctx.Remove"] = "移除",
        ["Ctx.OpenFolder"] = "在檔案總管中開啟資料夾",
        ["Ctx.OpenConfig"] = "開啟設定檔",
        ["Ctx.CreateShortcut"] = "建立桌面捷徑",
        ["Info.TotalUptime"] = "總執行時間：",
        ["Info.WasLastRun"] = "上次執行：",
        ["Info.WasStarted"] = "啟動於：",
        ["Info.PrinterTray"] = "印表機托盤",
        ["Info.Screenshots"] = "截圖：",
        ["Info.VmAge"] = "虛擬機年齡",
        ["Info.Unknown"] = "未知",
        ["Info.Uptime"] = "執行時間",
        ["Info.PlayCount"] = "執行次數",
        ["Info.DiskUsage"] = "磁碟用量",
        ["Info.Calculating"] = "計算中...",
        ["Info.None"] = "（無）",
        ["Info.FolderMissing"] = "找不到該虛擬機資料夾",
        ["Info.HelpBrowse"] = "幫我重新定位",
        ["Dialog.RunningVmsTitle"] = "仍有虛擬機在執行",
        ["Dialog.RunningVmsBody"] = "目前仍有虛擬機在執行。建議先停止後再關閉 86Box Manager。\n\n是否現在停止？",
        ["Dialog.RunningVmsQ"] = "是否現在停止？"
    };

    internal static void Initialize(IResourceDictionary resources)
    {
        _strings = SelectStrings();
        foreach (var pair in _strings)
        {
            resources[pair.Key] = pair.Value;
        }
    }

    internal static string T(string key)
    {
        if (_strings.TryGetValue(key, out var value))
        {
            return value;
        }
        return key;
    }

    private static IReadOnlyDictionary<string, string> SelectStrings()
    {
        var culture = CultureInfo.CurrentUICulture;
        var name = (culture.Name ?? string.Empty).ToLowerInvariant();
        var script = (culture.TextInfo?.CultureName ?? string.Empty).ToLowerInvariant();

        if (name.StartsWith("zh"))
        {
            if (name.Contains("hant") || name.StartsWith("zh-tw") || name.StartsWith("zh-hk") || name.StartsWith("zh-mo") || script.Contains("hant"))
            {
                return ZhHant;
            }
            return ZhHans;
        }

        return En;
    }
}
