using System;
using System.Collections.Concurrent;
using HarmonyLib;
using Vuplex.WebView.Internal;

namespace ExiledToolkit.Adapters;

/// <summary>
/// Catches messages our page scripts send ("slmod|name|...") before the game's web layer sees them (it would log them
/// as unknown) and queues each for whoever listens for its prefix. One Harmony prefix serves every listener. Static
/// because a Harmony prefix has nowhere else to look.
/// </summary>
internal static class PageInbox
{
    private static readonly object Gate = new();
    private static volatile PageMailbox[] _mailboxes = Array.Empty<PageMailbox>();
    private static Action<string> _log = _ => { };
    private static bool _hasReportedFailure;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(BaseWebView), "HandleMessageEmitted"), prefix: new HarmonyMethod(typeof(PageInbox), nameof(TakeOurs)));
            log("listening for page messages");
        }
        catch (Exception ex)
        {
            log("could not hook page messages, so toasts will not show and mod windows will not answer clicks (mods are still told about toasts): " + ex.Message);
        }
    }

    /// <summary>A mailbox for every message starting with <paramref name="prefix"/>; asking twice for one prefix gives the same mailbox.</summary>
    public static PageMailbox Listen(string prefix)
    {
        lock (Gate)
        {
            foreach (var existing in _mailboxes)
            {
                if (existing.Prefix == prefix) return existing;
            }

            var mailbox = new PageMailbox(prefix);
            var grown = new PageMailbox[_mailboxes.Length + 1];
            _mailboxes.CopyTo(grown, 0);
            grown[^1] = mailbox;
            _mailboxes = grown;

            return mailbox;
        }
    }

    /// <summary>A throw here would break every web message in the game, so any failure lets the game handle the message.</summary>
    private static bool TakeOurs(string serializedMessage)
    {
        try
        {
            foreach (var mailbox in _mailboxes)
            {
                if (!serializedMessage.StartsWith(mailbox.Prefix, StringComparison.Ordinal)) continue;

                mailbox.Put(serializedMessage);

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportFailureOnce(ex);

            return true;
        }
    }

    private static void ReportFailureOnce(Exception ex)
    {
        if (_hasReportedFailure) return;

        _hasReportedFailure = true;

        _log("reading a page message failed, the game handles it instead: " + ex.Message);
    }
}

/// <summary>Messages for one prefix, put on the web view's thread and taken on the main thread.</summary>
internal sealed class PageMailbox
{
    private readonly ConcurrentQueue<string> _queue = new();

    public PageMailbox(string prefix)
    {
        Prefix = prefix;
    }

    public string Prefix { get; }

    public void Put(string message) => _queue.Enqueue(message);

    /// <summary>The whole message, prefix included.</summary>
    public bool TryTake(out string message)
    {
        if (_queue.TryDequeue(out var queued))
        {
            message = queued;

            return true;
        }

        message = "";

        return false;
    }
}
