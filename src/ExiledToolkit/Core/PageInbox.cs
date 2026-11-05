using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ExiledToolkit.Web;

/// <summary>
/// Catches the messages page scripts send ("slmod|name|...") before the game's web layer sees them (it would log them
/// as unknown) and queues each for the mailbox listening for its prefix. One Harmony hook on the game's message handler
/// serves every mod. Toolkit 0.19.0 and newer (Mod Manager used it before).
/// </summary>
public static partial class PageInbox
{
    private static readonly object Gate = new();

    /// <summary>Longest prefix first, so "slmod|x|timer|" wins over "slmod|x|". Replaced whole, never changed in place.</summary>
    private static volatile PrefixRoute[] _routes = Array.Empty<PrefixRoute>();
    private static volatile PageMailbox[] _mailboxes = Array.Empty<PageMailbox>();
    private static Action<string> _log = _ => { };
    private static int _hasReportedFailure;

    /// <summary>Whether the Toolkit's hook is in. False means no mailbox will ever get a message (the log says why).</summary>
    public static bool IsListening { get; internal set; }

    /// <summary>A mailbox for every message starting with <paramref name="prefix"/>; asking twice for one prefix gives the same mailbox.</summary>
    public static PageMailbox Listen(string prefix) => Listen(new[] { prefix });

    /// <summary>
    /// The same, and <paramref name="onMessage"/> is called on the game's main thread, in the Toolkit's update of the
    /// frame after each message arrives, with the whole message. A handler that throws is logged once and does not stop the others.
    /// A later handler for the mailbox replaces this one.
    /// </summary>
    public static PageMailbox Listen(string prefix, Action<string> onMessage)
    {
        var mailbox = Listen(prefix);

        mailbox.OnMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));

        return mailbox;
    }

    /// <summary>
    /// One mailbox for every message starting with any of <paramref name="prefixes"/>. A prefix that already has a
    /// mailbox brings that one, and the other prefixes are added to it. Two prefixes that already belong to two
    /// different mailboxes are refused.
    /// </summary>
    public static PageMailbox Listen(params string[] prefixes)
    {
        CheckPrefixes(prefixes);

        lock (Gate)
        {
            var mailbox = ExistingFor(prefixes) ?? AddMailbox(prefixes[0]);

            foreach (var prefix in prefixes) AddRoute(prefix, mailbox);

            return mailbox;
        }
    }

    /// <summary>
    /// Runs on the web view's thread for every message the game's pages send. True when a mailbox took it, so the game
    /// does not see it.
    /// </summary>
    internal static bool Route(string? message)
    {
        if (message == null) return false;

        foreach (var route in _routes)
        {
            if (!message.StartsWith(route.Prefix, StringComparison.Ordinal)) continue;

            route.Mailbox.Put(message, Stopwatch.GetTimestamp());

            return true;
        }

        return false;
    }

    /// <summary>Main thread only, once a frame: hands every waiting message to the mailboxes that have a handler.</summary>
    internal static void Deliver()
    {
        foreach (var mailbox in _mailboxes)
        {
            if (mailbox.OnMessage is { } handler) mailbox.DeliverTo(handler, _log);
        }
    }

    internal static void UseLog(Action<string> log) => _log = log;

    /// <summary>Runs on the web view's thread, so the once-only flag is set atomically.</summary>
    internal static void ReportRouteFailureOnce(Exception ex)
    {
        if (Interlocked.Exchange(ref _hasReportedFailure, 1) == 0) _log("reading a page message failed, the game handles it instead (reported once): " + ex.Message);
    }

    private static void CheckPrefixes(string[] prefixes)
    {
        if (prefixes == null || prefixes.Length == 0) throw new ArgumentException("at least one prefix is needed", nameof(prefixes));

        foreach (var prefix in prefixes)
        {
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("a prefix may not be empty", nameof(prefixes));
        }
    }

    private static PageMailbox? ExistingFor(string[] prefixes)
    {
        PageMailbox? found = null;

        foreach (var prefix in prefixes)
        {
            var owner = OwnerOf(prefix);

            if (owner == null) continue;

            if (found != null && found != owner) throw new ArgumentException("these prefixes already belong to two different mailboxes: " + string.Join(", ", prefixes), nameof(prefixes));

            found = owner;
        }

        return found;
    }

    private static PageMailbox? OwnerOf(string prefix)
    {
        foreach (var route in _routes)
        {
            if (route.Prefix == prefix) return route.Mailbox;
        }

        return null;
    }

    private static PageMailbox AddMailbox(string firstPrefix)
    {
        var mailbox = new PageMailbox(firstPrefix);
        var grown = new PageMailbox[_mailboxes.Length + 1];

        _mailboxes.CopyTo(grown, 0);
        grown[^1] = mailbox;
        _mailboxes = grown;

        return mailbox;
    }

    private static void AddRoute(string prefix, PageMailbox mailbox)
    {
        if (OwnerOf(prefix) != null) return;

        var routes = new List<PrefixRoute>(_routes) { new(prefix, mailbox) };

        routes.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
        mailbox.AddPrefix(prefix);
        _routes = routes.ToArray();
    }

    private sealed record PrefixRoute(string Prefix, PageMailbox Mailbox);
}

/// <summary>Messages for one or more prefixes, put on the web view's thread and taken on the main thread.</summary>
public sealed class PageMailbox
{
    private readonly ConcurrentQueue<(string Message, long ArrivedAt)> _queue = new();
    private volatile string[] _prefixes = Array.Empty<string>();
    private bool _hasReportedHandlerFailure;

    internal PageMailbox(string prefix)
    {
        Prefix = prefix;
    }

    /// <summary>The first prefix the mailbox was made for.</summary>
    public string Prefix { get; }

    /// <summary>Every prefix the mailbox takes.</summary>
    public IReadOnlyList<string> Prefixes => _prefixes;

    /// <summary>Whether a message is waiting. Any thread.</summary>
    public bool HasWaiting => !_queue.IsEmpty;

    internal Action<string>? OnMessage { get; set; }

    /// <summary>The oldest waiting message, whole, prefix included.</summary>
    public bool TryTake(out string message) => TryTake(out message, out _);

    /// <summary>The same, with when it arrived as a <see cref="Stopwatch.GetTimestamp"/> reading taken on the web view's thread.</summary>
    public bool TryTake(out string message, out long arrivedAt)
    {
        if (_queue.TryDequeue(out var queued))
        {
            message = queued.Message;
            arrivedAt = queued.ArrivedAt;

            return true;
        }

        message = "";
        arrivedAt = 0;

        return false;
    }

    internal void Put(string message, long arrivedAt) => _queue.Enqueue((message, arrivedAt));

    internal void AddPrefix(string prefix)
    {
        var grown = new string[_prefixes.Length + 1];

        _prefixes.CopyTo(grown, 0);
        grown[^1] = prefix;
        _prefixes = grown;
    }

    internal void DeliverTo(Action<string> handler, Action<string> log)
    {
        while (TryTake(out var message))
        {
            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                ReportHandlerFailureOnce(ex, log);
            }
        }
    }

    private void ReportHandlerFailureOnce(Exception ex, Action<string> log)
    {
        if (_hasReportedHandlerFailure) return;

        _hasReportedHandlerFailure = true;
        log($"a handler for page messages starting {Prefix} failed (reported once): {ex}");
    }
}
