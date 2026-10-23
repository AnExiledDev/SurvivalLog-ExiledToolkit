using System;
using System.Collections.Generic;

namespace ExiledToolkit;

/// <summary>What a toast is about. It picks the accent colour on its left edge.</summary>
public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// Where the toast stack sits: a screen corner, or <see cref="Hud"/>, beside the game's tool buttons (phone, bag)
/// and above the action queue.
/// </summary>
public enum ToastPosition
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft,
    Hud,
}

/// <summary>Names one toast for its whole life, merges included. <see cref="None"/> is never given to a shown toast.</summary>
public readonly record struct ToastId(long Value)
{
    public static readonly ToastId None = default;

    public bool IsNone => Value == 0;
}

/// <summary>
/// One toast to show. Source and Title are required; everything else is optional. Build it with an object
/// initializer: <c>new ToastRequest("My Mod", "Saved") { Kind = ToastKind.Success }</c>.
/// </summary>
public sealed record ToastRequest(string Source, string Title)
{
    /// <summary>A second, smaller line. Null or empty shows none.</summary>
    public string? Body { get; init; }

    public ToastKind Kind { get; init; } = ToastKind.Info;

    /// <summary>A picture URL or path the web UI can load. Null shows no picture.</summary>
    public string? Icon { get; init; }

    /// <summary>Runs on the game's main thread when the player clicks the toast. The toast closes either way.</summary>
    public Action? OnClick { get; init; }

    /// <summary>How long the toast stays up. Null uses the player's setting.</summary>
    public double? Seconds { get; init; }

    /// <summary>
    /// A toast with the same Key replaces the one still on screen or waiting, in its place, instead of stacking.
    /// Use it for a status that changes, such as a progress line. Null never replaces.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Items the toast is about, shown as small chips (picture, name, amount) while the player hovers it. Null or empty
    /// shows none; the first <see cref="ToastItem.Max"/> are shown.
    /// </summary>
    public IReadOnlyList<ToastItem>? Items { get; init; }
}

/// <summary>One item chip on a toast. Label is required; Icon is a picture URL the web UI can load; Count of 0 or less shows none.</summary>
public sealed record ToastItem(string Label)
{
    /// <summary>How many chips one toast shows at most.</summary>
    public const int Max = 12;

    public string? Icon { get; init; }

    public int Count { get; init; }
}

/// <summary>
/// What <see cref="Toasts.Raised"/> reports for every toast trigger, shown or not. A trigger identical to a toast still
/// on screen or waiting merges into it: the record then carries that toast's id, the new Count, and IsMerge true.
/// </summary>
public sealed record ToastRecord(
    ToastId Id,
    string Source,
    string Title,
    string? Body,
    ToastKind Kind,
    string? Icon,
    DateTime RaisedAtUtc,
    bool HasClickAction,
    int Count,
    bool IsMerge);
