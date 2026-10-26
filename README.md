# Exiled's Toolkit

A shared library for my Survival Log mods. On its own it does nothing you can see. Install it when another mod says it needs it.

Right now the Toolkit gives mods five things. The first is toasts. A toast is a short notice that appears next to the phone, defense and other tool buttons, stays a few seconds, and goes away by itself. Mods that use the Toolkit show their messages this way instead of each drawing their own. The second is live settings: mods can apply a changed setting while you play, so a change in the Mod Manager works without restarting the game. The third is the inventory's "Mods" dropdown, one button in the inventory toolbar that holds the actions mods add there. The fourth keeps the HUD's tool buttons tidy: once mods have added enough buttons that the column would run past four, they are laid out in columns of four instead. The fifth tidies the phone: apps that mods add go into the dock at the bottom, after the game's own apps.

## Features

- **Beside the HUD**: toasts sit to the right of the phone, defense and other tool buttons, above the action queue, and follow them if the screen size changes. You can put them in a screen corner instead. Each one has a thin coloured edge that says what kind it is (info, success, warning or error).
- **Small and tidy**: compact boxes in the game's colours and font, with long text cut to two lines. They stay 7 seconds by default.
- **Layered**: several toasts lie on top of each other, newest in front, with the older ones peeking out behind, so they take about one toast's room. Rest the pointer on one behind to bring it to the front with more of its text (up to six lines).
- **Never in the way**: toasts never take keyboard focus. Only the boxes themselves catch the mouse; the rest of the screen plays as normal. They hide for a few seconds while the pointer is on the tool buttons or the speed buttons, so those buttons' tips can be read, and while an open window takes the whole screen or has a button where they would be.
- **Hover to keep one**: while the pointer rests on a toast, it stays. Move away and it leaves after a moment.
- **Click to open**: when a mod gives a toast something to open, clicking the toast opens it. The small x closes a toast without opening anything.
- **Repeats merge**: the same notice sent again while the first is still showing does not add another box. The first one counts up instead (x2, x3, ...) and its timer starts over.
- **No pile-ups**: at most 4 toasts show at once by default. The rest wait their turn and appear as earlier ones close. If more than 30 are waiting, the oldest waiting ones are never shown, though mods that keep a list of toasts still get them.
- **Tidy tool buttons**: the column of buttons on the left of the HUD (the phone, the bag and the buttons mods add there) never gets taller than four buttons. The rest go into a second column to its right, top to bottom, and a third after that. The phone and bag stay where they always are, and toasts move right to clear the extra column.
- **Tidy phone apps**: apps that mods add to the phone go into the dock at the bottom, after the game's own, in rows of four, instead of stacking in the middle of the home screen.
- **Survives reloads**: if the game's web interface reloads, the toasts on screen come back with the time they had left.

The Toolkit adds nothing to your save and does not edit game files.

## Known issues and limits

- A toast covers whatever is under it. Next to the HUD that is empty space, and toasts hide for open panels, but a game button under a toast cannot be clicked until the toast goes. Move the toasts with `Position` if they are in your way.
- Toasts that hide for an open panel keep counting down, so a short one can run out before the panel closes. Mods that keep a list of toasts still get it.

## Requirements

- Survival Log 1.1.19611. Built and tested on this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Download `ExiledToolkit-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/61).
3. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExiledToolkit.dll`

## Uninstall

Remove every mod that needs the Toolkit first; they will not load without it. Then delete `ExiledToolkit.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg` and `BepInEx\exiled-toolkit.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Each setting in the cfg file is explained in English and Simplified Chinese. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Tested on Survival Log 1.1.19611 with BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for `===== Exiled's Toolkit 0.8.0 started`. A few lines below it, `toasts shown ...` tells you whether toasts are shown, where they appear, how long they stay and how many show at once; it is written again whenever one of those settings changes. Once the game's interface is up, `toast script sent to the web UI` means toasts can appear. The same lines are in `BepInEx\exiled-toolkit.log`, which survives restarts.

If no toasts appear, check that `Enabled` is `true` in the config, then look in `BepInEx\exiled-toolkit.log` for a line starting with `could not`.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also leave a comment on the Nexus page. When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\exiled-toolkit.log`.

To reach me directly, message `exiled.dev` on Discord; you can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

A Survival Log wiki with detailed documentation for all of my mods is coming soon.

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
| `Items` | Optional list of `ToastItem(Label) { Icon, Count }`, shown as small chips (picture, name, amount) while the player hovers the toast. At most 12 show; items without a label are skipped. |

Text is always shown as plain text, never as HTML.

At most `MaxVisible` toasts show at once and the rest wait. If more than 30 are waiting, the oldest waiting ones are dropped from the screen without ever showing. `Toasts.Raised` still fires for them, so a history keeps them.

`Toasts.Show` returns a `ToastId`. A toast identical to one still showing or waiting (same source, title, body, kind, icon and items) merges into it: you get the earlier toast's id, the count goes up, its timer restarts, and the newest `OnClick` replaces the old one.

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

### Mods menu

`ModsMenu` puts an entry in the inventory toolbar's "Mods" dropdown, between the game's discard and sort buttons. Every mod's entries share the one button, and the button only shows while at least one entry wants to be listed. The same button also sits in the bag panel of the workbench, stove, ration press, greenhouse, drone hub, brewing barrel, dye vat, rat cage, material rack and shop screens (not the shredder or the drone trade), but it lists only page-script entries added with `stations: true`; `ModsMenu` entries stay on the inventory.

```csharp
using ExiledToolkit;

ModsMenu.Add(new ModsMenuEntry("mymod.sortall", "Sort all", SortAll)
{
    ChineseLabel = "全部整理",
    Title = "Sort every item in your bag",
    ChineseTitle = "整理背包里的所有物品",
    Icon = "M4 6h16v2H4zM4 11h12v2H4zM4 16h8v2H4z",
    Order = 300,
    IsShown = () => IsBeforeDisaster(),
});
```

- The id is unique across mods: 1 to 64 letters, digits, `.`, `_` or `-`. Adding an entry with an id already in use replaces it; `ModsMenu.Remove(id)` takes it out.
- The label and tooltip show in the game's language; empty Chinese text shows the English. `Icon` is the `d` of an SVG path on a 24-unit grid.
- Rows sort by `Order`, then id. Storage Hub uses the 100s and Unpack All 200.
- `IsShown` is asked on the main thread a few times a second; leave it out to always list the entry. One that throws counts as hidden and is logged once. The dropdown itself shows only on the inventory's item tab.
- The click action runs on the main thread after the menu has closed. One that throws is logged to `BepInEx\exiled-toolkit.log`.
- While the menu is open the game's hotkeys are held back, and Esc closes the menu.

### HUD buttons

A mod that adds a button to the HUD's tool column (`.vertical-tools` in CoreUI1 or CoreUI0) marks it with `data-etk-dock="<order>"`. Past four shown buttons the Toolkit lays the column out four high: the last four stay in the first column, and the rest fill columns to the right, top to bottom. Marked buttons come first, lowest order first, then the game's own buttons and unmarked ones in page order. Trap Hub uses 10, Package Bank 20, Farm Hub 30 and Defense Manager 40.

Nothing else is needed, and the mark does nothing without the Toolkit, so a mod can keep the Toolkit optional.

### Phone apps

A mod that adds an app to the phone's home screen (PhoneSMS.html) builds it like the game's own (`.app` with `.app-icon` and `.app-label`), puts it in `.app-grid`, and marks it with `data-etk-phone="<order>"`. The Toolkit moves marked apps into `.app-dock` after the game's apps, lowest order first, and past four shown apps lays the dock out in rows of four. Without the Toolkit the app stays in `.app-grid`. Look the app up by its class anywhere in the document, not only in `.app-grid`, or it is added again once moved. Field Guide uses 10 and Neighbor Basket 20.

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
