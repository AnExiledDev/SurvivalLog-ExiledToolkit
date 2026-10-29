using System.Collections.Generic;
using ExiledToolkit.Core;

namespace ExiledToolkit.Adapters;

/// <summary>
/// ui.js in the game's root document, the size it was last given and each mod's own size. It is put back with the
/// toasts, before them, so a reloaded web view has every size set now before any mod's panel asks for it.
/// </summary>
internal sealed class UiSizePage
{
    private readonly WebPage _page;
    private readonly Dictionary<string, int> _mods = new();
    private int _percent = UiSize.Default;
    private bool _hasInstalled;

    public UiSizePage(WebPage page) => _page = page;

    public void Install()
    {
        _page.Install();
        _hasInstalled = true;
        _page.Send(UiSize.SetScript(_percent));

        foreach (var mod in _mods) _page.Send(UiSize.ModScript(mod.Key, mod.Value));
    }

    /// <summary>Before the first install there is no page to send it to; the install sends it.</summary>
    public void Set(int percent)
    {
        _percent = UiSize.Clamp(percent);

        if (_hasInstalled) _page.Send(UiSize.SetScript(_percent));
    }

    /// <summary>A mod's own size, or 0 to have it follow the player's size again.</summary>
    public void SetMod(string mod, int percent)
    {
        UiSize.CheckedName(mod);

        if (percent > 0) _mods[mod] = UiSize.Clamp(percent);
        else _mods.Remove(mod);

        if (_hasInstalled) _page.Send(UiSize.ModScript(mod, percent));
    }
}
