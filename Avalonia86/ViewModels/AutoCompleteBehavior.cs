using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reflection;

namespace Avalonia86.ViewModels;

/// <summary>
/// https://github.com/AvaloniaUI/Avalonia/discussions/13301#discussioncomment-7324389
/// </summary>
internal class AutoCompleteBehavior : Behavior<AutoCompleteBox>
{
    static AutoCompleteBehavior()
    {
    }

    protected override void OnAttached()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyUp += (s, e) => ShowDropdown();
            AssociatedObject.GotFocus += (s, e) => ShowDropdown();
            AssociatedObject.DropDownOpening += DropDownOpening;
        }

        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyUp += (s, e) => ShowDropdown();
            AssociatedObject.GotFocus -= (s, e) => ShowDropdown();
            AssociatedObject.DropDownOpening -= DropDownOpening;
        }

        base.OnDetaching();
    }

    private void DropDownOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var prop = AssociatedObject?.GetType().GetProperty("TextBox", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
#nullable enable
        var tb = (TextBox?)prop?.GetValue(AssociatedObject);
#nullable disable
        if (tb is not null && tb.IsReadOnly)
        {
            e.Cancel = true;
            return;
        }
    }

    private void ShowDropdown()
    {
        if (AssociatedObject is not null && !AssociatedObject.IsDropDownOpen)
        {
            typeof(AutoCompleteBox).GetMethod("PopulateDropDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(AssociatedObject, new object[] { AssociatedObject, EventArgs.Empty });
            typeof(AutoCompleteBox).GetMethod("OpeningDropDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(AssociatedObject, new object[] { false });

            if (!AssociatedObject.IsDropDownOpen)
            {
                //We *must* set the field and not the property as we need to avoid the changed event being raised (which prevents the dropdown opening).
                var ipc = typeof(AutoCompleteBox).GetField("_ignorePropertyChange", BindingFlags.NonPublic | BindingFlags.Instance);

                if (ipc?.GetValue(AssociatedObject) is bool ipcValue && !ipcValue)
                {
                    ipc.SetValue(AssociatedObject, true);
                }

                AssociatedObject.SetCurrentValue<bool>(AutoCompleteBox.IsDropDownOpenProperty, true);
            }
        }
    }
}
