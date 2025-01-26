using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Interactivity;
using System.ComponentModel;

namespace Avalonia86.DialogBox
{
    internal class DialogWindow : Window
    {
        public DialogWindow(DialogBoxSettings settings)
        {
            Title = settings.Title;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = settings.Message,
                        Margin = new Thickness(10),
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        //Children =
                        //{
                        //    CreateButton(settings, DialogResult.Cancel),
                        //    CreateButton(settings, DialogResult.No),
                        //    CreateButton(settings, DialogResult.Yes),
                        //    CreateButton(settings, DialogResult.Ok)
                        //}
                    }
                }
            };

            if (settings.Icon != DialogIcon.None)
            {
                //window.Icon = new WindowIcon(settings.Icon);
            }
        }
    }
}
