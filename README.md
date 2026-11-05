# Exiled's Toolkit

A shared library for my Survival Log mods. On its own it does nothing you can see. Install it when another mod says it needs it.

Right now the Toolkit gives mods eight things. The first is toasts. A toast is a short notice that appears next to the phone, defense and other tool buttons, stays a few seconds, and goes away by itself. Mods that use the Toolkit show their messages this way instead of each drawing their own. The second is live settings: mods can apply a changed setting while you play, so a change in the Mod Manager works without restarting the game. The third is the inventory's "Mods" dropdown, one button in the inventory toolbar that holds the actions mods add there. The fourth keeps the HUD's tool buttons tidy: once mods have added enough buttons that the column would run past four, they are laid out in columns of four instead. The fifth tidies the phone: apps that mods add go into the dock at the bottom, after the game's own apps. The sixth is the UI size: one setting draws the windows and panels of every mod that supports it bigger or smaller. The seventh keeps your typing out of the game: while you type in a mod's text box, the keys do not open panels or move the camera. The eighth is buttons in the game's pause menu and title screen: mods add them through the Toolkit, and the menu shrinks its buttons and text just enough to keep every one on the screen.

## Features

- **Beside the HUD**: toasts sit to the right of the phone, defense and other tool buttons, above the action queue, and follow them if the screen size changes. You can put them in a screen corner instead. Each one has a thin coloured edge that says what kind it is (info, success, warning or error).
- **Small and tidy**: compact boxes in the game's colours and font, with long text cut to two lines. They stay 7 seconds by default.
- **Layered**: several toasts lie on top of each other, newest in front, with the older ones peeking out behind, so they take about one toast's room. Rest the pointer on one behind to bring it to the front with more of its text (up to six lines).
- **Never in the way**: toasts never take keyboard focus. Only the boxes themselves catch the mouse; the rest of the screen plays as normal. They hide for a few seconds while the pointer is on the tool buttons or the speed buttons, so those buttons' tips can be read. Over an open window, such as a bench, they stay in sight on top of it, half see-through, and every click goes through them to the window.
- **Hover to keep one**: while the pointer rests on a toast, it stays. Move away and it leaves after a moment.
- **Click to open**: when a mod gives a toast something to open, clicking the toast opens it. The small x closes a toast without opening anything.
- **Repeats merge**: the same notice sent again while the first is still showing does not add another box. The first one counts up instead (x2, x3, ...) and its timer starts over.
- **No pile-ups**: at most 4 toasts show at once by default. The rest wait their turn and appear as earlier ones close. If more than 30 are waiting, the oldest waiting ones are never shown, though mods that keep a list of toasts still get them.
- **Tidy tool buttons**: the column of buttons on the left of the HUD (the phone, the bag and the buttons mods add there) never gets taller than four buttons. The rest go into a second column to its right, top to bottom, and a third after that. The phone and bag stay where they always are, and toasts move right to clear the extra column.
- **Tidy phone apps**: apps that mods add to the phone go into the dock at the bottom, after the game's own, in rows of four, instead of stacking in the middle of the home screen.
- **Bigger or smaller mod windows**: `UiScale` draws mod windows, panels and toasts from 75% to 200% of their size. A change shows at once, without a restart. A window that would run off the screen is drawn as big as still fits. A mod with a UI size setting of its own uses that one when it is set.
- **Smaller or bigger tool buttons**: `HudButtonSize` draws the HUD's tool buttons from 50% to 150% of the game's size, 80% unless you change it.
- **A new look for the tool buttons**: `HudButtonStyle` draws the HUD's tool buttons, the mods' and the game's phone and bag, as Lamplight (dark brown, each tool's icon in its own colour), FieldKit (olive canvas that presses like a key), Signal (each tool lit in its own colour) or Classic (the game's own). The phone, the bag and the buttons of Trap Hub, Package Bank, Field Guide, Farm Hub, Defense Manager, Machines, Power Manager and Storage Hub get new icons. `HudIconColours` and `HudButtonExtras` fine-tune it. A change shows at once.
- **Typing stays in the box**: while a text box in any mod's window has the keyboard, the game's hotkeys and the camera keys are off, so typing "p" in a search box does not take a photo and WASD does not move the camera. Click the world, or anywhere outside the box, to give the keys back to the game.
- **Pause menu that always fits**: buttons mods add to the pause (Esc) menu and the title screen look like the game's own. When there are more than the screen has room for, the whole menu's buttons, gaps and text shrink just enough that Save & Exit stays on screen, down to half size; past that the button list scrolls. A menu that already fits keeps the game's size.
- **Survives reloads**: if the game's web interface reloads, the toasts on screen come back with the time they had left.

The Toolkit adds nothing to your save and does not edit game files.

## Known issues and limits

- With no window open, a game button under a toast cannot be clicked until the toast goes. Next to the HUD that is empty space. Move the toasts with `Position` if they are in your way.
- Over an open window a toast cannot be hovered to hold it or clicked, since its clicks go to the window. It runs out on its usual time.
- A button from a mod the Toolkit does not know keeps its own icon, tinted amber, in the new button styles.
- `UiScale` changes only what mods draw. The game's own screens keep their size, and a mod's windows follow the setting only once that mod supports it.

## Requirements

- Survival Log 1.1.20151. Built and tested on this version; a game update can break it until the mod is updated.
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

Tested on Survival Log 1.1.20151 with BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for `===== Exiled's Toolkit 0.18.0 started`. A few lines below it, `toasts shown ...` tells you whether toasts are shown, where they appear, how long they stay and how many show at once; it is written again whenever one of those settings changes. `mod windows drawn at ...%` gives the UI size and `HUD buttons drawn at ...%` the button size, both written again on every change. A mod with its own UI size writes `<mod> windows drawn at ...%` when it sets it. Once the game's interface is up, `toast script sent to the web UI` means toasts can appear. The same lines are in `BepInEx\exiled-toolkit.log`, which survives restarts.

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

### World rules

A setting can be a world rule: a world (a save) sets its own value, and every other world, and the cfg, keep the global one. Tag the setting with the plain string `ModManager.WorldRule` and watch it with `LiveSettings`; that is all a mod does. No reference to anything else is needed, so the tag is harmless when nothing uses it.

```csharp
var days = Config.Bind("Restock", "Days", 3, new ConfigDescription("...", null, "ModManager.WorldRule"));
LiveSettings.Watch(days, value => Restock.Days = value);
```

- Bool, int, float, double, enum (not flags) and text settings can be world rules. A text setting with a fixed list of choices is a choice; one without holds a number, and a world value that is not a number is refused. A tagged setting of another type, or one not watched live, is not a rule; the first is named in the log.
- When a world starts, each rule it sets takes the world's value, and your watcher runs as for any change. When it closes, the global value comes back. The cfg always holds the global value, so a crash mid-world leaves nothing to clean up.
- While a world sets a rule, a change to it from anywhere (the Mod Manager, your mod) is a change to that world's value.
- The world's values are kept by the mod that starts the worlds (Customizable Difficulty keeps them in the save). It calls `WorldRules.Begin(values)` at a world start and `WorldRules.End()` at its close, and stores what `WorldRules.WorldValueChanged` reports. A world stores each value as a whole number: bool as 0 or 1, int as is, float, double or number text times 1000, a choice as its place in the list; `TryEncode` and `TryDecode` convert.
- For a settings screen: `IsRule`, `IsSetByWorld`, `GlobalValue`, `SetWorld`, `ClearWorld` and `SetGlobal`. Values are serialized as in the cfg.

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

### Shared helpers (C#)

Toolkit 0.19.0 and newer. The plumbing most mods used to carry their own copy of, in three namespaces: `ExiledToolkit.Web` (the game's web view), `ExiledToolkit.Game` (game reads, hooks, keys and clicks) and `ExiledToolkit.Kit` (plain helpers). They sit outside `ExiledToolkit` so a mod that still has its own copy of a helper keeps compiling until it deletes it. Everything here is for the game's main thread unless it says otherwise.

#### Page messages

`PageInbox` catches the messages your page script sends (`vuplex.postMessage("slmod|farmhub|open")`) before the game sees them. The Toolkit has the one Harmony hook on the game's message handler; don't hook `HandleMessageEmitted` yourself.

```csharp
using ExiledToolkit.Web;

var mailbox = PageInbox.Listen("slmod|farmhub|");                       // drain it yourself each frame
while (mailbox.TryTake(out var message)) Handle(PageCommand.Parse(message));

PageInbox.Listen("slmod|vitals|tab|", "slmod|vitals|hud|");             // one mailbox, two prefixes
PageInbox.Listen("slmod|machines|timer|", message => TimerPage.AskInstall());   // a handler, called on the main thread
```

- A message goes to the mailbox with the longest prefix it starts with, whichever mod listened first; a message no mailbox takes goes on to the game. The message you get is whole, prefix included.
- `Listen` with a prefix that already has a mailbox gives that mailbox. Several prefixes make one mailbox; prefixes that already belong to two different mailboxes are refused (`ArgumentException`), and so is an empty prefix.
- A handler runs on the main thread in the Toolkit's update of the frame after the message came. One that throws is logged once and does not stop the others. A later handler for the mailbox replaces the earlier one.
- `TryTake(out message, out arrivedAt)` also gives when it arrived, a `Stopwatch.GetTimestamp()` reading taken on the web view's thread. `HasWaiting` says whether anything waits (any thread). `PageInbox.IsListening` is false when the hook could not go in.

#### Page script

`WebPage` keeps your embedded page.js in the game's web UI. `WebUi.TryRun(js, out failure)` runs one script in the root document, false when there is no web view yet.

```csharp
using ExiledToolkit.Kit;
using ExiledToolkit.Web;

var page = new WebPage(typeof(Plugin).Assembly, "FarmHub.page.js", "window.__slfarmhub", "slmod|farmhub|noscript", log.Write)
{
    Words = isChinese => PageWords.Script(isChinese),   // optional
    CheckOffset = 0.6,                                    // optional
};

page.KeepInstalled(Clock.Now);       // every frame: probes every 2 seconds
page.Install();                      // when your page answers "noscript": the web view reloaded
page.Call("open(" + viewJson + ")"); // window.__slfarmhub.open(...), or "noscript" when the script is missing
page.Run("window.__slfarmhub&&window.__slfarmhub.close()");
```

- The probe (`Probe`) is silent while `global` exists and sends your noscript message when it does not. Send it with `KeepInstalled(now)`, any clock in seconds; it probes on a steady beat of `CheckSeconds` (2), starting `CheckOffset` seconds after the first call, and a long stall starts the beat again.
- `Install()` sends the resource, then each of `ExtraResources` after it (`"\n;"` between them), and logs `page script sent to the web UI` (`Name` replaces "page"). It throws `InvalidOperationException` when a resource is not embedded.
- With `Words` set, a `Call` sends the words first whenever the page lacks them or holds the other language's: after `Install`, after `ForgetWords()` (call it when the page says it installed itself afresh), and when the game's language changes. Words that did not reach the page go with the next call.
- `Call` and `Run` return false when there is no web view or the script failed; a failure is logged once, as `could not run the page script in the web UI (reported once): ...`. `HasWebView` is whether the last run found the web view. `Global` and `NoScriptReply` are there for calls you build yourself.
- `JsText.Literal(text)` turns any text into a JavaScript string literal that cannot break out of the script (null is `""`).

#### Mod log

`ModLog` writes each line to the BepInEx log (a `could not hook` line as an error) and to your own file under the BepInEx folder, which survives restarts. A Toolkit worker thread writes every mod's file, so logging never waits on the disk; the lines still waiting are written when the game closes. Any thread.

```csharp
using ExiledToolkit.Kit;

var log = new ModLog(Log, "farm-hub.log") { MaxFileBytes = 2_000_000 };
LiveSettings.Watch(diagnostics, isOn => log.IsFileOn = isOn);
log.Write("loaded");
log.WriteFileOnly("a detail too many for the console");
```

- `IsFileOn` (default on) turns the file copy off; the BepInEx log still gets every `Write`. `MaxFileBytes` above 0 moves a full file to `<name>.1` and starts it again. `Flush()` writes the waiting lines now, on your thread. `Path` is the file's full path.
- `LogLines` is the queue behind it, if you write a file of your own: `Add(atUtc, message)` from any thread (true when the writer needs waking), `Take()` on one writer thread gives every waiting line, stamped in local time, or null.

#### Hook failures

`HookFailure.Prefix` is `could not hook`. Start every log line about a hook that could not be placed with it: the Mod Manager reads those lines as a sign the mod may need an update after a game update. `HookFailure.Is(line)` checks one.

`PatchKit` places your own hooks with that wording, and logs failures from inside a hook without flooding the log:

```csharp
using ExiledToolkit.Game;

var hooks = new PatchKit(harmony, log.Write);   // maxLoggedFailures: 1 by default
hooks.Patch(typeof(CropHooks), typeof(Plant), "Grow", postfix: nameof(CropHooks.AfterGrow), done: "crops grow faster", consequence: "crops grow at the game's speed");
// in the hook's catch:
hooks.Fail("speeding up a crop", ex);
```

- `Patch(hooks, type, method, parameters, prefix, postfix, finalizer, done, consequence)` returns whether the hook went in, and logs `done` or `could not hook Type.Method, consequence: why`. Give `parameters` to pick one overload. `PatchOrThrow` does the same without logging and throws instead.
- `Fail(doing, ex)` logs `failed <doing> (reported once): ...`, or `(logged 2 of 3)` with a higher `maxLoggedFailures`, then stays quiet for that `doing`. Any thread; never throws.

#### Window keys and clicks

The Toolkit has the one hook on the game's hotkeys (`HotKeyManager.Update`) and the one on where clicks go (`WebUILayer.ValidateRaycast`); don't patch either yourself.

```csharp
using ExiledToolkit.Game;
using ExiledToolkit.Web;

InputGate.Opened("FarmHub.window");   // hotkeys held back, every click to the web UI
InputGate.Closed("FarmHub.window");   // both let go; hotkeys stay off through the next frame, so the closing Esc is not seen twice

HotkeyGate.Hold("FarmHub.menu");      // only the keys, say for a dropdown
HotkeyGate.Release("FarmHub.menu");
ClickRouting.Capture("FarmHub.picker", true);

var keyboard = new WebKeyboard();     // one per window
keyboard.Claim();                     // your page says a text field took focus
keyboard.Release();                   // and that it let go
```

- Each holder holds and lets go under its own name, so two mods never release each other's hold; holding twice under one name is one hold. `HotkeyGate.IsHeld` and `ClickRouting.IsCapturing` say whether anyone holds now.
- `WebKeyboard.Claim` sends the keyboard to the web UI and turns on the system's input method, so Chinese can be typed; `Release` gives the keyboard back as it was found and turns the input method off. `IsClaimed` is true in between. The Toolkit already keeps the game's keys off while any text box has the keyboard (see Typing).

#### Hotkeys

```csharp
using ExiledToolkit.Kit;

var key = Config.Bind("Keys", "PanelKey", KeyCode.None, "...");   // a key with a button too starts unbound: Hotkey.DefaultKey
if (Hotkey.Fires(key.Value != KeyCode.None, () => Input.GetKeyDown(key.Value), () => Typing.IsActive)) Toggle();
if (KeyGate.Fires(key.Value, KeyCode.None, Input.GetKeyDown, () => Typing.IsActive)) Toggle();
```

Both ask for the press only for a bound key and for typing only on a press. `KeyGate.Fires(isBound, isPressed, isTyping)` takes the press already read.

#### Game reads

```csharp
using ExiledToolkit.Game;

var config = GameSingleton.Existing<ConfigManager>();     // null until the game made it
if (!GameSingleton.IsCreated<GameSaveManager>()) return;   // allocates nothing
var isChinese = GameLanguage.IsChinese();                  // false (English) when unknown
var name = GameNames.ItemName(config, 1203) ?? "Item 1203";
var save = SaveSlot.CurrentKey();                          // the save's file name, or null
var frame = UnityFrame.Count();                            // Time.frameCount without boxing
```

- `GameSingleton.Existing<T>()` never makes a singleton: one read before the game makes it would be built too early and answer the game's events twice for the rest of the session. `Pointer<T>()` is its native address, zero before; a new address means a new one.
- `GameLanguage.Key()` is the game's language key, empty when unknown, for caches of names read from its tables.
- `GameNames.ItemName(config, item)` and `ItemName(config, configId)` give an item's name as the game shows it: the text for its name key, else its local name, else the key; null when it has none. By id it reads the table itself, since the game's own getter logs an error for every id it lacks. `Translate(config, key)` is the game's text for any key in the language it shows, and `Translate(config, key, language)` in the one you pick; both null when the table has no text for it. `NonEmpty(text)` is null for blank text. None of them throws.
- `RawDictionary` (a game `Dictionary<long, reference>`) and `RawList` (a game `List<reference>`) read a big collection straight from its memory for a hot walk: `Learn(class)` once, then `TryOpen(object, ...)` every walk, and fall back to the interop path when it returns false. The pointers are good only within that frame.

#### Web icons

```csharp
using ExiledToolkit.Web;

_iconScan = WebIcons.StartScan();                                    // once, at load, on the main thread
if (_iconScan.IsCompleted) log.Write(_iconScan.Result.Note);         // later, from the main thread
var src = WebIcons.Icons.Resolve(item.WebIcon) ?? WebIcons.Icons.Resolve(item.Icon);   // "Res/Food/x.png", or null
```

- The game's web icon folder (about 1800 files) is walked once for every mod, on a worker; every `StartScan` gets the same task, which never fails. `Icons` is `IconIndex.Empty` until it is done (`IsReady`), so show letters meanwhile.
- `IconIndex.Resolve(named)` matches on the file name, whatever path the table gives; of several files with that name, the one whose folder the table names wins. `IconIndex.Build(paths)` indexes paths of your own.

#### UI size setting

`ModUiSize.Bind(Config, "FarmHub")` binds your Interface > UiScale setting (0 to 200, default 0) with the standard description, applies it live and calls `ModUiSize.Set` for you. The description names the mod by splitting the name ("Farm Hub"); pass `displayName` when that reads wrong, or a whole `description` (English, `\n`, Chinese) when your panels are not windows. Nothing is sent while the setting stays 0.

#### Small helpers

All in `ExiledToolkit.Kit`:

- `Phrase(En, Zh)`: `Pick(isChinese)`, `Say(isChinese, args)` (numbers written the same in every culture), `Phrase.FromGame(line)`. `Counted(One, Many).Say(isChinese, count, more)` picks One only for 1, with the count as `{0}`.
- `SlicedJob(steps)`: a long job as an `IEnumerable<bool>`; `Advance(isSliceSpent)` runs steps until a step yields false, the slice is spent or the job ends, at least one step per call, and returns `IsDone`.
- `Clock.Now`: seconds as a `double`, any thread, without the boxing Unity's clocks cost.
- `ReplySlot`: `Open()` numbers a call to the page, `Offer(call, reply)` (any thread) keeps only the answer to the newest call, `Take()` hands it over once.

### Mods menu

`ModsMenu` puts an entry in the inventory toolbar's "Mods" dropdown, between the game's discard and sort buttons. Every mod's entries share the one button, and the button only shows while at least one entry wants to be listed. The same button also sits in the bag panel of the workbench, stove, ration press, greenhouse, drone hub, brewing barrel, dye vat, rat cage, material rack and shop screens (not the shredder or the drone trade), but it lists only the entries that set `Stations = true`; the rest stay on the inventory.

```csharp
using ExiledToolkit;

ModsMenu.Add(new ModsMenuEntry("mymod.sortall", "Sort all", SortAll)
{
    ChineseLabel = "全部整理",
    Title = "Sort every item in your bag",
    ChineseTitle = "整理背包里的所有物品",
    Icon = "M4 6h16v2H4zM4 11h12v2H4zM4 16h8v2H4z",
    Order = 300,
    Hotkey = "F7",
    Stations = true,
    IsShown = () => IsBeforeDisaster(),
});
```

- The id is unique across mods: 1 to 64 letters, digits, `.`, `_` or `-`. Adding an entry with an id already in use replaces it; `ModsMenu.Remove(id)` takes it out.
- The label and tooltip show in the game's language; empty Chinese text shows the English. `Icon` is the `d` of an SVG path on a 24-unit grid.
- Rows sort by `Order`, then id. Storage Hub uses the 100s and Unpack All 200.
- `IsShown` is asked on the main thread a few times a second; leave it out to always list the entry. One that throws counts as hidden and is logged once. The dropdown itself shows only on the inventory's item tab and the station screens.
- `Hotkey` is shown at the right of the row as given; leave it empty for none. To change it, add the entry again with the same id.
- `Stations = true` also lists the entry on the station screens' bag panel (since 0.15.0).
- The click action runs on the main thread after the menu has closed. One that throws is logged to `BepInEx\exiled-toolkit.log`.
- While the menu is open the game's hotkeys are held back, and Esc closes the menu.

### Pause menu and title screen

`GameMenu` puts a button in the game's pause (Esc) menu or on its title screen, drawn like the game's own. The Toolkit keeps it there when the game redraws the menu and shrinks the menu's buttons, gaps and text to fit the screen however many buttons mods add (to half size at most; past that the list scrolls).

```csharp
using ExiledToolkit;

GameMenu.Add(new GameMenuButton("mymod.backup", "Back up now", GameMenuPlace.PauseBottom, BackUpNow)
{
    ChineseLabel = "立即备份",
    Order = 200,
    Accent = "#8fc7bf",
    IsShown = () => IsInRun(),
});
```

- `PauseTop` goes right after the game's Settings, `PauseBottom` at the bottom just above Restart (when the game shows it) and Save & Exit, and `Title` right before the title screen's Exit.
- The id is unique across mods: 1 to 64 letters, digits, `.`, `_` or `-`. Adding a button with an id already in use replaces it; `GameMenu.Remove(id)` takes it out.
- The label shows in the game's language; empty Chinese text shows the English.
- Buttons in one place sort by `Order`, then id. Mod Manager uses 100 on the title screen and at the top, Save Backups 200 and 210 at the bottom, and New Loop Plus 900, so its button sits last above Save & Exit.
- `Accent` colours the button's left edge (the bar, on the title screen) as `#rgb` or `#rrggbb`; leave it empty for the game's.
- `IsShown` is asked on the main thread a few times a second; leave it out to always show the button. One that throws counts as hidden and is logged once.
- The click action runs on the main thread, with the menu still open. On the title screen it runs after the game's own click animation. One that throws is logged to `BepInEx\exiled-toolkit.log`.
- Never add your own nodes to these two menus: they would not count when the Toolkit fits the menu.

### HUD buttons

A mod that adds a button to the HUD's tool column (`.vertical-tools` in CoreUI1 or CoreUI0) marks it with `data-etk-dock="<order>"`. Past four shown buttons the Toolkit lays the column out four high: the last four stay in the first column, and the rest fill columns to the right, top to bottom. Marked buttons come first, lowest order first, then the game's own buttons and unmarked ones in page order. Trap Hub uses 10, Package Bank 20, Farm Hub 30 and Defense Manager 40.

Nothing else is needed, and the mark does nothing without the Toolkit, so a mod can keep the Toolkit optional.

The player's `HudButtonSize` zooms the whole column, the game's buttons with the mods', so a button needs nothing more to follow it.

The player's `HudButtonStyle` restyles every `.ui-btn` in the column, so a button that uses the game's `ui-btn icon-sq` classes takes the style with nothing more. The Toolkit marks the buttons it has an icon for with `data-etk-ic="<tool>"`, hides their `svg.icon-svg` and adds its own `svg.etk-ic` beside it; it knows the buttons by their classes (`th-hud-btn`, `pb-hud-btn`, `fg-hud-btn`, `fh-hud-btn`, `dm-hud-btn`, `mso-hud-btn`, `pm-hud-btn`, `sh-hud-btn`). Any other button keeps its own svg, filled with the style's amber, so keep its fill at `currentColor`. Badges named `th-hud-badge`, `fh-hud-badge`, `mso-hud-needs` and `mso-hud-working` take the stamped look of the extras.

To draw anything else in the player's style, read the HUD page's `<html>`: `data-etk-look` is `classic`, `lamplight`, `fieldkit` or `signal`, and `data-etk-tips` is `paper` when tips wear Field kit's paper tags, `dark` otherwise. In every style but Classic the page also has these colour variables: `--etk-accent` (and `--etk-accent-rgb` as `r,g,b`), `--etk-panel`, `--etk-edge`, `--etk-text`, `--etk-muted`, `--etk-track` (an empty bar), `--etk-radius`, and `--etk-tip-bg`, `--etk-tip-edge`, `--etk-tip-text`, `--etk-tip-muted` for tips. Always give a fallback, `var(--etk-panel, #111)`, for Classic and for players without the Toolkit.

### Phone apps

A mod that adds an app to the phone's home screen (PhoneSMS.html) builds it like the game's own (`.app` with `.app-icon` and `.app-label`), puts it in `.app-grid`, and marks it with `data-etk-phone="<order>"`. The Toolkit moves marked apps into `.app-dock` after the game's apps, lowest order first, and past four shown apps lays the dock out in rows of four. Without the Toolkit the app stays in `.app-grid`. Look the app up by its class anywhere in the document, not only in `.app-grid`, or it is added again once moved. Field Guide uses 10 and Neighbor Basket 20.

### UI size

The player's `UiScale` setting draws mod windows bigger or smaller with CSS `zoom`. A page script hands each window or panel it draws to `ExiledUi.size` once, right after it puts the element in the page, and the Toolkit zooms it at once and again after every change of the setting. Carry this helper in the script and call it instead: it holds panels until the Toolkit's script has loaded, so load order does not matter, and it does nothing without the Toolkit, so the Toolkit can stay optional.

```js
function uiSize(el, options) {
  var ui = window.ExiledUi;
  if (ui) {
    ui.size(el, options);
    return;
  }
  var waiting = window.__exiledUiWait = window.__exiledUiWait || [];
  waiting.push([el, options]);
  if (waiting.length > 32) waiting.shift();
}

uiSize(root);                                         // a window
uiSize(backdrop, { fit: backdrop.firstChild });       // a full-screen backdrop: the window inside it must fit
uiSize(tooltip, { fit: false });                      // never shrunk to fit
uiSize(root, { onZoom: function (zoom) { refit(); } });
uiSize(column, { fit: page, pageFits: true, onZoom: function () { applyViewportFit(); } });   // inside a page that fits itself
```

- Above 100%, a panel whose box would run past its page's window is zoomed as far as still fits, and never below 100%. `fit` names the element whose box is measured; leave it out to measure the panel itself.
- `onZoom(zoom)` runs after each new zoom, say to place the panel again. One that throws is skipped and does not stop other panels.
- `pageFits: true` is for a panel added inside a game page that zooms its own body down when its panels are too big for the window (the workbench and several machine screens do). `fit` is then measured at the page's own size, without that body zoom, so the panel grows only as far as the whole page still shows at full size and the game's own panels keep their size. Have `onZoom` ask the page to fit itself again.
- In a zoomed panel, `getBoundingClientRect` and mouse positions are screen pixels, while `left`, `top`, `width` and `offsetLeft` inside it are the panel's own pixels. Divide screen pixels by `el.currentCSSZoom` before writing them as positions.
- A panel taken out of its page, or left in a page that reloaded, is let go. Hand it over again when you put it back. Handing the same element twice changes nothing but its options.
- `ExiledUi.scale` is the size now as a factor (1.25 at 125%).
- A mod with a UI size setting of its own passes its name as `mod` with every panel (`uiSize(root, { mod: 'FarmHub' })`) and, when the setting loads or changes, calls `ExiledToolkit.ModUiSize.Set("FarmHub", percent)` from C# (Toolkit 0.11.0 and newer). 75 to 200 draws that mod's panels at that size whatever `UiScale` is; 0 has them follow `UiScale` again. The name is letters, digits, dots, dashes and underscores. With an older Toolkit the call throws `MissingMethodException`, so keep it behind the same guard as the rest of the optional Toolkit calls. `ExiledUi.scaleOf(name)` is the size that mod's panels are drawn at now.
- `ExiledUi.zoomAt(el)` is the zoom the Toolkit draws `el` at through the panels it sits in, and 1 outside any. The page's own zoom is not counted. A tooltip drawn on the page's body for a row inside a panel can take this zoom to match the panel. Read it again on each draw, since the size can change while the tooltip shows.

### Typing

While any text box in any page has the keyboard, the Toolkit stops the game's hotkeys and the camera keys by itself, so a mod's search box needs nothing for that. A mod that reads its own hotkeys with `Input.GetKeyDown` skips them while `ExiledToolkit.Typing.IsActive` is true (Toolkit 0.12.0 and newer). Read it on the game's main thread. With an older Toolkit the read throws `MissingMethodException`, so keep it behind the same guard as the rest of the optional Toolkit calls:

```csharp
[MethodImpl(MethodImplOptions.NoInlining)]
private static bool IsTypingUnguarded() => ExiledToolkit.Typing.IsActive;

if (Input.GetKeyDown(Key) && !(IsToolkitLoaded && IsTypingUnguarded())) Toggle();
```

- A box counts while it has focus: a textarea, a contenteditable element, or an input of type text, search, number, email, password, url or tel that is not disabled or read-only. Checkboxes, sliders and buttons do not count.
- A box that is removed or hidden stops counting within a quarter of a second. A click on the world (not on any UI) takes the keyboard from the box.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/ExiledToolkit -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. The output DLL is at `src\ExiledToolkit\bin\Release\ExiledToolkit.dll`.

The unit tests cover the toast rules and the page scripts, and need nothing from the game:

```
dotnet test tests/ExiledToolkit.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
