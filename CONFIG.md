# Exiled's Toolkit settings

Settings live in `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Delete the file to get the defaults back.

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `100` | How big mod windows, panels and toasts are drawn, in percent, from 75 to 200. A change shows at once in every mod that supports it. A window that would no longer fit on the screen is drawn as big as still fits, but never smaller than at 100. A mod with a UI size setting of its own uses that one when it is set. |
| `HudButtonSize` | `80` | How big the column of tool buttons on the left of the HUD is drawn, in percent of the game's size, from 50 to 150. It covers the buttons mods add and the game's own phone and bag. 100 is the game's size. |

`UiScale` changes only what mods draw. The game's own screens keep their size.

## Toasts

| Setting | Default | What it does |
| --- | --- | --- |
| `Position` | `Hud` | Where toasts appear. `Hud`: to the right of the phone and tool buttons, above the action queue. `TopRight`, `TopLeft`, `BottomRight` or `BottomLeft`: that corner of the screen. |
| `Seconds` | `7` | How long a toast stays on screen, from 1 to 120 seconds. A mod can pick its own time for its toasts. |
| `MaxVisible` | `4` | The most toasts on screen at once, from 1 to 4. Further toasts wait and appear as earlier ones close. If more than 30 are waiting, the oldest waiting ones are never shown, though mods that keep a list of toasts still get them. |
| `Enabled` | `true` | Show toasts on screen. When `false`, nothing is drawn, but mods that keep a list of toasts (such as a notification history) still get every one. |

Toasts lie on top of each other, newest in front. A toast stays while the pointer rests on it, for up to about 15 seconds, then leaves as normal; resting on one of the toasts behind brings it to the front.

Toasts hide for a few seconds while the pointer is on the tool buttons or the speed buttons (with `Hud`). Their time keeps running while hidden. Over an open window, such as a bench, they stay in sight on top of it and every click goes through them to the window.

Values outside a range are moved to the nearest end of it.
