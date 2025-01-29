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
    public DialogBoxBuilder WithHeader(string header, string sub = null) { _settings.Header = header; _settings.Subheader = sub; return this; }
    public DialogBoxBuilder WithException(Exception err) { _settings.Error = err; return this; }
    public DialogBoxBuilder WithButtons(DialogButtons buttons) { _settings.Buttons = buttons; return this; }
    public DialogBoxBuilder WithIcon(DialogIcon icon) { _settings.Icon = icon; return this; }
    public DialogBoxBuilder WithDefaultButton(DialogResult defaultButton) { _settings.DefaultButton = defaultButton; return this; }
    public DialogBoxBuilder WithCheckbox(string text, bool def_state = false) { _settings.Checkbox = text; _settings.IsChecked = def_state; return this; }

    public async Task<DialogResult> ShowDialog()
    {
        if (_settings.Error != null)
            Program.AddError(_settings.Message, "Unknown", _settings.Error);

        if (_settings.Icon == DialogIcon.None)
            _settings.Icon = DialogIcon.Information;
        if (_settings.Title == null)
        {
            switch(_settings.Icon)
            {
                case DialogIcon.Error:
                    _settings.Title = "Error";
                    break;

                case DialogIcon.Warning:
                    _settings.Title = "Warning";
                    break;

                case DialogIcon.Question:
                    _settings.Title = "Question";
                    break;

                default:
                    _settings.Title = "Information";
                    break;
            }
        }
        if (_settings.Header == null)
            _settings.Header = _settings.Title;
        else
            _settings.Banner = true;
        if (_settings.Subheader == null && _settings.Error != null)
            _settings.Subheader = _settings.Error.Message;
        _settings.ShowBtn2 = _settings.Buttons != DialogButtons.Ok;
        if (_settings.Buttons == DialogButtons.YesNo)
        {
            _settings.Btn1 = "Yes";
            _settings.Btn2 = "No";
        }

        var dlg = new DialogWindow() { DataContext = _settings };

        if (_parent == null)
        {
            dlg.Show();
        }
        else
        {
            dlg.Icon = _parent.Icon;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return await dlg.ShowDialog<DialogResult>(_parent);
        }
        
        return DialogResult.Ok;
    }
}
