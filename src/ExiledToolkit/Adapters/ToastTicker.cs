using System;
using System.Collections.Generic;
using ExiledToolkit.Core;
using UnityEngine;

namespace ExiledToolkit.Adapters;

/// <summary>
/// The Unity object that hands frames to the toasts: keeps the page script installed, acts on the page's messages, and
/// sends the frame's toast changes in one script call. Each distinct failure is logged once and none is rethrown into
/// the game; one that repeats every frame would otherwise flood the log.
/// </summary>
public sealed class ToastTicker : MonoBehaviour
{
    private const int MaxReportedFailures = 20;

    internal static WebPage? Page;
    internal static Action<string> Log = _ => { };

    private static readonly HashSet<string> ReportedFailures = new();

    /// <summary>Main thread only, like the raycasts that read what it hands to <see cref="ClickRouting"/>.</summary>
    private static readonly ClickArea Area = new();
    private static volatile int _mainThreadId;

    public ToastTicker(IntPtr pointer) : base(pointer)
    {
    }

    /// <summary>Zero until the first frame, so until then every click action waits for a frame.</summary>
    internal static bool IsMainThread() => Environment.CurrentManagedThreadId == _mainThreadId;

    private void Update()
    {
        var center = Toasts.Center;
        var page = Page;
        _mainThreadId = Environment.CurrentManagedThreadId;

        if (center == null || page == null) return;

        try
        {
            var now = Time.realtimeSinceStartup;

            page.KeepInstalled(now);

            while (PageMessages.TryTake(out var command)) Handle(command, center, page, now);

            page.Apply(center.Pump(Toasts.Listeners()));

            if (!page.HasWebView) Area.Forget();

            ClickRouting.SetArea(Area.At(now));
        }
        catch (Exception ex)
        {
            ReportOnce(ex);
        }
    }

    private static void Handle(PageCommand command, ToastCenter center, WebPage page, float now)
    {
        switch (command)
        {
            case PageCommand.Ready:
                Area.Forget();
                center.PageReady();
                break;
            case PageCommand.NoScript:
                Area.Forget();
                page.Install();
                break;
            case PageCommand.Click click:
                center.Activate(click.Id);
                break;
            case PageCommand.Gone gone:
                center.Gone(gone.Id, gone.Count, gone.IsClosedByPlayer);
                break;
            case PageCommand.Rects rects:
                Area.Heard(new HitArea(rects.Boxes), now);
                break;
            case PageCommand.Unknown unknown:
                Log("unknown message from the toast page: " + unknown.Text);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }

    private static void ReportOnce(Exception ex)
    {
        var key = ex.GetType().Name + ": " + ex.Message;

        if (ReportedFailures.Count >= MaxReportedFailures || !ReportedFailures.Add(key)) return;

        Log("toast update failed (each kind of failure is logged once): " + ex);
    }
}
