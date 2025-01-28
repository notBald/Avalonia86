using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia86.DialogBox;

public static class DialogBox
{
    public static Task<DialogResult> ShowMsg(this Window w, string message) => Create(w).WithMessage(message).ShowDialog();
    public static Task<DialogResult> ShowMsg(this Window w, string message, string header) 
        => Create(w).WithMessage(message).WithHeader(header).ShowDialog();
    public static Task<DialogResult> ShowWarning(this Window w, string message, string header = null) 
        => Create(w).WithMessage(message).WithHeader(header).WithIcon(DialogIcon.Warning).ShowDialog();
    public static Task<DialogResult> ShowError(this Window w, string message, string header = null) 
        => Create(w).WithMessage(message).WithHeader(header).WithIcon(DialogIcon.Error).ShowDialog();
    public static Task<DialogResult> ShowQuestion(this Window w, string message, string header = null, string sub = null) 
        => Create(w).WithMessage(message).WithHeader(header, sub).WithIcon(DialogIcon.Question).WithButtons(DialogButtons.YesNo).ShowDialog();
    public static DialogBoxBuilder Create(Window w) => new DialogBoxBuilder(w);
}
