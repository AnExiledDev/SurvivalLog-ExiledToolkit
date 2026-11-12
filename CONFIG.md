# Exiled's Toolkit settings

Settings live in `BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect right away, or edit the file while the game is closed. Delete the file to get the defaults back.

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `100` | How big mod windows, panels and toasts are, in percent, from 75 to 200. Changes show right away in every mod that supports this setting. A window only grows as far as it still fits on the screen, and never gets smaller than it is at 100. That means a big window such as the Mods screen barely gets bigger past about 110. A mod with its own UI size setting uses that setting instead, unless it is set to 0. |
| `HudButtonSize` | `80` | How big the tool buttons on the left of the HUD are, in percent, from 50 to 150. This covers the buttons mods add there and the game's phone and bag buttons. 100 is the normal size. |
| `HudButtonStyle` | `Lamplight` | How the tool buttons on the left of the HUD look, including the buttons mods add there and the game's phone and bag buttons. `Lamplight`: dark brown buttons with each tool's icon in its own colour. `FieldKit`: olive canvas buttons with stitching that press down like keys. `Signal`: dark buttons with each tool's icon lit up in its own colour. `Classic`: the game's normal grey buttons and icons. |
| `HudIconColours` | `PerTool` | The colour of the icons on the tool buttons. `PerTool` gives each tool's icon its own colour. `One` draws every icon amber in Lamplight and bone white in FieldKit. Signal always gives each tool its own colour, and Classic always shows the game's normal icons. |
| `HudButtonExtras` | `true` | Gives Lamplight and Signal the extra details of the FieldKit style: buttons that press down like keys, a moving striped bar on a button with something new, stamped number badges and tooltips that look like paper tags. FieldKit always has them and Classic never does. |

`UiScale` only changes what mods draw. The game's screens stay the same size.

## Toasts

Toasts are the short notices mods show for a few seconds.

| Setting | Default | What it does |
| --- | --- | --- |
| `Position` | `Hud` | Where toasts appear. `Hud`: to the right of the phone and tool buttons, above the action queue. `TopRight`, `TopLeft`, `BottomRight` or `BottomLeft`: that corner of the screen. |
| `Seconds` | `7` | How long a toast stays on screen, from 1 to 120 seconds. A mod can pick a different time for its toasts. |
| `MaxVisible` | `4` | The most toasts on screen at once, from 1 to 4. Extra toasts wait and show up as earlier ones close. If more than 30 are waiting, the oldest ones are skipped and never show, but mods that keep a toast history still get them. |
| `Enabled` | `true` | Show toasts on screen. When `false`, no toasts show, but mods that keep a toast history still get every one. |

New toasts sit on top of older ones, with the newest in front. While the pointer rests on a toast, its time stops for up to 15 seconds, then it carries on and leaves as usual. Resting the pointer on one of the toasts behind brings it to the front.

With `Hud`, toasts hide while the pointer is on the tool buttons or the speed buttons, so you can read those buttons' tips. Their time keeps running while they are hidden. Over an open window, such as a bench, toasts stay on top of it, and every click goes through them to the window.

A value outside a setting's range is moved to the nearest end of it.
