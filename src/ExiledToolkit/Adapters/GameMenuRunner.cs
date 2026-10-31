using System;
using System.Collections.Generic;
using ExiledToolkit.Core;

namespace ExiledToolkit.Adapters;

/// <summary>
/// Drives the pause menu and title screen buttons once a frame: keeps gamemenu.js installed, sends the button list when
/// it changes, and runs the button the player clicked.
/// </summary>
internal sealed class GameMenuRunner
{
    private const float SyncSeconds = 0.25f;
    private const int MaxReportedFailures = 20;

    private readonly WebPage _page;
    private readonly PageMailbox _mailbox = PageInbox.Listen(GameMenuCommand.Prefix);
    private readonly Action<string> _log;
    private readonly HashSet<string> _reportedFailures = new();
    private float _nextSyncAt;

    public GameMenuRunner(WebPage page, Action<string> log)
    {
        _page = page;
        _log = log;
    }

    public void Tick(float now)
    {
        _page.KeepInstalled(now);

        while (_mailbox.TryTake(out var message)) Handle(GameMenuCommand.Parse(message));

        if (now < _nextSyncAt) return;

        _nextSyncAt = now + SyncSeconds;
        Sync();
    }

    private void Sync()
    {
        var script = GameMenu.Pending(GameLanguage.IsChinese(), IsShown);

        if (script != null && _page.Send(script)) GameMenu.Shown(script);
    }

    private void Handle(GameMenuCommand command)
    {
        switch (command)
        {
            case GameMenuCommand.Ready:
                GameMenu.PageReset();
                _nextSyncAt = 0;
                break;
            case GameMenuCommand.NoScript:
                GameMenu.PageReset();
                _page.Install();
                break;
            case GameMenuCommand.Run run:
                Click(run.Id);
                break;
            case GameMenuCommand.Diagnostic diagnostic:
                LogOnce("diag " + diagnostic.Text, "game menu page: " + diagnostic.Text);
                break;
            case GameMenuCommand.Unknown unknown:
                LogOnce("unknown " + unknown.Text, "unknown message from the game menu page: " + unknown.Text);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }

    private void Click(string id)
    {
        var button = GameMenu.Find(id);

        if (button == null) return;

        try
        {
            button.OnClick();
        }
        catch (Exception ex)
        {
            LogOnce("click " + id + ex.GetType().Name, $"menu button {id} failed when clicked (each kind logged once): {ex}");
        }
    }

    private bool IsShown(GameMenuButton button)
    {
        if (button.IsShown == null) return true;

        try
        {
            return button.IsShown();
        }
        catch (Exception ex)
        {
            LogOnce("shown " + button.Id + ex.GetType().Name, $"menu button {button.Id} could not say whether to show, so it is hidden (each kind logged once): {ex}");

            return false;
        }
    }

    private void LogOnce(string key, string message)
    {
        if (_reportedFailures.Count >= MaxReportedFailures || !_reportedFailures.Add(key)) return;

        _log(message);
    }
}
