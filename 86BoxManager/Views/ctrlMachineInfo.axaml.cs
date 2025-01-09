using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace _86BoxManager.Views;

public partial class ctrlMachineInfo : UserControl
{
    public ctrlMachineInfo()
    {
        //Yes, we're doing this before InitializeComponent. This avoids harmless binding errors from filling up the debug window.
        // Didn't have this before, since DataContext used to be sett in App OnFrameworkInitializationCompleted, but now it's set
        // in frmMain (before initializing components over there), so we get a unwanted non-null datacontect inherithed here.
        // Avalonia supresses the "null" errors, by design, so we had no trouble until now.
        DataContext = new ViewModels.VMConfig();

        InitializeComponent();
    }
}