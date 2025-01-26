using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace _86BoxManager.ViewModels
{
    /// <summary>
    /// For when I don't want to battle with the internal impl. of toggle button. Basically, this
    /// toggle button leaves everything to the implementor.
    /// </summary>
    [PseudoClasses(":checked", ":unchecked")]
    internal class StyleButton : Button
    {
        /// <summary>
        /// Defines the <see cref="IsChecked"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsCheckedProperty =
            AvaloniaProperty.Register<ToggleButton, bool>(nameof(IsChecked), false,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets whether the <see cref="ToggleButton"/> is checked.
        /// </summary>
        public bool IsChecked
        {
            get => GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public StyleButton()
        {
            UpdatePseudoClasses(IsChecked);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsCheckedProperty)
            {
                var newValue = change.GetNewValue<bool>();

                UpdatePseudoClasses(newValue);
            }
        }

        private void UpdatePseudoClasses(bool isChecked)
        {
            PseudoClasses.Set(":checked", isChecked == true);
            PseudoClasses.Set(":unchecked", isChecked == false);
        }
    }
}
