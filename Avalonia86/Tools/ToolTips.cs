﻿using Avalonia.Controls;

namespace Avalonia86.Tools;

public static class ToolTips
{
    public static void SetToolTip(this Control widget, string text)
    {
        ToolTip.SetTip(widget, text);
    }

    public static void UnsetToolTip(this Control widget)
    {
        ToolTip.SetTip(widget, null);
    }
}