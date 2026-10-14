using System;
using System.Diagnostics;
using System.IO;
using BepInEx;
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
    public const string Version = "0.1.0";

    public override void Load()
    {
        var settings = BindToastSettings();
        var log = new ToolkitLog(Log, Path.Combine(Paths.BepInExRootPath, "exiled-toolkit.log"));
        var harmony = new Harmony(Guid);
        var clock = Stopwatch.StartNew();

        log.Write($"===== Exiled's Toolkit {Version} started, toasts shown {settings.IsEnabled}, corner {settings.Position}, {settings.DefaultSeconds} s, up to {settings.MaxVisible} at once");
        PageMessages.Apply(harmony, log.Write);
        ClickRouting.Apply(harmony, log.Write);
        Toasts.Center = new ToastCenter(settings, new ToastClock(() => clock.ElapsedMilliseconds, () => DateTime.UtcNow), ToastTicker.IsMainThread, log.Write);
        ToastTicker.Page = new WebPage(log.Write);
        ToastTicker.Log = log.Write;
        AddComponent<ToastTicker>();
    }

    private ToastSettings BindToastSettings()
    {
        var position = Config.Bind("Toasts", "Position", ToastPosition.TopRight,
            "Which corner of the screen toasts appear in: TopRight, TopLeft, BottomRight or BottomLeft. Toasts are the short notices mods show for a few seconds. Default: TopRight.\n" +
            "通知出现在屏幕的哪个角：TopRight（右上）、TopLeft（左上）、BottomRight（右下）或 BottomLeft（左下）。通知是模组显示几秒钟的简短提示。默认 TopRight。").Value;
        var seconds = Config.Bind("Toasts", "Seconds", 7f,
            "How many seconds a toast stays on screen, from 1 to 120. A mod can choose a different time for its own toasts. A toast stays while the pointer rests on it. Default: 7.\n" +
            "通知在屏幕上停留的秒数，范围 1 到 120。模组可以为自己的通知指定别的时长。鼠标停在通知上时，通知不会消失。默认 7。").Value;
        var maxVisible = Config.Bind("Toasts", "MaxVisible", 4,
            "The most toasts on screen at once, from 1 to 10. Further toasts wait and appear as earlier ones close. If more than " + ToastBoard.MaxWaiting + " are waiting, the oldest waiting ones are never shown, though mods that keep a list of toasts still get them. Default: 4.\n" +
            "屏幕上同时显示的通知数量上限，范围 1 到 10。多出的通知会排队，等前面的通知关闭后再出现。排队的通知超过 " + ToastBoard.MaxWaiting + " 条时，最早排队的通知不会再显示，但会记录通知的模组仍会收到它们。默认 4。").Value;
        var isEnabled = Config.Bind("Toasts", "Enabled", true,
            "Show toasts on screen. Off: no toast is drawn, but mods that keep a list of them, such as a notification history, still get every one. Default: on.\n" +
            "在屏幕上显示通知。关闭后不再显示任何通知，但会记录通知的模组（例如通知历史）仍会收到每一条。默认开启。").Value;

        // The file can hold a number for the corner; one that names no corner falls back to the default.
        if (!Enum.IsDefined(typeof(ToastPosition), position)) position = ToastPosition.TopRight;

        return new ToastSettings(position, Math.Clamp(seconds, ToastSettings.MinSeconds, ToastSettings.MaxSeconds), Math.Clamp(maxVisible, 1, ToastSettings.MaxMaxVisible), isEnabled);
    }
}
