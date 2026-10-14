# Exiled's Toolkit

A shared library for my Survival Log mods. On its own it does nothing you can see. Install it when another mod says it needs it.

Right now the Toolkit gives mods one thing: toasts. A toast is a short notice that slides into a corner of the screen, stays a few seconds, and goes away by itself. Mods that use the Toolkit show their messages this way instead of each drawing their own.

## Features

- **Toasts in a corner**: notices stack in the corner you choose, newest on top. Each one has a coloured edge that says what kind it is (info, success, warning or error).
- **Easy to read**: bigger text and a darker box than the game's own notices, in the same style and font, and they stay a little longer (7 seconds by default).
- **Never in the way**: toasts never take keyboard focus. Only the boxes themselves catch the mouse; the rest of the screen plays as normal.
- **Hover to keep one**: while the pointer rests on a toast, it stays. Move away and it leaves after a moment.
- **Click to open**: when a mod gives a toast something to open, clicking the toast opens it. The small x closes a toast without opening anything.
- **Repeats merge**: the same notice sent again while the first is still showing does not add another box. The first one counts up instead (x2, x3, ...) and its timer starts over.
- **No pile-ups**: at most 4 toasts show at once by default. The rest wait their turn and appear as earlier ones close. If more than 30 are waiting, the oldest waiting ones are never shown, though mods that keep a list of toasts still get them.
- **Survives reloads**: if the game's web interface reloads, the toasts on screen come back with the time they had left.

The Toolkit adds nothing to your save and does not edit game files.

## Known issues and limits

- While a toast is on screen, it covers whatever is under it. A game button under a toast cannot be clicked until the toast goes. Move the toasts to another corner with `Position` if one is in your way.
- If the game does not report the pointer leaving a toast, a hovered toast still leaves on its own after about 15 seconds.
- If something looks wrong, please tell me (see [Feedback](#feedback-and-bug-reports)).

## Requirements

- Survival Log 1.1.18317. Built and tested on this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Put `ExiledToolkit.dll` in the `BepInEx\plugins` folder inside the game folder (the folder with the game .exe). Full path example:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExiledToolkit.dll`

## Uninstall

Remove every mod that needs the Toolkit first; they will not load without it. Then delete `ExiledToolkit.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg` and `BepInEx\exiled-toolkit.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg`, created the first time the game starts with the mod. Edit it while the game is closed; changes apply at the next game start. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Tested on Survival Log 1.1.18317 with BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for `===== Exiled's Toolkit 0.1.0 started`. The line after it tells you whether toasts are shown, which corner they use, how long they stay and how many show at once. Once the game's interface is up, `toast script sent to the web UI` means toasts can appear. The same lines are in `BepInEx\exiled-toolkit.log`, which survives restarts.

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
