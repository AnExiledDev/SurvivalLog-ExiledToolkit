using ExiledToolkit.Core;

namespace ExiledToolkit.Adapters;

/// <summary>
/// dock.js in the game's root document and the HUD button size it was last given, sent again each time the script is
/// put back after the web view reloads.
/// </summary>
internal sealed class DockPage
{
    private readonly WebPage _page;
    private int _percent = ButtonSize.Default;
    private bool _hasInstalled;

    public DockPage(WebPage page) => _page = page;

    public void Install()
    {
        _page.Install();
        _hasInstalled = true;
        _page.Send(ButtonSize.SetScript(_percent));
    }

    /// <summary>Before the first install there is no page to send it to; the install sends it.</summary>
    public void Set(int percent)
    {
        _percent = ButtonSize.Clamp(percent);

        if (_hasInstalled) _page.Send(ButtonSize.SetScript(_percent));
    }
}
