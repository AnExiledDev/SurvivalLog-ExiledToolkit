namespace ExiledToolkit.Core;

/// <summary>The calls the plugin makes into typing.js.</summary>
internal static class TypingScript
{
    private const string Global = "window.__exiledtoolkittyping";

    /// <summary>Takes the keyboard from the text box that has it, which then tells the plugin no box does.</summary>
    public const string Blur = Global + "&&" + Global + ".blur()";
}
