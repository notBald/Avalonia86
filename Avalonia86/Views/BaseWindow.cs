using Avalonia86.Core;
using Avalonia86.Tools;
using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;

namespace Avalonia86.Views;

public abstract class BaseWindow : Window
{
    protected readonly string ID;

    #region Private fields

    private Size RestoreSize;
    private PixelPoint OldPos, NewPos;
    private PixelPoint CurPos;
    private double CurWidth, CurHeight;
    private WindowState OldWindowState;

    #endregion
    public BaseWindow(string id)
    {
        ID = id;

        //Restore size is a feature missing in Avalonia, so we do it ourselves. The basic problem is that
        //we need to know the size of the window before it was maximized when saving the window size.
        RestoreSize = new Size(Width, Height);
        OldPos = NewPos = Position;
        OldWindowState = WindowState;

        Closing += BaseWindow_Closing;
        Closed += BaseWindow_Closed;
    }

    /// <summary>
    /// Must be done after the InitializeComponent call
    /// </summary>
    protected void BaseInit()
    {
        //This is where the window size is restored
        try
        {
            if (!Design.IsDesignMode)
                SetWindowSize();
        }
        catch { }

        //Workaround for missing RestoreSize property in Avalonia
        // https://github.com/AvaloniaUI/Avalonia/issues/5285#issuecomment-1764175742
        //In addition to this, we also need the old window position. We take advantage of the fact that the
        //window will not move once maximized, so we add a handler that always saves away an "oldpos".
        //
        //One problem with this implementation is that we don't handle events where the screen layout
        //changes, such as when a screen is removed. There aren't really any good ways of handling
        //this in Avalonia.
        this.GetPropertyChangedObservable(ClientSizeProperty).AddClassHandler<Visual>((t, args) =>
        {
            if (WindowState == WindowState.Normal && args.OldValue is Size rs)
            {
                //Note that we store the "old value". This way we get the position before the
                //window was maximized, as this event will fire with the Max size in NewValue
                //and "WindowState == WindowState.Normal", annoyingly enough.
                RestoreSize = rs;
            }
        });
        PositionChanged += (s, e) =>
        {
            //Note, position change before window state, so a "wrong" positon will be set in "NewPos" and
            //      the position we want to change will be set in OldPos. Then, if the WindowsState change
            //      between maximized and minimized, the OldPos will now not be overwritten as in both
            //      those cases the WindowState will not be normal.
            if (WindowState == WindowState.Normal)
            {
                OldPos = NewPos;
            }

            //CurPos is updated later, so we keep NewPos up to date
            NewPos = e.Point;
        };

        //Windows 10 workarround
        NativeMSG.SetDarkMode(this);
        if (App.Current != null)
            App.Current.PropertyChanged += Current_PropertyChanged;
    }

    private void Current_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(App.RequestedThemeVariant))
        {
            NativeMSG.SetDarkMode(this);
        }
    }

    private void BaseWindow_Closing(object sender, WindowClosingEventArgs e)
    {
        //These values are not trustable in the "Closed" handle. But we don't
        //know for sure that the window actually closed until then.
        CurPos = Position;
        CurWidth = Width;
        CurHeight = Height;
    }

    private void BaseWindow_Closed(object sender, EventArgs e)
    {
        var s = AppSettings.Settings;
        Closed -= BaseWindow_Closed;
        Closing -= BaseWindow_Closing;
        App.Current.PropertyChanged -= Current_PropertyChanged;

        using (var t = s.BeginTransaction())
        {
            if (WindowState == WindowState.Maximized || WindowState == WindowState.Minimized)
                DBStore.UpdateWindow(ID, OldPos.Y, OldPos.X, double.IsNaN(RestoreSize.Height) ? CurHeight : RestoreSize.Height, double.IsNaN(RestoreSize.Width) ? CurWidth : RestoreSize.Width, WindowState == WindowState.Maximized || OldWindowState == WindowState.Maximized);
            else
                DBStore.UpdateWindow(ID, CurPos.Y, CurPos.X, CurHeight, CurWidth, false);

            try
            {
                SaveWindowParams();
            }
            catch { }

            t.Commit();
        }
    }

    protected virtual void SaveWindowParams() { }
    protected virtual void SetWindowParams() { }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            OldWindowState = change.GetOldValue<WindowState>();
        }
            
    }

    /// <summary>
    /// Sets the window size, but makes sure not to set the window in a bad location.
    /// </summary>
    protected void SetWindowSize()
    {
        var size = DBStore.FetchWindowSize(ID);

        //If size for some reason fails to load, we do nothing. The window will the open with
        //default size.
        if (size != null && size.Width > 50 && size.Height > 50)
        {
            var left_pos = new PixelPoint((int)size.Left, (int)size.Top);
            var right_pos = new PixelPoint((int)(size.Left + size.Width), (int)size.Top);
            var windowRect = new PixelRect(left_pos, new PixelSize((int)size.Width, (int)size.Height));
            double windowArea = windowRect.Width * windowRect.Height * 0.5;
            double totalIntersectionArea = 0;
            bool isPositionValid = false;

            //What we want is to furfill two conditions.
            // 1. That the top/left position on the window is visible on at least one screen.
            //    The goal here is to avoid situations where the top of the window is above
            //    the screen.
            // 2. At least 50% of the window is visible on all screens combinded. Maybe we can
            //    reduse this number, as what we want is a decent chunk of the app visible.
            foreach (var screen in Screens.All)
            {
                var intersection = screen.Bounds.Intersect(windowRect);
                totalIntersectionArea += intersection.Width * intersection.Height;

                if (screen.Bounds.Contains(left_pos) || screen.Bounds.Contains(right_pos))
                    isPositionValid = true;
            }

            //Note that "windowArea" referes to the size of the app's window, and we've halved it
            //so that we'll pass the check with half the window intersecting with all screens.
            if (totalIntersectionArea >= windowArea && isPositionValid)
            {
                Position = left_pos;
                Width = size.Width;
                Height = size.Height;
                if (size.Maximized)
                    WindowState = WindowState.Maximized;

                SetWindowParams();
            }
        }
    }
}
