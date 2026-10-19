# Changelog

## 0.5.0

- New: the inventory's "Mods" dropdown. Mods that need it, such as Loot All Pre-Disaster, add their entries to it through the Toolkit. It shares the one button with Storage Hub's and Unpack All's own entries.
- For mod authors: `ModsMenu.Add` puts an entry in the dropdown, with English and Chinese text, an icon, and a rule for when it shows.
- Toast settings apply at once when changed in the Mod Manager: the position, the time on screen, how many show at once, and the off switch.
- The Mod Manager shows the Toolkit with its name in Chinese, a description and its GitHub page, in the Libraries group.

## 0.4.0

- For mod authors: `LiveSettings.Watch` applies a mod's settings while the game runs, once per frame on the main thread, and tells the Mod Manager the setting needs no restart. The Mod Manager 0.4.0 needs this version.
- For mod authors: `ItemValues` reads and writes the item list and item amount strings behind the Mod Manager's item pickers.

## 0.3.0

No visible change. The Mod Manager needs this version.

## 0.2.2

First public release. A shared library for my Survival Log mods. It shows their short notices (toasts) beside the HUD. On its own it does nothing you can see; install it when another mod says it needs it.
