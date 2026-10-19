using System;
using System.Collections.Generic;
using ExiledToolkit.Core;
using UnityEngine;

namespace ExiledToolkit.Adapters;

/// <summary>
/// Drives the Mods menu once a frame: keeps menu.js installed, sends the entry list when it changes, runs the entry the
/// player picked, and while the menu is open holds the game's hotkeys and closes the menu on Esc (the page gets no keys).
/// </summary>
internal sealed class MenuRunner
{
    private const float SyncSeconds = 0.25f;
    private const string Holder = "exiledtoolkit.modsmenu";
    private const int MaxReportedFailures = 20;

    private readonly WebPage _page;
    private readonly PageMailbox _mailbox = PageInbox.Listen(MenuCommand.Prefix);
    private readonly Action<string> _log;
    private readonly HashSet<string> _reportedFailures = new();
    private float _nextSyncAt;
    private bool _isOpen;

    public MenuRunner(WebPage page, Action<string> log)
    {
        _page = page;
        _log = log;
    }

    public void Tick(float now)
    {
        _page.KeepInstalled(now);

        while (_mailbox.TryTake(out var message)) Handle(MenuCommand.Parse(message));

        if (_isOpen && Input.GetKeyDown(KeyCode.Escape)) _page.Send(MenuScript.Close);

        if (now < _nextSyncAt) return;

        _nextSyncAt = now + SyncSeconds;
        Sync();
    }

    private void Sync()
    {
        var script = ModsMenu.Pending(GameLanguage.IsChinese(), IsShown);

        if (script != null && _page.Send(script)) ModsMenu.Shown(script);
    }

    private void Handle(MenuCommand command)
    {
        switch (command)
        {
            case MenuCommand.Ready:
                ModsMenu.PageReset();
                _nextSyncAt = 0;
                break;
            case MenuCommand.NoScript:
                ModsMenu.PageReset();
                SetOpen(false);
                _page.Install();
                break;
            case MenuCommand.Run run:
                Click(run.Id);
                break;
            case MenuCommand.Open open:
                SetOpen(open.IsOpen);
                break;
            case MenuCommand.Diagnostic diagnostic:
                LogOnce("diag " + diagnostic.Text, "Mods menu page: " + diagnostic.Text);
                break;
            case MenuCommand.Unknown unknown:
                LogOnce("unknown " + unknown.Text, "unknown message from the Mods menu page: " + unknown.Text);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }

    private void SetOpen(bool isOpen)
    {
        _isOpen = isOpen;

        if (isOpen) HotkeyGate.Hold(Holder);
        else HotkeyGate.Release(Holder);
    }

    private void Click(string id)
    {
        var entry = ModsMenu.Find(id);

        if (entry == null) return;

        try
        {
            entry.OnClick();
        }
        catch (Exception ex)
        {
            LogOnce("click " + id + ex.GetType().Name, $"Mods menu entry {id} failed when picked (each kind logged once): {ex}");
        }
    }

    private bool IsShown(ModsMenuEntry entry)
    {
        if (entry.IsShown == null) return true;

        try
        {
            return entry.IsShown();
        }
        catch (Exception ex)
        {
            LogOnce("shown " + entry.Id + ex.GetType().Name, $"Mods menu entry {entry.Id} could not say whether to show, so it is hidden (each kind logged once): {ex}");

            return false;
        }
    }

    private void LogOnce(string key, string message)
    {
        if (_reportedFailures.Count >= MaxReportedFailures || !_reportedFailures.Add(key)) return;

        _log(message);
    }
}
