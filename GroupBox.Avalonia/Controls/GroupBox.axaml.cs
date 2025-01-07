using Avalonia.Controls.Primitives;

namespace GroupBox.Avalonia.Controls;
/// <summary>
/// <para>A thin wrapper for <see cref="HeaderedContentControl"/> intended for recreations of GroupBox and GroupBox-like Controls.</para>
/// From Microsoft's documentation: This Control displays a frame around a <i>group</i> of controls with an optional caption.<br/>
/// </summary>
/// <remarks>
///     If you're looking for that win32/WinForms/Aero look, set the GroupBox's Theme to {StaticResource GroupBoxClassic}.<br/>
///     If you haven't already, you may want to consider reading the <see href="https://web.archive.org/web/20221208093501/https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-group-boxes">Windows 7 design guidelines</see>. A GroupBox may be incorrect for the intended content or layout.
/// </remarks>
public partial class GroupBox : HeaderedContentControl
{
    public GroupBox() => InitializeComponent();
}
