using System;
using System.Collections.Generic;
using System.Linq;

namespace ExiledToolkit.Core;

/// <summary>A box on the page as fractions of the page's size, top-left origin, the way the game's pages report theirs.</summary>
internal readonly record struct NormalRect(double X, double Y, double W, double H)
{
    /// <summary>Left and top edges count, right and bottom do not, as in the game's own check.</summary>
    public bool Contains(double x, double y) => x >= X && x < X + W && y >= Y && y < Y + H;
}

/// <summary>
/// Where the toast boxes are, so a click on one reaches the web view. The game only lets a click through to its web
/// UI where one of its pages reported a button, and the toast stack is not one of its pages.
/// </summary>
internal sealed class HitArea
{
    public static readonly HitArea Empty = new(Array.Empty<NormalRect>());

    private readonly NormalRect[] _boxes;

    public HitArea(IEnumerable<NormalRect> boxes)
    {
        _boxes = boxes.ToArray();
    }

    public bool IsEmpty => _boxes.Length == 0;

    /// <summary>
    /// A point in the web view's rect transform, as Unity gives it, mapped to page fractions with the game's own
    /// formula (WebUIVm.IsPointInInteractiveArea): Unity's y grows upward, the page's grows downward.
    /// </summary>
    public bool ContainsLocalPoint(double localX, double localY, double width, double height, double pivotX, double pivotY)
    {
        if (IsEmpty || width <= 0 || height <= 0) return false;

        var x = localX / width + pivotX;
        var y = 1 - (localY / height + pivotY);

        return _boxes.Any(box => box.Contains(x, y));
    }
}
