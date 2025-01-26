using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia86.DialogBox;

public class DialogBoxBuilder
{
    private DialogBoxSettings _settings = new();
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
        throw new NotImplementedException();
        //// 1. Create DialogBoxView
        //var view = new DialogBoxView(_settings);

        //// 2. Show as modal dialog
        //return await view.ShowDialog<DialogResult>();
    }
}
