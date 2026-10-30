# Changelog

## 0.13.1

- The HUD's tool buttons sit a little higher, so the bottom row no longer covers the quick actions bar above the action queue. This holds at every `HudButtonSize`.
- Made for Survival Log 1.1.20151.

## 0.13.0

- The HUD's tool buttons have a new look: the buttons mods add there and the game's own phone and bag. Pick it with `HudButtonStyle`: Lamplight (dark brown, each tool's icon in its own colour, the default), FieldKit (olive canvas with stitching, buttons press like keys), Signal (each tool lit in its own colour) or Classic (the game's own grey buttons).
- New icons for the phone, the bag and the buttons of Trap Hub, Package Bank, Field Guide, Farm Hub, Defense Manager, Machines, Power Manager and Storage Hub. Other buttons keep their own icon.
- `HudIconColours` draws every icon in one colour instead of one per tool, and `HudButtonExtras` gives Lamplight and Signal the FieldKit touches: keys that press down, a striped edge on a button that needs you, stamped badges and paper tips.
- A change to any of the three shows at once.
- Made for Survival Log 1.1.20151.

## 0.12.0

- While you type in a mod's text box, such as a search box, the keys no longer reach the game: letters no longer open panels or take a photo, and WASD no longer moves the camera. This works for every mod's boxes without the mod doing anything. Clicking the world leaves the box.
- Mods can ask the Toolkit whether you are typing, to skip their own hotkeys while you are.
- Made for Survival Log 1.1.20151.

## 0.11.3

- Toasts shown over an open window are half see-through, so the window under them stays readable. Clicks there still go through them to the window.
- Made for Survival Log 1.1.20151.

## 0.11.2

- Fixed: a mod window that redraws itself, such as a bench or storage window, could show too big for the screen after a redraw at a large UI size.
- Fixed: a window opened for the first time at a large UI size no longer flashes too big for one frame before it fits.
- Fixed: one mod handing the Toolkit a bad window could stop every other mod's windows from following the UI size.
- The UiScale description now says that large windows, such as the Mods screen, grow only a little past about 110.
- Made for Survival Log 1.1.20151.

## 0.11.1

- Toasts now show over open windows, such as a bench, instead of hiding until the window closes. Clicks there go through the toast to the window.
- Made for Survival Log 1.1.19611.

## 0.11.0

- New: the HUD button size setting (`HudButtonSize` under Interface) draws the column of tool buttons on the left of the HUD smaller or bigger, from 50% to 150%. It covers the buttons mods add and the game's own phone and bag. It starts at 80%; set it to 100 for the game's size.
- Mods can have a UI size setting of their own, which overrides `UiScale` for that mod's windows. Each mod gets it once it is updated for it.
- For mod authors: `ModUiSize.Set` and `{ mod: "<name>" }` in `ExiledUi.size`. See "UI size" in the README.
- Made for Survival Log 1.1.19611.

## 0.10.0

- New: the UI size setting (`UiScale` under Interface) draws mod windows, panels and toasts bigger or smaller, from 75% to 200%. A change shows at once, without restarting the game.
- A mod window that would run off the screen at a bigger size is drawn as big as still fits.
- Toasts follow the UI size now. Each other mod's windows follow it once that mod is updated for it.
- For mod authors: hand a panel your page script draws to `ExiledUi.size` and it follows the player's UI size, and `ExiledUi.zoomAt` gives the size a tooltip should take to match the panel under it. See "UI size" in the README.
- Made for Survival Log 1.1.19611.

## 0.9.2

- Fixed: the phone was very laggy since 0.9.1. Mod apps still go into the dock as soon as the phone opens.
- The HUD buttons, the toasts and the Mods button do much less work while you play, so the game runs more smoothly with several mods installed.
- Made for Survival Log 1.1.19611.

## 0.9.1

- Mod apps on the phone are in the dock the moment the phone opens, instead of showing up a moment later.
- Made for Survival Log 1.1.19611.

## 0.9.0

- The Mods button is now also in the bag panel of the workbench, stove, ration press, greenhouse, drone hub, brewing barrel, dye vat, rat cage, material rack and shop screens, not just the inventory. It lists the actions that work there. The shredder and the drone trade are left out.
- For mod authors: page-script entries in the shared menu can ask to be listed on those screens too (`stations: true`). Entries added with `ModsMenu` stay on the inventory.
- Made for Survival Log 1.1.19611.

## 0.8.0

- New: mod apps on the phone go into the dock at the bottom, after the game's own apps, instead of stacking in the middle of the home screen over the notifications. Past four apps the dock shows rows of four.
- For mod authors: mark a phone app with `data-etk-phone="<order>"` to have it placed in the dock.
- Made for Survival Log 1.1.19131; works on 1.1.19611.

## 0.7.0

- New look for every mod's toasts, in the gold style of the newer mod windows: warm brown cards, a coloured edge for the kind of notice, a gold count badge, and a thin bar along the bottom that shows how long the toast has left. The bar stops while the pointer rests on the toast.
- For mod authors: `ToastRequest.Items` lists the items a toast is about. They show as small chips with picture, name and amount on the toast the pointer rests on, and as one line of text on the cards behind it. Two toasts merge only when their items match too.
- Made for Survival Log 1.1.19131.

## 0.6.0

- New: the HUD's tool buttons stay four high. When mods add enough buttons that the column on the left would grow past four, the rest go into a second column to its right, filled top to bottom. The phone and bag stay in place, and toasts move right to clear the wider column.
- For mod authors: mark a HUD button with `data-etk-dock="<order>"` to set its place in the layout.
- After a game update, Mod Manager can tell you if this mod may need an update.
- Made for Survival Log 1.1.19131.

## 0.5.0

- New: the inventory's "Mods" dropdown. Mods that need it, such as Loot All Pre-Disaster, add their entries to it through the Toolkit. It shares the one button with Storage Hub's and Unpack All's own entries.
- For mod authors: `ModsMenu.Add` puts an entry in the dropdown, with English and Chinese text, an icon, and a rule for when it shows.
- Toast settings apply at once when changed in the Mod Manager: the position, the time on screen, how many show at once, and the off switch.
- The Mod Manager shows the Toolkit with its name in Chinese, a description and its GitHub page, in the Libraries group.

## 0.4.0

- For mod authors: `LiveSettings.Watch` applies a mod's settings while the game runs, once per frame on the main thread, and tells the Mod Manager the setting needs no restart. The Mod Manager 0.4.0 needs this version.
- For mod authors: `ItemValues` reads and writes the item list and item amount strings behind the Mod Manager's item pickers.

## 0.3.0

- No visible change. Mod Manager needs this version.

## 0.2.2

- First release.
- A shared library for my Survival Log mods.
- It shows their short notices (toasts) beside the HUD.
- On its own it does nothing you can see. Install it when another mod says it needs it.
