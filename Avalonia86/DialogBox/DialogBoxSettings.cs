using System.Collections.Generic;

namespace Avalonia86.DialogBox;
public class DialogBoxSettings
{
    public string Message { get; set; }
    public string Title { get; set; }
    public DialogButtons Buttons { get; set; }
    public DialogIcon Icon { get; set; }
    public DialogResult DefaultButton { get; set; }
    public Dictionary<string, DialogResult> CustomButtons { get; set; }
}
