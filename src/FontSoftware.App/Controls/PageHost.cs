using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace FontSoftware.App.Controls;

/// <summary>
/// The headerless tab control that shows one page at a time, driven by the sidebar.
/// A stock TabControl reports only its tab headers to UI Automation, and these tabs
/// have none, so screen readers (and UI tests) saw an empty window. This one reports
/// what is actually on screen: the open page and its controls.
/// </summary>
public sealed class PageHost : TabControl
{
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
