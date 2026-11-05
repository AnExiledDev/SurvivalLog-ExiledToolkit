using System.Globalization;

namespace ExiledToolkit.Kit;

/// <summary>Words a mod puts on screen, in English and Simplified Chinese. Toolkit 0.19.0 and newer.</summary>
public readonly record struct Phrase(string En, string Zh)
{
    public string Pick(bool isChinese) => isChinese ? Zh : En;

    /// <summary>The picked text with its {0}, {1}, ... filled in, numbers written the same in every culture.</summary>
    public string Say(bool isChinese, params object[] args) => string.Format(CultureInfo.InvariantCulture, Pick(isChinese), args);

    /// <summary>A line the game wrote itself, which is already in the player's language.</summary>
    public static Phrase FromGame(string line) => new(line, line);
}

/// <summary>A phrase about a count: English says "1 trap" and "2 traps", Chinese has one form for both. {0} is the count.</summary>
public readonly record struct Counted(Phrase One, Phrase Many)
{
    /// <summary>The phrase for this count with the count as {0} and <paramref name="more"/> as {1}, {2}, ...</summary>
    public string Say(bool isChinese, int count, params object[] more)
    {
        var args = new object[more.Length + 1];

        args[0] = count;
        more.CopyTo(args, 1);

        return (count == 1 ? One : Many).Say(isChinese, args);
    }
}
