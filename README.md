# Exiled's Toolkit

A shared library for my Survival Log mods. On its own it does nothing you can see. Install it when another mod says it needs it.

Right now the Toolkit gives mods two things. The first is toasts. A toast is a short notice that appears next to the phone, defense and other tool buttons, stays a few seconds, and goes away by itself. Mods that use the Toolkit show their messages this way instead of each drawing their own. The second is live settings: mods can apply a changed setting while you play, so a change in the Mod Manager works without restarting the game.

## Features

- **Beside the HUD**: toasts sit to the right of the phone, defense and other tool buttons, above the action queue, and follow them if the screen size changes. You can put them in a screen corner instead. Each one has a thin coloured edge that says what kind it is (info, success, warning or error).
- **Small and tidy**: compact boxes in the game's colours and font, with long text cut to two lines. They stay 7 seconds by default.
- **Layered**: several toasts lie on top of each other, newest in front, with the older ones peeking out behind, so they take about one toast's room. Rest the pointer on one behind to bring it to the front with more of its text (up to six lines).
- **Never in the way**: toasts never take keyboard focus. Only the boxes themselves catch the mouse; the rest of the screen plays as normal. They hide for a few seconds while the pointer is on the tool buttons or the speed buttons, so those buttons' tips can be read, and while an open window takes the whole screen or has a button where they would be.
- **Hover to keep one**: while the pointer rests on a toast, it stays. Move away and it leaves after a moment.
- **Click to open**: when a mod gives a toast something to open, clicking the toast opens it. The small x closes a toast without opening anything.
- **Repeats merge**: the same notice sent again while the first is still showing does not add another box. The first one counts up instead (x2, x3, ...) and its timer starts over.
- **No pile-ups**: at most 4 toasts show at once by default. The rest wait their turn and appear as earlier ones close. If more than 30 are waiting, the oldest waiting ones are never shown, though mods that keep a list of toasts still get them.
- **Survives reloads**: if the game's web interface reloads, the toasts on screen come back with the time they had left.

The Toolkit adds nothing to your save and does not edit game files.

## Known issues and limits

- A toast covers whatever is under it. Next to the HUD that is empty space, and toasts hide for open panels, but a game button under a toast cannot be clicked until the toast goes. Move the toasts with `Position` if they are in your way.
- Toasts that hide for an open panel keep counting down, so a short one can run out before the panel closes. Mods that keep a list of toasts still get it.

## Requirements

- Survival Log 1.1.18902. Built and tested on this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Download `ExiledToolkit-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from Nexus Mods.
3. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExiledToolkit.dll`

## Uninstall

Remove every mod that needs the Toolkit first; they will not load without it. Then delete `ExiledToolkit.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg` and `BepInEx\exiled-toolkit.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg`, created the first time the game starts with the mod. Edit it while the game is closed; changes apply at the next game start. Each setting in the cfg file is explained in English and Simplified Chinese. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Tested on Survival Log 1.1.18902 with BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for `===== Exiled's Toolkit 0.3.0 started`. The rest of that line tells you whether toasts are shown, where they appear, how long they stay and how many show at once. Once the game's interface is up, `toast script sent to the web UI` means toasts can appear. The same lines are in `BepInEx\exiled-toolkit.log`, which survives restarts.

If no toasts appear, check that `Enabled` is `true` in the config, then look in `BepInEx\exiled-toolkit.log` for a line starting with `could not`.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also leave a comment on the Nexus page. When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\exiled-toolkit.log`.

## For mod authors

Add a hard dependency so BepInEx loads the Toolkit first, and reference `ExiledToolkit.dll`:

```csharp
[BepInDependency("com.dev.survivallog.exiledtoolkit")]
```

Show a toast from any thread. It appears from the next frame:

```csharp
using ExiledToolkit;

Toasts.Show(new ToastRequest("Storage Hub", "Put away 12 items")
{
    Body = "Fridge: 5\nShelf: 7",
    Kind = ToastKind.Success,
    OnClick = () => OpenStoragePanel(),
});
```

`ToastRequest(Source, Title)` also takes:

| Property | What it does |
| --- | --- |
| `Body` | Optional second text. Line breaks are kept. |
| `Kind` | `Info` (default), `Success`, `Warning` or `Error`. Sets the edge colour. |
| `Icon` | Optional image path, as the game's web interface would load it. |
| `OnClick` | Runs on the game's main thread when the player clicks the toast. The toast then closes. An exception is caught and logged. |
| `Seconds` | This toast's time on screen, 1 to 120. Default: the player's `Seconds` setting. |
| `Key` | A toast with the same key replaces the one showing in place. Use it for progress ("Organizing 2/3"). |

Text is always shown as plain text, never as HTML.

At most `MaxVisible` toasts show at once and the rest wait. If more than 30 are waiting, the oldest waiting ones are dropped from the screen without ever showing. `Toasts.Raised` still fires for them, so a history keeps them.

`Toasts.Show` returns a `ToastId`. A toast identical to one still showing or waiting (same source, title, body, kind and icon) merges into it: you get the earlier toast's id, the count goes up, its timer restarts, and the newest `OnClick` replaces the old one.

`Toasts.Raised` fires on the main thread for every call, shown or not (even with `Enabled` off), with a `ToastRecord`: id, source, title, body, kind, icon, time (UTC), whether it has a click action, the count, and `IsMerge`. Use it to keep a history.

`Toasts.Activate(id)` runs a toast's click action as if it was clicked, also after it has left the screen. The Toolkit keeps the actions of the 500 most recent toasts that have one; for older toasts it returns `false`.

### Live settings

`LiveSettings` applies your settings while the game runs, so a change in the Mod Manager (or anything else that sets the value) takes effect at once instead of at the next start. Watching a setting also tells the Mod Manager that it applies live, so it does not show a restart warning for it.

```csharp
using ExiledToolkit;

var speed = Config.Bind("General", "Speed", 1.5f, "...");
LiveSettings.Watch(speed, value => Mover.Speed = value);

var width = Config.Bind("Panel", "Width", 400, "...");
var height = Config.Bind("Panel", "Height", 300, "...");
LiveSettings.Watch(() => Panel.Resize(width.Value, height.Value), width, height);
```

- The apply runs once at once, on the thread that calls `Watch` (usually from `Load`), so it can replace your first read of the value. If it touches game objects that do not exist yet, check for them in the apply.
- After that it runs on the game's main thread, at the next frame after a change, from whatever thread the change came. Several changes in one frame make one call, so "Reset all" costs one apply. With the second form, read every entry inside the apply.
- An apply that throws is logged to `BepInEx\exiled-toolkit.log` and does not stop other mods.
- `Watch` returns an `IDisposable`; dispose it to stop watching. Most mods watch for the life of the game and never dispose it.
- An apply that sets one of its own watched settings runs again at the next frame, not in a loop inside one frame.

Without the Toolkit, a setting can say how it applies with a tag the Mod Manager reads; see the Mod Manager's README, "For mod authors".

### Item settings

The Mod Manager draws item pickers for string settings tagged `ModManager.Control=ItemList` (`"1203,7"`) or `ModManager.Control=ItemCounts` (`"1203:5,7:2"`). `ItemValues` reads and writes those strings:

```csharp
using ExiledToolkit;

var blocked = Config.Bind("Loot", "Blocked", "", new ConfigDescription("...", null, "ModManager.Control=ItemList"));
var kit = Config.Bind("Start", "Kit", "1203:5,7:2", new ConfigDescription("...", null, "ModManager.Control=ItemCounts"));

LiveSettings.Watch(blocked, value => Loot.Blocked = ItemValues.ParseList(value).ToHashSet());
LiveSettings.Watch(kit, value => Start.Kit = ItemValues.ParseCounts(value));   // ItemCount(Id, Count) each

kit.Value = ItemValues.FormatCounts(new[] { new ItemCount(1203, 10) });
```

- `ParseList` gives the ids in order, each once. `ParseCounts` gives each item with its amount; an item listed twice keeps its first amount.
- Parsing never throws. An entry that is not a whole number of 0 or more, or an amount under 1, is skipped, so a .cfg edited by hand cannot break your mod.
- `FormatList` and `FormatCounts` write the plain form the Mod Manager saves, leaving out negative ids, amounts under 1 and repeats.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/ExiledToolkit -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. The output DLL is at `src\ExiledToolkit\bin\Release\ExiledToolkit.dll`.

The unit tests cover the pure toast rules and need nothing from the game:

```
dotnet test tests/ExiledToolkit.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
