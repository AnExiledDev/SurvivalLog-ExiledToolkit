using System;

namespace ExiledToolkit.Core;

/// <summary>A message from menu.js, sent as <c>slmod|toolkitmenu|verb|args</c>. Anything else parses to Unknown.</summary>
internal abstract record MenuCommand
{
    public const string Prefix = "slmod|toolkitmenu|";

    private MenuCommand()
    {
    }

    /// <summary>The script was just installed in a fresh page.</summary>
    public sealed record Ready : MenuCommand;

    /// <summary>The page answered without the script: the web view reloaded.</summary>
    public sealed record NoScript : MenuCommand;

    /// <summary>The player picked this entry; the menu has already closed.</summary>
    public sealed record Run(string Id) : MenuCommand;

    /// <summary>The menu opened or closed.</summary>
    public sealed record Open(bool IsOpen) : MenuCommand;

    /// <summary>Something the page could not do, for the log.</summary>
    public sealed record Diagnostic(string Text) : MenuCommand;

    public sealed record Unknown(string Text) : MenuCommand;

    public static MenuCommand Parse(string message)
    {
        if (!message.StartsWith(Prefix, StringComparison.Ordinal)) return new Unknown(message);

        var body = message.Substring(Prefix.Length);
        var parts = body.Split('|');

        return (parts[0], parts.Length) switch
        {
            ("ready", 1) => new Ready(),
            ("noscript", 1) => new NoScript(),
            ("run", 2) when MenuBoard.IsValidId(parts[1]) => new Run(parts[1]),
            ("open", 2) when parts[1] is "1" or "0" => new Open(parts[1] == "1"),
            ("diag", > 1) => new Diagnostic(body.Substring("diag|".Length)),
            _ => new Unknown(message),
        };
    }
}
