using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Dto;
using System.Linq;
using StartLoc = Avalonia.Controls.WindowStartupLocation;
using Avalonia.Platform.Storage;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace _86BoxManager.Tools
{
    internal static class Dialogs
    {
        //private static Task<ButtonResult> ShowMessageBox(string msg, Icon icon, Window parent,
        //    ButtonEnum buttons = ButtonEnum.Ok, string title = "Attention")
        //{
        //    var loc = parent == null ? StartLoc.CenterScreen : StartLoc.CenterOwner;
        //    var opts = new MessageBoxStandardParams
        //    {
        //        ButtonDefinitions = buttons,
        //        ContentTitle = title,
        //        ContentMessage = msg,
        //        Icon = icon,
        //        CanResize = false,
        //        WindowStartupLocation = loc,
        //        SizeToContent = SizeToContent.WidthAndHeight
        //    };
        //    var window = MessageBoxManager.GetMessageBoxStandard(opts);
        //    var raw = parent != null ? window.ShowWindowDialogAsync(parent) : window.ShowAsync();

        //    //Todo: This code is key to getting it running
        //    //if (Application.Current is var app)
        //    //{
        //    //    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        //    //    var windowField = window.GetType().GetField("_window", flags)!;
        //    //    var windowObj = (Window)windowField.GetValue(window)!;
        //    //    if (parent?.Icon is { } wi)
        //    //        windowObj.Icon = wi;
        //    //    app.Run(windowObj);
        //    //}

        //    return raw;
        //}

        public static void DispatchMSGBox(string msg, Icon icon, Window parent,
            ButtonEnum buttons = ButtonEnum.Ok, string title = "Attention")
        {
            var aw = ShowMessageBox(msg, icon, parent, buttons, title);
            Dispatcher.UIThread.Post(async () => await aw);
        }

        public static async Task<ButtonResult> ShowMessageBox(string msg, Icon icon, Window parent,
            ButtonEnum buttons = ButtonEnum.Ok, string title = "Attention")
        {
            var loc = parent == null ? StartLoc.CenterScreen : StartLoc.CenterOwner;
            var opts = new MessageBoxStandardParams
            {
                ButtonDefinitions = buttons,
                ContentTitle = title,
                ContentMessage = msg,
                Icon = icon,
                CanResize = false,
                WindowStartupLocation = loc,
                SizeToContent = SizeToContent.WidthAndHeight
            };
            var window = MessageBoxManager.GetMessageBoxStandard(opts);
            var raw = parent != null ? window.ShowWindowDialogAsync(parent) : window.ShowAsync();

            return await raw;
        }

        [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
        public static async Task RunDialog(this Window parent, Window dialog, Action func = null)
        {
            dialog.WindowStartupLocation = StartLoc.CenterOwner;
            dialog.Icon = parent.Icon;

            var raw = dialog.ShowDialog(parent);
            await raw;
            func?.Invoke();
            (dialog as IDisposable)?.Dispose();
        }

        public static async Task<string> SelectFolder(string dir, string title, Window parent)
        {
            Uri uri;
            if (!Uri.TryCreate("file://" + dir, UriKind.Absolute, out uri))
            {
                Uri.TryCreate("file://" + Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), UriKind.Absolute, out uri);
                if (uri == null)
                {
                    await Dialogs.ShowMessageBox("Failed to open file dialog.", Icon.Error, parent);
                    //We let the exception happen further down
                }
            }
            var tl = TopLevel.GetTopLevel(parent);
            var folder = await tl.StorageProvider.TryGetFolderFromPathAsync(uri);
            var res = await tl.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = title,
                SuggestedStartLocation = folder
            });

            string fld = null;

            foreach(var s in res)
            {
                try { fld = s.Path.LocalPath; }
                catch { fld = s.Path.ToString(); }
            }

            if (string.IsNullOrWhiteSpace(fld))
                return null;
            return fld;
        }

        public static async Task<string> SaveFile(string title, string dir, string filter,
            Window parent, string ext = null)
        {
            //Todo: figure out how the file type filtering is supposed to work. This is a get it to compile impl:
            FilePickerFileType[] fpft = ext == null ? Array.Empty<FilePickerFileType>() : new FilePickerFileType[] { new FilePickerFileType(ext) };

            Uri.TryCreate("file://" + dir, UriKind.Absolute, out var uri);
            var tl = TopLevel.GetTopLevel(parent);
            var folder = await tl.StorageProvider.TryGetFolderFromPathAsync(uri);
            var res = await tl.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = title,
                SuggestedStartLocation = folder,
                FileTypeFilter = fpft
            });

            //if (filter != null)
            //{
            //    var tmp = filter.Split('|', 2);
            //    dialog.Filters = new List<FileDialogFilter>
            //    {
            //        new() { Name = tmp.First(), Extensions = new List<string> { tmp.Last() } }
            //    };
            //}

            string fld = null;

            foreach (var s in res)
            {
                fld = s.Path.AbsolutePath;
            }

            if (string.IsNullOrWhiteSpace(fld))
                return null;
            return fld;
        }
    }


}