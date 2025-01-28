using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia86.DialogBox;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using StartLoc = Avalonia.Controls.WindowStartupLocation;

namespace Avalonia86.Tools;

internal static class Dialogs
{
    public delegate void DialogResult(DialogBox.DialogResult? result);


    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    public static async Task RunDialog(this Window parent, Window dialog, DialogResult func = null)
    {
        try
        {
            if (!(dialog is Views.BaseWindow))
                dialog.WindowStartupLocation = StartLoc.CenterOwner;

            var raw = dialog.ShowDialog<object>(parent);

            dialog.Icon = parent.Icon;
            await raw;
            func?.Invoke(raw.Result as DialogBox.DialogResult?);
        }
        finally
        {
            (dialog as IDisposable)?.Dispose();
        }

    }

    public static async Task<string> SelectFolder(string dir, string title, Window parent)
    {
        Uri uri;
        if (!Uri.TryCreate("file://" + dir, UriKind.Absolute, out uri))
        {
            Uri.TryCreate("file://" + Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), UriKind.Absolute, out uri);
            if (uri == null)
            {
                await parent.ShowError("Failed to open file dialog.");
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
        FilePickerFileType[] fpft = ext == null ? Array.Empty<FilePickerFileType>() : [ new FilePickerFileType(ext) { Patterns = [ $"*{ext}"], MimeTypes = [ "*/*"] }, FilePickerFileTypes.All ];

        Uri.TryCreate("file://" + dir, UriKind.Absolute, out var uri);
        var tl = TopLevel.GetTopLevel(parent);
        var folder = await tl.StorageProvider.TryGetFolderFromPathAsync(uri);
        var res = await tl.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedStartLocation = folder,
            FileTypeChoices = fpft
        });

        string fld = null;

        if (res != null)
        {
            fld = res.Path.AbsolutePath;
        }

        if (string.IsNullOrWhiteSpace(fld))
            return null;
        return fld;
    }

    public static async Task<string> OpenFile(string title, string dir, string filter,
        Window parent, string ext = null)
    {
        FilePickerFileType[] fpft = ext == null ? Array.Empty<FilePickerFileType>() : [new FilePickerFileType(ext) { Patterns = [$"*{ext}"], MimeTypes = ["*/*"] }, FilePickerFileTypes.All];

        Uri.TryCreate("file://" + dir, UriKind.Absolute, out var uri);
        var tl = TopLevel.GetTopLevel(parent);
        var folder = await tl.StorageProvider.TryGetFolderFromPathAsync(uri);
        var res = await tl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = folder,
            AllowMultiple = false,
            FileTypeFilter = fpft
        });

        string file = null;

        if (res.Count == 1)
        {
            file = res[0].Path.LocalPath;
        }

        if (string.IsNullOrWhiteSpace(file))
            return null;
        return file;
    }
}