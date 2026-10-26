using System;
using System.Diagnostics;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ExiledToolkit.Adapters;
using ExiledToolkit.Core;
using HarmonyLib;

namespace ExiledToolkit;

[BepInPlugin(Guid, "Exiled's Toolkit", Version)]
public sealed class Plugin : BasePlugin
{
    /// <summary>For <c>[BepInDependency(ExiledToolkit.Plugin.Guid)]</c> in a mod that shows toasts.</summary>
    public const string Guid = "com.dev.survivallog.exiledtoolkit";
    public const string Version = "0.9.0";

    public override void Load()
    {
        var entries = BindToastSettings();
        var log = new ToolkitLog(Log, Path.Combine(Paths.BepInExRootPath, "exiled-toolkit.log"));
        var harmony = new Harmony(Guid);
        var clock = Stopwatch.StartNew();
        var center = new ToastCenter(entries.Read(), new ToastClock(() => clock.ElapsedMilliseconds, () => DateTime.UtcNow), ToastTicker.IsMainThread, log.Write);

        log.Write($"===== Exiled's Toolkit {Version} started");
        PageInbox.Apply(harmony, log.Write);
        ClickRouting.Apply(harmony, log.Write);
        HotkeyGate.Apply(harmony, log.Write);

        Toasts.Center = center;
        ToastTicker.Page = new WebPage(log.Write, "ExiledToolkit.toasts.js", ToastScript.Probe, "toast");
        ToastTicker.Dock = new WebPage(log.Write, "ExiledToolkit.dock.js", "", "HUD dock");
        ToastTicker.Phone = new WebPage(log.Write, "ExiledToolkit.phone.js", "", "phone apps");
        ToastTicker.Menu = new MenuRunner(new WebPage(log.Write, "ExiledToolkit.menu.js", MenuScript.Probe, "Mods menu"), log.Write);
        ToastTicker.Log = log.Write;

        AddComponent<ToastTicker>();
        LiveSettings.Watch(() => ApplyToastSettings(center, entries.Read(), log.Write), entries.All);
    }

    private static void ApplyToastSettings(ToastCenter center, ToastSettings settings, Action<string> log)
    {
        center.Configure(settings);
        log($"toasts shown {settings.IsEnabled}, position {settings.Position}, {settings.DefaultSeconds} s, up to {settings.MaxVisible} at once");
    }

    private ToastEntries BindToastSettings()
    {
        var position = Config.Bind("Toasts", "Position", ToastSettings.DefaultPosition,
            "Where toasts appear. Hud: to the right of the phone and tool buttons, above the action queue. TopRight, TopLeft, BottomRight or BottomLeft: that corner of the screen. Toasts are the short notices mods show for a few seconds. Default: Hud.\n" +
            "通知出现的位置。Hud：手机和工具按钮的右侧、行动队列的上方。TopRight（右上）、TopLeft（左上）、BottomRight（右下）或 BottomLeft（左下）：屏幕的对应角落。通知是模组显示几秒钟的简短提示。默认 Hud。");
        var seconds = Config.Bind("Toasts", "Seconds", 7f,
            "How many seconds a toast stays on screen, from 1 to 120. A mod can choose a different time for its own toasts. A toast stays while the pointer rests on it. Default: 7.\n" +
            "通知在屏幕上停留的秒数，范围 1 到 120。模组可以为自己的通知指定别的时长。鼠标停在通知上时，通知不会消失。默认 7。");
        var maxVisible = Config.Bind("Toasts", "MaxVisible", 4,
            "The most toasts on screen at once, from 1 to 4. Further toasts wait and appear as earlier ones close. If more than " + ToastBoard.MaxWaiting + " are waiting, the oldest waiting ones are never shown, though mods that keep a list of toasts still get them. Default: 4.\n" +
            "屏幕上同时显示的通知数量上限，范围 1 到 4。多出的通知会排队，等前面的通知关闭后再出现。排队的通知超过 " + ToastBoard.MaxWaiting + " 条时，最早排队的通知不会再显示，但会记录通知的模组仍会收到它们。默认 4。");
        var isEnabled = Config.Bind("Toasts", "Enabled", true,
            "Show toasts on screen. Off: no toast is drawn, but mods that keep a list of them, such as a notification history, still get every one. Default: on.\n" +
            "在屏幕上显示通知。关闭后不再显示任何通知，但会记录通知的模组（例如通知历史）仍会收到每一条。默认开启。");

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
