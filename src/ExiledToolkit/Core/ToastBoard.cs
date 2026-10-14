using System;
using System.Collections.Generic;
using System.Linq;

namespace ExiledToolkit.Core;

/// <summary>The board's answer to a new toast: the id it now goes by, its count, and whether it merged.</summary>
internal readonly record struct Placed(ToastId Id, int Count, bool IsMerge);

/// <summary>
/// Which toasts are on screen and which wait for room. The page reports when a box leaves; the board decides what is
/// shown next, what merges and what replaces, and turns each decision into page ops. Times are milliseconds on a
/// clock that only has to move forward.
/// </summary>
internal sealed class ToastBoard
{
    /// <summary>Beyond this many waiting, the oldest waiting toast is dropped, so a flood cannot build an endless queue.</summary>
    public const int MaxWaiting = 30;

    /// <summary>
    /// How long past its time a box may stay before the board stops waiting for the page to report it gone. Covers a
    /// web UI that is missing or reloading, a lost message, and the pause while the pointer rests on a box.
    /// </summary>
    public const long LateMilliseconds = 30_000;

    /// <summary>The shortest time a box gets when it is shown again after the page reloaded.</summary>
    public const int MinReshowMilliseconds = 1_500;

    /// <summary>
    /// The least time between two updates of one box. A toast fired every frame then costs the page a few updates a
    /// second; the count still catches up, since every update carries the whole count.
    /// </summary>
    public const long MergeEveryMilliseconds = 200;

    private readonly int _maxVisible;
    private readonly List<Entry> _visible = new();
    private readonly List<Entry> _waiting = new();

    public ToastBoard(int maxVisible)
    {
        _maxVisible = Math.Max(1, maxVisible);
    }

    public IReadOnlyList<ToastId> VisibleIds => _visible.Select(entry => entry.Id).ToList();

    public IReadOnlyList<ToastId> WaitingIds => _waiting.Select(entry => entry.Id).ToList();

    /// <summary>
    /// Takes a new toast. An identical toast on screen or waiting absorbs it; otherwise one with the same key becomes
    /// it; otherwise it goes on top of the stack, or waits when the stack is full.
    /// </summary>
    public Placed Add(ToastId id, ToastContent content, string? key, int milliseconds, bool hasClick, long now, List<PageOp> ops)
    {
        if (Find(entry => entry.Content == content) is { } same) return Merge(same, milliseconds, hasClick, now, ops);

        if (key != null && Find(entry => string.Equals(entry.Key, key, StringComparison.Ordinal)) is { } keyed)
        {
            return Replace(keyed, id, content, milliseconds, hasClick, now, ops);
        }

        var added = new Entry(id, content, key, milliseconds, hasClick);

        if (_visible.Count < _maxVisible)
        {
            ShowEntry(added, now, ops);
        }
        else
        {
            _waiting.Add(added);

            if (_waiting.Count > MaxWaiting) _waiting.RemoveAt(0);
        }

        return new Placed(id, 1, false);
    }

    /// <summary>
    /// The page says the box left (timed out, closed, or clicked) showing <paramref name="shownCount"/>; the next
    /// waiting toast takes its room. A box that left before drawing a merge stays: the merge is sent again and the page
    /// draws it as a fresh box under the same id.
    /// </summary>
    public bool Dismiss(ToastId id, int shownCount, long now, List<PageOp> ops)
    {
        if (_visible.Find(entry => entry.Id == id) is { } shown && shown.Count > shownCount)
        {
            shown.DueAt = now + shown.Milliseconds + LateMilliseconds;
            SendMerge(shown, now, ops);

            return false;
        }

        var removed = _visible.RemoveAll(entry => entry.Id == id) + _waiting.RemoveAll(entry => entry.Id == id) > 0;

        Promote(now, ops);

        return removed;
    }

    /// <summary>Sends the merges held back because their box was updated too recently.</summary>
    public void SendDueMerges(long now, List<PageOp> ops)
    {
        foreach (var entry in _visible)
        {
            if (entry.HasUnsentMerge && now - entry.LastSentAt >= MergeEveryMilliseconds) SendMerge(entry, now, ops);
        }
    }

    /// <summary>Takes down boxes the page has not reported gone long after their time.</summary>
    public void Expire(long now, List<PageOp> ops)
    {
        var overdue = _visible.Where(entry => entry.DueAt <= now).ToList();

        if (overdue.Count == 0) return;

        foreach (var entry in overdue)
        {
            _visible.Remove(entry);
            ops.Add(new PageOp.Remove(entry.Id));
        }

        Promote(now, ops);
    }

    /// <summary>Sends every box on screen again, oldest first so the newest ends on top, with the time it has left.</summary>
    public void Reshow(long now, List<PageOp> ops)
    {
        foreach (var entry in _visible)
        {
            var left = entry.DueAt - LateMilliseconds - now;
            var milliseconds = (int)Math.Clamp(left, MinReshowMilliseconds, Math.Max(entry.Milliseconds, MinReshowMilliseconds));
            entry.DueAt = now + milliseconds + LateMilliseconds;

            Sent(entry, now);
            ops.Add(new PageOp.Show(entry.View(milliseconds)));
        }
    }

    private Placed Merge(Entry entry, int milliseconds, bool hasClick, long now, List<PageOp> ops)
    {
        entry.Count++;
        entry.Milliseconds = milliseconds;
        entry.HasClick = hasClick;

        if (_visible.Contains(entry))
        {
            entry.DueAt = now + milliseconds + LateMilliseconds;
            entry.HasUnsentMerge = true;

            if (now - entry.LastSentAt >= MergeEveryMilliseconds) SendMerge(entry, now, ops);
        }

        return new Placed(entry.Id, entry.Count, true);
    }

    private Placed Replace(Entry entry, ToastId id, ToastContent content, int milliseconds, bool hasClick, long now, List<PageOp> ops)
    {
        var oldId = entry.Id;
        entry.Id = id;
        entry.Content = content;
        entry.Count = 1;
        entry.Milliseconds = milliseconds;
        entry.HasClick = hasClick;

        if (_visible.Contains(entry))
        {
            entry.DueAt = now + milliseconds + LateMilliseconds;

            Sent(entry, now);
            ops.Add(new PageOp.Replace(oldId, entry.View(milliseconds)));
        }

        return new Placed(id, 1, false);
    }

    private void Promote(long now, List<PageOp> ops)
    {
        while (_visible.Count < _maxVisible && _waiting.Count > 0)
        {
            var next = _waiting[0];

            _waiting.RemoveAt(0);
            ShowEntry(next, now, ops);
        }
    }

    private void ShowEntry(Entry entry, long now, List<PageOp> ops)
    {
        entry.DueAt = now + entry.Milliseconds + LateMilliseconds;

        _visible.Add(entry);
        Sent(entry, now);
        ops.Add(new PageOp.Show(entry.View(entry.Milliseconds)));
    }

    private static void SendMerge(Entry entry, long now, List<PageOp> ops)
    {
        Sent(entry, now);
        ops.Add(new PageOp.Merge(entry.View(entry.Milliseconds)));
    }

    /// <summary>Every op for a box carries its whole state, so after one the box has no merge left to send.</summary>
    private static void Sent(Entry entry, long now)
    {
        entry.LastSentAt = now;
        entry.HasUnsentMerge = false;
    }

    private Entry? Find(Func<Entry, bool> matches) => _visible.FirstOrDefault(matches) ?? _waiting.FirstOrDefault(matches);

    /// <summary>Mutable on purpose: a merge or a key replacement changes the toast but keeps its place in the stack.</summary>
    private sealed class Entry
    {
        public Entry(ToastId id, ToastContent content, string? key, int milliseconds, bool hasClick)
        {
            Id = id;
            Content = content;
            Key = key;
            Milliseconds = milliseconds;
            HasClick = hasClick;
        }

        public ToastId Id { get; set; }

        public ToastContent Content { get; set; }

        public string? Key { get; }

        public int Count { get; set; } = 1;

        public int Milliseconds { get; set; }

        public bool HasClick { get; set; }

        public long DueAt { get; set; } = long.MaxValue;

        /// <summary>When the page was last sent this box, on the board's clock.</summary>
        public long LastSentAt { get; set; }

        /// <summary>A merge the page has not been sent yet, held back by <see cref="MergeEveryMilliseconds"/>.</summary>
        public bool HasUnsentMerge { get; set; }

        public ToastView View(int milliseconds) => new(Id, Content, Count, milliseconds, HasClick);
    }
}
