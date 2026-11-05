namespace ExiledToolkit.Game;

/// <summary>Names and words from the game's tables, in the language the game shows. Toolkit 0.19.0 and newer.</summary>
public static partial class GameNames
{
    /// <summary>Null for null, empty or blank text; the text itself otherwise.</summary>
    public static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    /// <summary>The game hands a key back, or nothing, when its language table has no text for it.</summary>
    internal static string? Translated(string key, string? text) => string.IsNullOrWhiteSpace(text) || text == key ? null : text;

    /// <summary>The order every mod names an item in: the table's text for its name key, its own local name, then the key.</summary>
    internal static string? FirstName(string? translated, string? localName, string? nameKey) =>
        translated ?? NonEmpty(localName) ?? NonEmpty(nameKey);
}
