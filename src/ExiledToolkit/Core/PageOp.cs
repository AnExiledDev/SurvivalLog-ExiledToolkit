using System;
using System.Collections.Generic;
using System.Linq;

namespace ExiledToolkit.Core;

/// <summary>What makes two toasts the same toast: every visible part, compared exactly.</summary>
internal sealed record ToastContent(string Source, string Title, string? Body, ToastKind Kind, string? Icon)
{
    public ToastItems Items { get; init; } = ToastItems.None;
}

/// <summary>A toast's item chips, cleaned up, compared by what they show so two toasts with the same items still merge.</summary>
internal sealed class ToastItems : IEquatable<ToastItems>
{
    public static readonly ToastItems None = new(new List<ToastItem>());

    private ToastItems(IReadOnlyList<ToastItem> list)
    {
        List = list;
    }

    public IReadOnlyList<ToastItem> List { get; }

    /// <summary>Drops items without a label, keeps the first <see cref="ToastItem.Max"/>, and makes an empty icon none.</summary>
    public static ToastItems From(IReadOnlyList<ToastItem>? items)
    {
        if (items == null || items.Count == 0) return None;

        var kept = items
            .Where(item => item != null && !string.IsNullOrEmpty(item.Label))
            .Take(ToastItem.Max)
            .Select(item => item with { Icon = string.IsNullOrEmpty(item.Icon) ? null : item.Icon, Count = Math.Max(0, item.Count) })
            .ToList();

        return kept.Count == 0 ? None : new ToastItems(kept);
    }

    public bool Equals(ToastItems? other) => other != null && List.SequenceEqual(other.List);

    public override bool Equals(object? obj) => Equals(obj as ToastItems);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in List) hash.Add(item);
        return hash.ToHashCode();
    }
}

/// <summary>One toast as the page draws it. Milliseconds is how long it stays up from the moment the page gets it.</summary>
internal sealed record ToastView(ToastId Id, ToastContent Content, int Count, int Milliseconds, bool HasClick);

/// <summary>One change to the toast stack in the web UI. A frame's changes go to the page in a single script call.</summary>
internal abstract record PageOp
{
    private PageOp()
    {
    }

    /// <summary>Puts the stack in its place on screen; sent when the page script has just been installed.</summary>
    public sealed record Place(ToastPosition Position) : PageOp;

    /// <summary>A new box on top of the stack; a box the page already has with this id is updated in place.</summary>
    public sealed record Show(ToastView Toast) : PageOp;

    /// <summary>
    /// An identical toast arrived: the box shows the new count and starts its time again, in its place. It carries the
    /// whole toast so the page can draw the box afresh when the old one is already leaving or gone.
    /// </summary>
    public sealed record Merge(ToastView Toast) : PageOp;

    /// <summary>A toast with the same key arrived: the box becomes the new toast, in its place.</summary>
    public sealed record Replace(ToastId OldId, ToastView Toast) : PageOp;

    /// <summary>The page never said this box left, long after its time; it is taken down.</summary>
    public sealed record Remove(ToastId Id) : PageOp;

    /// <summary>
    /// A frame's ops with every merge but the last for each box dropped, so a burst of one toast, dozens of times a
    /// second, costs the page one update. Each merge carries the whole count and time, so the last one says it all.
    /// </summary>
    public static IReadOnlyList<PageOp> Coalesce(IReadOnlyList<PageOp> ops)
    {
        var lastMergeAt = new Dictionary<ToastId, int>();

        for (var i = 0; i < ops.Count; i++)
        {
            if (ops[i] is Merge merge) lastMergeAt[merge.Toast.Id] = i;
        }

        return ops.Where((op, i) => op is not Merge merge || lastMergeAt[merge.Toast.Id] == i).ToList();
    }
}
