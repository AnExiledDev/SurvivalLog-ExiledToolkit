# Exiled's Toolkit settings

Settings live in `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Delete the file to get the defaults back.

## Toasts

| Setting | Default | What it does |
| --- | --- | --- |
| `Position` | `Hud` | Where toasts appear. `Hud`: to the right of the phone and tool buttons, above the action queue. `TopRight`, `TopLeft`, `BottomRight` or `BottomLeft`: that corner of the screen. |
| `Seconds` | `7` | How long a toast stays on screen, from 1 to 120 seconds. A mod can pick its own time for its toasts. |
| `MaxVisible` | `4` | The most toasts on screen at once, from 1 to 4. Further toasts wait and appear as earlier ones close. If more than 30 are waiting, the oldest waiting ones are never shown, though mods that keep a list of toasts still get them. |
| `Enabled` | `true` | Show toasts on screen. When `false`, nothing is drawn, but mods that keep a list of toasts (such as a notification history) still get every one. |

Toasts lie on top of each other, newest in front. A toast stays while the pointer rests on it, for up to about 15 seconds, then leaves as normal; resting on one of the toasts behind brings it to the front.

Toasts step aside rather than cover the game: they hide for a few seconds while the pointer is on the tool buttons or the speed buttons (with `Hud`), and while an open window takes the whole screen or has a button where they would be. Their time keeps running while hidden.

Values outside a range are moved to the nearest end of it.
