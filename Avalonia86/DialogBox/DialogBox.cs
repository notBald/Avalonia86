using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia86.DialogBox;

public class DialogBox
{
    public static Task<DialogResult> Show(string message) => Create().WithMessage(message).ShowDialog();
    public static Task<DialogResult> Show(string message, string title) 
        => Create().WithMessage(message).WithTitle(title).ShowDialog();
    public static Task<DialogResult> ShowWarning(string message, string title = "Warning") 
        => Create().WithMessage(message).WithTitle(title).WithIcon(DialogIcon.Warning).ShowDialog();
    public static Task<DialogResult> ShowError(string message, string title = "Error") 
        => Create().WithMessage(message).WithTitle(title).WithIcon(DialogIcon.Error).ShowDialog();
    public static Task<DialogResult> ShowQuestion(string message, string title = "Question") 
        => Create().WithMessage(message).WithTitle(title).WithIcon(DialogIcon.Question).WithButtons(DialogButtons.YesNo).ShowDialog();
    public static DialogBoxBuilder Create() => new DialogBoxBuilder();
}
