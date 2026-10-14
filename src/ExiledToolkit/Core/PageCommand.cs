using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExiledToolkit.Core;

/// <summary>
/// A message from toasts.js, sent as <c>slmod|toolkit|verb|args</c> through the web view. Anything else, or a verb
/// with bad arguments, parses to Unknown so it can be logged rather than acted on.
/// </summary>
internal abstract record PageCommand
{
    public const string Prefix = "slmod|toolkit|";

    /// <summary>More boxes than any stack can hold; a longer list is not from our page.</summary>
    public const int MaxRects = 64;

    private PageCommand()
    {
    }

    /// <summary>The script was just installed in a fresh page.</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The page answered a check or a call without the script: the web view reloaded.</summary>
    public sealed record NoScript : PageCommand;

    /// <summary>The player clicked a toast's body.</summary>
    public sealed record Click(ToastId Id) : PageCommand;

    /// <summary>A box left the screen.</summary>
    public sealed record Gone(ToastId Id) : PageCommand;

    /// <summary>Where the boxes are now, as fractions of the page, top-left origin.</summary>
    public sealed record Rects(IReadOnlyList<NormalRect> Boxes) : PageCommand;

    public sealed record Unknown(string Text) : PageCommand;

    public static bool IsOurs(string? message) => message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Parses a whole message, prefix included.</summary>
    public static PageCommand Parse(string message)
    {
        if (!IsOurs(message)) return new Unknown(message);

        var parts = message.Substring(Prefix.Length).Split('|');

        return (parts[0], parts.Length) switch
        {
            ("ready", 1) => new Ready(),
            ("noscript", 1) => new NoScript(),
            ("click", 2) when TryId(parts[1], out var id) => new Click(id),
            ("gone", 2) when TryId(parts[1], out var id) => new Gone(id),
            ("rects", 2) when TryRects(parts[1], out var boxes) => new Rects(boxes),
            _ => new Unknown(message),
        };
    }

    private static bool TryId(string text, out ToastId id)
    {
        var isNumber = long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value);

        id = new ToastId(value);

        return isNumber && value > 0;
    }

    /// <summary>"x,y,w,h;x,y,w,h", or empty for no boxes.</summary>
    private static bool TryRects(string text, out IReadOnlyList<NormalRect> boxes)
    {
        var parsed = new List<NormalRect>();
        boxes = parsed;

        if (text.Length == 0) return true;

        var items = text.Split(';');

        if (items.Length > MaxRects) return false;

        foreach (var item in items)
        {
            var numbers = item.Split(',');

            if (numbers.Length != 4) return false;

            if (!TryFraction(numbers[0], out var x) || !TryFraction(numbers[1], out var y)) return false;

            if (!TryFraction(numbers[2], out var width) || !TryFraction(numbers[3], out var height) || width < 0 || height < 0) return false;

            parsed.Add(new NormalRect(x, y, width, height));
        }

        return true;
    }

    private static bool TryFraction(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
