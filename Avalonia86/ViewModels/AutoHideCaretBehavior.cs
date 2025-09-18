
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using System;

namespace Avalonia86.ViewModels;

//Example usage:
//
//<UserControl
//    xmlns = "https://github.com/avaloniaui"
//    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
//    xmlns:i="using:Avalonia.Xaml.Interactivity"
//    xmlns:b="using:Avalonia86.ViewModels">

//    <Grid>
//        <TextBox x:Name="SearchBox"
//                 Text="{Binding FilterMachines, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
//                 Watermark="Filter machines...">
//            <i:Interaction.Behaviors>
//                <b:AutoHideCaretBehavior IdleAfter = "0:0:5" />
//            </i:Interaction.Behaviors>
//        </TextBox>
//    </Grid>
//</UserControl>

/// <summary>
/// Makes a TextBox caret visible while the user is active (focus/typing/click),
/// then hides it (CaretBrush = Transparent) after a period of inactivity.
/// On new activity, the caret becomes visible and blinks again.
/// </summary>
public sealed class AutoHideCaretBehavior : Behavior<TextBox>
{
    // Idle duration before hiding the caret (default 5 seconds)
    public static readonly StyledProperty<TimeSpan> IdleAfterProperty =
        AvaloniaProperty.Register<AutoHideCaretBehavior, TimeSpan>(
            nameof(IdleAfter), TimeSpan.FromSeconds(5));

    public TimeSpan IdleAfter
    {
        get => GetValue(IdleAfterProperty);
        set => SetValue(IdleAfterProperty, value);
    }

    private DispatcherTimer _timer;
    private bool _isAttached;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is null || _isAttached)
            return;

        // 1) Prevent attaching twice
        _isAttached = true;

        // 2) Create the timer but only start it when focused.
        _timer = new DispatcherTimer { Interval = IdleAfter };
        _timer.Tick += OnTimerTick;

        // 3) Wire up activity sources.
        AssociatedObject.GotFocus += OnActivity;
        AssociatedObject.TextChanged += OnActivity;
        AssociatedObject.KeyDown += OnActivity;

        // The text box handles mouse clicks, so we grab all clicks. It does not matter if
        // the caret is activated too aggressively.
        AssociatedObject.AddHandler(
                        InputElement.PointerPressedEvent,
                        OnPointerPressed,
                        RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                        handledEventsToo: true);


        // 4) Pause when focus leaves (optional).
        AssociatedObject.LostFocus += OnLostFocus;

        // 5) React to IdleAfter changes at runtime.
        this.PropertyChanged += OnBehaviorPropertyChanged;

        // Initial state: caret visible (if not focused yet, timer won't run).
        ShowCaretAndMaybeStartTimer();
    }

    protected override void OnDetaching()
    {
        if (!_isAttached)
            return;

        _isAttached = false;

        if (AssociatedObject is not null)
        {
            AssociatedObject.GotFocus -= OnActivity;
            AssociatedObject.TextChanged -= OnActivity;
            AssociatedObject.KeyDown -= OnActivity;
            AssociatedObject.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            //AssociatedObject.PointerPressed -= OnActivity;
            AssociatedObject.LostFocus -= OnLostFocus;

            // Restore the original caret state (theme-aware).
            AssociatedObject.ClearValue(TextBox.CaretBrushProperty);
        }

        this.PropertyChanged -= OnBehaviorPropertyChanged;

        if (_timer is not null)
        {
            _timer.Tick -= OnTimerTick;
            _timer.Stop();
            _timer = null;
        }

        base.OnDetaching();
    }

    private void OnBehaviorPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IdleAfterProperty && _timer is not null)
        {

            var next = (TimeSpan)e.NewValue!;
            _timer.Interval = next <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : next;

            if (AssociatedObject?.IsFocused == true)
                ShowCaretAndMaybeStartTimer(); // restart with new interval
        }
    }

    // Called for: GotFocus, TextChanged, KeyDown, PointerPressed
    private void OnActivity(object sender, EventArgs e)
    {
        ShowCaretAndMaybeStartTimer();
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e) => ShowCaretAndMaybeStartTimer();

    private void OnLostFocus(object sender, EventArgs e)
    {
        _timer?.Stop();
        // Ensure that when focus returns the caret will be visible immediately
        RestoreCaret();
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        _timer?.Stop();
        HideCaret(); // After idle: make caret invisible (appears to stop blinking)
    }

    private void ShowCaretAndMaybeStartTimer()
    {
        RestoreCaret();

        // Nudge the caret layer so the caret shows immediately (not waiting for blink phase).
        if (AssociatedObject is { } tb)
        {
            Dispatcher.UIThread.Post(() =>
            {
                //if (tb.IsDisposed) return;
                tb.InvalidateVisual();
                // If needed in stubborn cases:
                // var i = tb.CaretIndex; tb.CaretIndex = i;
            }, DispatcherPriority.Background);
        }

        if (AssociatedObject?.IsFocused == true)
        {
            _timer?.Stop();
            _timer?.Start();
        }
    }

    private void RestoreCaret()
    {
        if (AssociatedObject is null)
            return;

        // Restore original brush if caret is currently hidden
        if (ReferenceEquals(AssociatedObject.CaretBrush, Brushes.Transparent))
        {
            AssociatedObject.ClearValue(TextBox.CaretBrushProperty);
        }
    }

    private void HideCaret()
    {
        if (AssociatedObject is null) return;
        if (!ReferenceEquals(AssociatedObject.CaretBrush, Brushes.Transparent))
        {
            AssociatedObject.CaretBrush = Brushes.Transparent;
        }
    }
}


