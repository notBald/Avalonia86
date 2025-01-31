using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia86.Tools;

namespace Avalonia86.DialogBox;

public partial class DialogWindow : Window
{
    public DialogBoxSettings Settings { get => (DialogBoxSettings)DataContext; }

    public DialogWindow()
    {
        InitializeComponent();

        //Windows 10 workarround
        NativeMSG.SetDarkMode(this);

        Loaded += DialogWindow_Loaded;
    }

    private void DialogWindow_Loaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= DialogWindow_Loaded;
        btn1.Focus();
    }

    private void btn1_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Settings.Buttons == DialogButtons.YesNo)
            Close(Settings.IsChecked ? DialogResult.YesChecked : DialogResult.Yes);
        else
            Close(Settings.IsChecked ? DialogResult.OkChecked : DialogResult.Ok);
    }

    private void btn2_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Settings.Buttons == DialogButtons.YesNo)
            Close(DialogResult.No);
        else
            Close(DialogResult.Cancel);
    }
}