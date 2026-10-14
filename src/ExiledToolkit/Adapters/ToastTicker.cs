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
            page.KeepInstalled(Time.realtimeSinceStartup);

            while (PageMessages.TryTake(out var command)) Handle(command, center, page);

            page.Apply(center.Pump(Toasts.Listeners()));
        }
        catch (Exception ex)
        {
            ReportOnce(ex);
        }
    }

    private static void Handle(PageCommand command, ToastCenter center, WebPage page)
    {
        switch (command)
        {
            case PageCommand.Ready:
                ClickRouting.SetArea(HitArea.Empty);
                center.PageReady();
                break;
            case PageCommand.NoScript:
                ClickRouting.SetArea(HitArea.Empty);
                page.Install();
                break;
            case PageCommand.Click click:
                center.Activate(click.Id);
                break;
            case PageCommand.Gone gone:
                center.Gone(gone.Id);
                break;
            case PageCommand.Rects rects:
                ClickRouting.SetArea(new HitArea(rects.Boxes));
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
