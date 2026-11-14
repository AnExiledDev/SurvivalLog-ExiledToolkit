namespace ExiledToolkit.Game;

/// <summary>
/// The other people playing in this game, for mods that pause the game or change its speed on their own. A multiplayer
/// mod reports here every frame while a game is shared. With no report this copy plays alone, and every value is the
/// one for a game played alone. Toolkit 0.22.0 and newer.
/// </summary>
public static class OtherPlayers
{
    private static readonly object Gate = new();
    private static int _count;
    private static bool _isGuest;
    private static bool _isAnyBusy;

    /// <summary>How many other people play in this game. 0 when this copy plays alone.</summary>
    public static int Count
    {
        get { lock (Gate) return _count; }
    }

    /// <summary>
    /// True when this copy joined someone else's game. Its own world doesn't run: the host's game sets the time and the
    /// speed, so a mod here should leave both alone.
    /// </summary>
    public static bool IsGuest
    {
        get { lock (Gate) return _isGuest; }
    }

    /// <summary>True when another player has an order waiting or running, so the game is busy even if this player is not.</summary>
    public static bool IsAnyBusy
    {
        get { lock (Gate) return _isAnyBusy; }
    }

    /// <summary>
    /// What the multiplayer mod sees now. A guest always counts the host, so it has at least one other player. Nobody
    /// else can be busy when nobody else is playing.
    /// </summary>
    public static void Report(int count, bool isGuest, bool isAnyBusy)
    {
        var others = count < 0 ? 0 : count;
        if (isGuest && others == 0) others = 1;

        lock (Gate)
        {
            _count = others;
            _isGuest = isGuest;
            _isAnyBusy = others > 0 && isAnyBusy;
        }
    }

    /// <summary>The shared game ended: this copy plays alone again.</summary>
    public static void Clear() => Report(0, false, false);
}
