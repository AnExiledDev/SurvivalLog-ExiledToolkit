using System;
using System.Diagnostics;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ExiledToolkit.Adapters;
using ExiledToolkit.Core;
using ExiledToolkit.Game;
using ExiledToolkit.Web;
using HarmonyLib;

namespace ExiledToolkit;

[BepInPlugin(Guid, "Exiled's Toolkit", Version)]
public sealed class Plugin : BasePlugin
{
    /// <summary>For <c>[BepInDependency(ExiledToolkit.Plugin.Guid)]</c> in a mod that shows toasts.</summary>
    public const string Guid = "com.dev.survivallog.exiledtoolkit";
    public const string Version = "0.21.0";

    public override void Load()
    {
        var entries = BindToastSettings();
        var uiScale = BindUiScale();
        var buttonSize = BindButtonSize();
        var look = BindLook();
        var log = new ToolkitLog(Log, Path.Combine(Paths.BepInExRootPath, "exiled-toolkit.log"));
        var harmony = new Harmony(Guid);
        var clock = Stopwatch.StartNew();
        var center = new ToastCenter(entries.Read(), new ToastClock(() => clock.ElapsedMilliseconds, () => DateTime.UtcNow), ToastTicker.IsMainThread, log.Write);

        log.Write($"===== Exiled's Toolkit {Version} started");
        WebPage.DefaultRunner = WebUi.TryRun;
        WebPage.DefaultIsChinese = GameLanguage.IsChinese;
        PageInbox.Apply(harmony, log.Write);
        ClickRouting.Apply(harmony, log.Write);
        HotkeyGate.Apply(harmony, log.Write);
        TypingGate.Apply(harmony, log.Write);
        WorldRules.Apply(harmony, log.Write);

        Toasts.Center = center;
        ToastTicker.Page = new WebPage(log.Write, "ExiledToolkit.toasts.js", ToastScript.Probe, "toast");
        ToastTicker.Dock = new DockPage(new WebPage(log.Write, "ExiledToolkit.dock.js", "", "HUD dock"));
        ToastTicker.Phone = new WebPage(log.Write, "ExiledToolkit.phone.js", "", "phone apps");
        ToastTicker.Ui = new UiSizePage(new WebPage(log.Write, "ExiledToolkit.ui.js", "", "UI size"));
        ToastTicker.Typing = new WebPage(log.Write, "ExiledToolkit.typing.js", "", "typing watch");
        ToastTicker.Api = new WebPage(log.Write, "ExiledToolkit.api.js", "", "page API");
        ToastTicker.Menu = new MenuRunner(new WebPage(log.Write, "ExiledToolkit.menu.js", MenuScript.Probe, "Mods menu"), log.Write);
        ToastTicker.GameMenu = new GameMenuRunner(new WebPage(log.Write, "ExiledToolkit.gamemenu.js", GameMenuScript.Probe, "game menu buttons"), log.Write);
        ToastTicker.Log = log.Write;

        AddComponent<ToastTicker>();
        LiveSettings.Watch(() => ApplyToastSettings(center, entries.Read(), log.Write), entries.All);
        LiveSettings.Watch(uiScale, percent => ApplyUiScale(percent, log.Write));
        LiveSettings.Watch(buttonSize, percent => ApplyButtonSize(percent, log.Write));
        LiveSettings.Watch(() => ApplyLook(look, log.Write), look.Style, look.IconColours, look.Extras);
    }

    private static void ApplyLook(LookEntries look, Action<string> log)
    {
        var style = HudStyle.KnownStyle(look.Style.Value);
        var iconColours = HudStyle.KnownIconColours(look.IconColours.Value);

        ToastTicker.Dock?.SetLook(style, iconColours, look.Extras.Value);
        log($"HUD buttons in the {style} style, icon colours {iconColours}, extras {look.Extras.Value}");
    }

    private static void ApplyButtonSize(int percent, Action<string> log)
    {
        ToastTicker.Dock?.Set(percent);
        log($"HUD buttons drawn at {ButtonSize.Clamp(percent)}%");
    }

    private static void ApplyUiScale(int percent, Action<string> log)
    {
        ToastTicker.Ui?.Set(percent);
        log($"mod windows drawn at {UiSize.Clamp(percent)}%");
    }

    private ConfigEntry<int> BindButtonSize() =>
        Config.Bind("Interface", "HudButtonSize", ButtonSize.Default, new ConfigDescription(
            "How big the tool buttons on the left of the HUD are, in percent, from " + ButtonSize.Min + " to " + ButtonSize.Max + ". This covers the buttons mods add there and the game's phone and bag buttons. 100 is the normal size. Changes show right away. Default: " + ButtonSize.Default + ".\n" +
            "HUD 左侧工具按钮的大小（百分比），范围 " + ButtonSize.Min + " 到 " + ButtonSize.Max + "。包括模组加在那里的按钮，以及游戏的手机和背包按钮。100 为正常大小。改动会立即生效。默认 " + ButtonSize.Default + "。",
            new AcceptableValueRange<int>(ButtonSize.Min, ButtonSize.Max)));

    private sealed record LookEntries(ConfigEntry<string> Style, ConfigEntry<string> IconColours, ConfigEntry<bool> Extras);

    private LookEntries BindLook() => new(
        Config.Bind("Interface", "HudButtonStyle", HudStyle.DefaultStyle, new ConfigDescription(
            HudStyle.StyleDescription, new AcceptableValueList<string>(HudStyle.Styles), HudStyle.StyleTags())),
        Config.Bind("Interface", "HudIconColours", HudStyle.DefaultIconColours, new ConfigDescription(
            HudStyle.IconColoursDescription, new AcceptableValueList<string>(HudStyle.IconColourChoices), HudStyle.IconColourTags)),
        Config.Bind("Interface", "HudButtonExtras", HudStyle.DefaultExtras, HudStyle.ExtrasDescription));

    private ConfigEntry<int> BindUiScale() =>
        Config.Bind("Interface", "UiScale", UiSize.Default, new ConfigDescription(
            "How big mod windows, panels and toasts are, in percent, from " + UiSize.Min + " to " + UiSize.Max + ". Changes show right away in every mod that supports this setting. A window only grows as far as it still fits on the screen, so a big window such as the Mods screen barely gets bigger past about 110. A mod with its own UI size setting uses that setting instead, unless it is set to 0. Default: " + UiSize.Default + ".\n" +
            "模组的窗口、面板和通知的大小（百分比），范围 " + UiSize.Min + " 到 " + UiSize.Max + "。改动会立即在所有支持此设置的模组中生效。窗口只会放大到屏幕还放得下为止，所以像「模组」界面这样的大窗口超过 110 左右后几乎不会再变大。有自己界面大小设置的模组会改用那项设置，除非那项设为 0。默认 " + UiSize.Default + "。",
            new AcceptableValueRange<int>(UiSize.Min, UiSize.Max)));

    private static void ApplyToastSettings(ToastCenter center, ToastSettings settings, Action<string> log)
    {
        center.Configure(settings);
        log($"toasts shown {settings.IsEnabled}, position {settings.Position}, {settings.DefaultSeconds} s, up to {settings.MaxVisible} at once");
    }

    private ToastEntries BindToastSettings()
    {
        var position = Config.Bind("Toasts", "Position", ToastSettings.DefaultPosition,
            "Where toasts appear. Toasts are the short notices mods show for a few seconds. Hud: to the right of the phone and tool buttons, above the action queue. TopRight, TopLeft, BottomRight or BottomLeft: that corner of the screen. Default: Hud.\n" +
            "通知出现的位置。通知是模组显示几秒钟的简短提示。Hud：手机和工具按钮的右侧、行动队列的上方。TopRight（右上）、TopLeft（左上）、BottomRight（右下）或 BottomLeft（左下）：屏幕的对应角落。默认 Hud。");
        var seconds = Config.Bind("Toasts", "Seconds", 7f,
            "How many seconds a toast stays on screen, from 1 to 120. A mod can pick a different time for its toasts. While the pointer rests on a toast, its time stops for up to 15 seconds. Default: 7.\n" +
            "通知在屏幕上停留的秒数，范围 1 到 120。模组可以为自己的通知设定别的时长。鼠标停在通知上时，它的计时会暂停，最多 15 秒。默认 7。");
        var maxVisible = Config.Bind("Toasts", "MaxVisible", 4,
            "The most toasts on screen at once, from 1 to 4. Extra toasts wait and show up as earlier ones close. If more than " + ToastBoard.MaxWaiting + " are waiting, the oldest ones are skipped and never show, but mods that keep a toast history still get them. Default: 4.\n" +
            "屏幕上最多同时显示几条通知，范围 1 到 4。多出的通知会排队，等前面的通知关闭后再出现。排队超过 " + ToastBoard.MaxWaiting + " 条时，最早排队的通知会被跳过，不再显示，但记录通知历史的模组仍会收到它们。默认 4。");
        var isEnabled = Config.Bind("Toasts", "Enabled", true,
            "Show toasts on screen. When off, no toasts show, but mods that keep a toast history still get every one. Default: on.\n" +
            "在屏幕上显示通知。关闭后不再显示通知，但记录通知历史的模组仍会收到每一条。默认开启。");

        return new ToastEntries(position, seconds, maxVisible, isEnabled);
    }

    /// <summary>The four toast settings, kept as entries so a change made while the game runs can be read again.</summary>
    private sealed record ToastEntries(ConfigEntry<ToastPosition> Position, ConfigEntry<float> Seconds, ConfigEntry<int> MaxVisible, ConfigEntry<bool> IsEnabled)
    {
        public ConfigEntryBase[] All => new ConfigEntryBase[] { Position, Seconds, MaxVisible, IsEnabled };

        public ToastSettings Read() =>
            new(ToastSettings.KnownPosition(Position.Value), Math.Clamp(Seconds.Value, ToastSettings.MinSeconds, ToastSettings.MaxSeconds), Math.Clamp(MaxVisible.Value, 1, ToastSettings.MaxMaxVisible), IsEnabled.Value);
    }
}
