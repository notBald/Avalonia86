using System;
using Avalonia;
using Avalonia.Controls;
using System.Collections.Generic;

namespace Avalonia86.DialogBox;
public class DialogBoxSettings
{
    public string Message { get; set; }
    public string Title { get; set; }
    public string Header { get; set; }
    public string Subheader { get; set; }
    public DialogButtons Buttons { get; set; }
    public DialogIcon Icon { get; set; }
    public DialogResult DefaultButton { get; set; }
    public Dictionary<string, DialogResult> CustomButtons { get; set; }
    public bool Banner {  get; set; }
    public string Btn1 { get; set; }
    public string Btn2 { get; set; }
    public bool ShowBtn2 { get; set; }
    public bool IsChecked { get; set; }
    public string Checkbox { get; set; }
    public Exception Error { get; set; }
    public Thickness Border
    {
        get
        {
            if (Banner)
                return new Thickness(0, 1);
            return new Thickness(0, 0, 0, 1);
        }
    }
        


#if DEBUG
    public DialogBoxSettings()
    {
        if (Design.IsDesignMode)
        {
            Subheader = "Sub header";
            Message = "Hello world, here is a sentence of text.";
            Banner = true;
            ShowBtn2 = true;
            Checkbox = "Be nice";
        }
    }
#endif
}
