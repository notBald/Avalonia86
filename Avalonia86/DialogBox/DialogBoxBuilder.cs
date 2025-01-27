using Avalonia.Styling;
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using System.Text;
using System.Threading.Tasks;
using Avalonia86.Tools;

namespace Avalonia86.DialogBox;

public class DialogBoxBuilder
{
    private readonly DialogBoxSettings _settings = new();
    private readonly Window _parent;

    public DialogBoxBuilder(Window w) {  _parent = w; }

    public DialogBoxBuilder WithMessage(string message)
    {
        _settings.Message = message;
        return this;  // Returns self for method chaining
    }
    public DialogBoxBuilder WithTitle(string title) { _settings.Title = title; return this; }
    public DialogBoxBuilder WithButtons(DialogButtons buttons) { _settings.Buttons = buttons; return this; }
    public DialogBoxBuilder WithIcon(DialogIcon icon) { _settings.Icon = icon; return this; }
    public DialogBoxBuilder WithDefaultButton(DialogResult defaultButton) { _settings.DefaultButton = defaultButton; return this; }

    public async Task<DialogResult> ShowDialog()
    {
        var dlg = new DialogWindow() { DataContext = _settings };

        if (_parent == null)
        {
            dlg.Show();
        }
        else
        {
            dlg.Icon = _parent.Icon;
            await dlg.ShowDialog(_parent);
        }
        
        return DialogResult.Ok;
    }
}
