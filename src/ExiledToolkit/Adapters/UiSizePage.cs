using ExiledToolkit.Core;

namespace ExiledToolkit.Adapters;

/// <summary>
/// ui.js in the game's root document, and the size it was last given. It is put back with the toasts, before them, so
/// a reloaded web view has it at the size set now before any mod's panel asks for it.
/// </summary>
internal sealed class UiSizePage
{
    private readonly WebPage _page;
    private int _percent = UiSize.Default;
    private bool _hasInstalled;

    public UiSizePage(WebPage page) => _page = page;

    public void Install()
    {
        _page.Install();
        _hasInstalled = true;
        _page.Send(UiSize.SetScript(_percent));
    }

    /// <summary>Before the first install there is no page to send it to; the install sends it.</summary>
    public void Set(int percent)
    {
        _percent = UiSize.Clamp(percent);

        if (_hasInstalled) _page.Send(UiSize.SetScript(_percent));
    }
}
