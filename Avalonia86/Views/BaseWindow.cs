using Avalonia;
using Avalonia.Controls;
using Avalonia86.Core;
using Avalonia86.Tools;
using System;

namespace Avalonia86.Views;

public abstract class BaseWindow : Window
{
    protected readonly string ID;

    #region Private fields

    //When windows are maximized / minimized, we need to know the values the window is to be restored to.
    //This value needs to be fetched before the window state changes, so we save it away.
    private Size RestoreSize;

    //These values are only usable after closing the window.
    private PixelPoint OldPos, NewPos;
    private PixelPoint CurPos;
    private double CurWidth, CurHeight;

    //For when you minimize a maximized window, we need to know that it was previously maximized.
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
    /// Called during initialization to safely restore window size and bounds from persistence.
    /// Must be invoked after InitializeComponent().
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
        //this in Avalonia (though this comment probably is out of date, and 11.2.7 has added more
        //rezise events, as noted in the issue above - I have not looked into this).
        this.GetPropertyChangedObservable(ClientSizeProperty).AddClassHandler<Visual>((t, args) =>
        {
            if (WindowState == WindowState.Normal && args.OldValue is Size rs)
            {
                //Note that we store the "old value". This way we get the position before the
                //window was maximized, as this event will fire with the Max size in NewValue
                //and "WindowState == WindowState.Normal", annoyingly enough.
                RestoreSize = rs;

                //Note, this event will fire when the app opens with the window in "normal state" and
                //      the app have a saved window position. This because the size is first set in
                //      the constructor, then later set in the window size function. This results
                //      in this handler triggering and restore size being set to the value set by the
                //      constructor.
                //
                //      This error is corrected in the OnOpened function
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

                //We need to grab the restore size before we get the window state changed event, as it is too
                //late to grab the right size then
                RestoreSize = new Size(Width, Height);
            }

            //Sometimes the value here will be wrong, but we don't know if it is 
            //a wrong or right value here, so we always keep NewPos up to date. 
            NewPos = e.Point;
        };

        //Windows 10 workaround
        NativeMSG.SetDarkMode(this);
        if (App.Current != null)
            App.Current.PropertyChanged += Current_PropertyChanged;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (WindowState == WindowState.Normal)
        {
            RestoreSize = new Size(Width, Height);

            //Makes sure the restore size has the values set by the SetWindowSize function.
            //This because restore size is overwritten by the ClientSizeProperty changed handler.
        }
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
        //know for sure that the window actually closed until then. Thus we
        //save them here and use these values in the close handler. 
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
                DBStore.UpdateWindow(ID, OldPos.Y, OldPos.X, RestoreSize.Height, RestoreSize.Width, WindowState == WindowState.Maximized || OldWindowState == WindowState.Maximized);
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
            //This is used to determine if the previous state is maximized when the window has been closed while minimized.
            OldWindowState = change.GetOldValue<WindowState>();
        }
    }

    /// <summary>
    /// Sets the window size, but makes sure not to set the window in a bad location.
    /// </summary>
    protected void SetWindowSize()
    {
        var size = DBStore.FetchWindowSize(ID);

        //If size for some reason fails to load, we do nothing. The window will then open with
        //its default size.
        if (size != null && size.Width > 50 && size.Height > 50)
        {
            var left_pos = new PixelPoint((int)size.Left, (int)size.Top);
            var right_pos = new PixelPoint((int)(size.Left + size.Width), (int)size.Top);
            var windowRect = new PixelRect(left_pos, new PixelSize((int)size.Width, (int)size.Height));
            double windowArea = windowRect.Width * windowRect.Height * 0.5;
            double totalIntersectionArea = 0;
            bool isPositionValid = false;

            //What we want is to fulfill two conditions.
            // 1. That the top/left position on the window is visible on at least one screen.
            //    The goal here is to avoid situations where the top of the window is above
            //    the screen.
            // 2. At least 50% of the window is visible on all screens combined. Maybe we can
            //    reduce this number, as what we want is a decent chunk of the app visible.
            foreach (var screen in Screens.All)
            {
                var intersection = screen.Bounds.Intersect(windowRect);
                totalIntersectionArea += intersection.Width * intersection.Height;

                if (screen.Bounds.Contains(left_pos) || screen.Bounds.Contains(right_pos))
                    isPositionValid = true;
            }

            //Note that "windowArea" refers to the size of the app's window, and we've halved it
            //so that we'll pass the check with half the window intersecting with all screens.
            if (totalIntersectionArea >= windowArea && isPositionValid)
            {
                Position = left_pos;
                Width = size.Width;
                Height = size.Height;
                if (size.Maximized)
                {
                    WindowState = WindowState.Maximized;
                    RestoreSize = new Size(size.Width, size.Height);
                }
                SetWindowParams();
            }
        }
    }
}
