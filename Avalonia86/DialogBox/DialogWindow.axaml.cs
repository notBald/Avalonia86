using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia86.Tools;

namespace Avalonia86.DialogBox;

public partial class DialogWindow : Window
{
    public DialogWindow()
    {
        InitializeComponent();

        //Windows 10 workarround
        NativeMSG.SetDarkMode(this);
    }
}