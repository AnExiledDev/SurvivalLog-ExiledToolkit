# Exiled's Toolkit settings

Settings live in `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg`, created the first time the game starts with the mod. Edit the file while the game is closed; changes apply at the next game start. Delete the file to get the defaults back.

## Toasts

| Setting | Default | What it does |
| --- | --- | --- |
| `Position` | `TopRight` | The corner toasts appear in: `TopRight`, `TopLeft`, `BottomRight` or `BottomLeft`. |
| `Seconds` | `7` | How long a toast stays on screen, from 1 to 120 seconds. A mod can pick its own time for its toasts. |
| `MaxVisible` | `4` | The most toasts on screen at once, from 1 to 10. Further toasts wait and appear as earlier ones close. |
| `Enabled` | `true` | Show toasts on screen. When `false`, nothing is drawn, but mods that keep a list of toasts (such as a notification history) still get every one. |

A toast stays while the pointer rests on it, for up to about 15 seconds, then leaves as normal.

Values outside a range are moved to the nearest end of it.
