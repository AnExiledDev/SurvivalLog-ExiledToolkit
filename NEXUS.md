Name: Exiled's Toolkit
Summary: The shared library my Survival Log mods use. It lets you make mod windows bigger or smaller, applies settings changed in the Mod Manager without a restart, shows mods' short notices as toasts next to the HUD, keeps tool buttons and phone apps in tidy groups of four, and gives mods one shared Mods menu. On its own it only restyles the HUD's phone and bag buttons.

Description (BBCode):
[center][img]https://sldb.core-directive.com/media/save/1-save-safe.png[/img][/center]

Exiled's Toolkit is the shared part of my Survival Log mods. Install it when another mod says it needs it. On its own it only changes the look and size of the phone and bag buttons on the HUD. Right now it gives mods eight things:
[list]
[*]Toasts. A toast is a short notice that shows up next to the tool buttons on the left of the HUD, stays a few seconds, and goes away by itself. Mods that use the Toolkit show their messages this way instead of each drawing their own.[/*]
[*]Live settings. When you change a setting in the Mod Manager, mods can apply it while you play, without restarting the game.[/*]
[*]One shared Mods button in the inventory, which holds the actions mods add there.[/*]
[*]Tidy tool buttons on the HUD, in columns of four.[/*]
[*]Tidy phone apps. Apps that mods add to the phone go into the dock at the bottom, in rows of four.[/*]
[*]One UI size setting that makes the windows and panels of every mod that supports it bigger or smaller.[/*]
[*]Typing that stays in the text box, so the keys you type don't trigger the game's hotkeys.[/*]
[*]Buttons in the pause menu and title screen that always fit on the screen.[/*]
[/list]
Mod authors: see [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit#for-mod-authors]For mod authors[/url] in the README.

[size=5]Main features[/size]
[list]
[*][b]Next to the HUD[/b]: toasts sit to the right of the tool buttons, above the action queue. You can put them in a screen corner instead.[/*]
[*][b]Compact[/b]: small warm brown cards in the game's gold style. A coloured edge shows whether a toast is info, success, a warning or an error, and a thin bar along the bottom shows how much time it has left.[/*]
[*][b]Stacked[/b]: several toasts lie on top of each other with the newest in front. Rest the pointer on one behind to bring it to the front and show more of its text, plus the items it is about if the mod lists them.[/*]
[*][b]Out of the way[/b]: toasts never take keyboard focus, and they hide while the pointer is on the tool buttons. Over an open window, such as a bench, they stay on top of it, half see-through, and every click goes through them to the window.[/*]
[*][b]Hover to hold, click to open[/b]: while the pointer rests on a toast, its time stops for up to 15 seconds. When a mod gives a toast something to open, clicking the toast opens it. The small x just closes it.[/*]
[*][b]Repeats merge[/b]: if the same notice comes in again, the first one counts up (x2, x3, ...) and no new box appears.[/*]
[*][b]A limit on how many show[/b]: at most 4 toasts show at once by default, and you can change that. The rest wait their turn.[/*]
[*][b]Tidy tool buttons[/b]: the column of buttons on the left of the HUD never gets taller than four. The rest go into a column to its right, top to bottom. The phone and bag stay where they are.[/*]
[*][b]Bigger or smaller tool buttons[/b]: the HudButtonSize setting makes the HUD's tool buttons anywhere from 50% to 150% of their normal size. It starts at 80%.[/*]
[*][b]A new look for the tool buttons[/b]: the HudButtonStyle setting changes how the HUD's tool buttons look, including the buttons mods add and the game's phone and bag. Pick Lamplight (dark brown, each tool's icon in its own colour), FieldKit (olive canvas that presses down like a key), Signal (each tool lit up in its own colour) or Classic (the game's normal look). The phone, the bag and the buttons of my mods get new icons. HudIconColours and HudButtonExtras let you adjust the look further.[/*]
[*][b]Tidy phone apps[/b]: apps that mods add to the phone go into the dock at the bottom, after the game's apps, in rows of four.[/*]
[*][b]Typing stays in the box[/b]: while you type in a text box in a mod's window, the game's hotkeys and camera keys are off. Typing "p" in a search box doesn't take a photo, and WASD doesn't move the camera. Click anywhere outside the box to give the keys back to the game.[/*]
[*][b]Pause menu that always fits[/b]: buttons that mods add to the pause menu and title screen look like the game's buttons. When there are more than fit, the menu's buttons and text shrink just enough to keep Save & Exit on screen.[/*]
[*][b]Bigger or smaller mod windows[/b]: the UiScale setting makes mod windows, panels and toasts anywhere from 75% to 200% of their normal size. Changes show right away, without a restart. A window only grows as far as it still fits on the screen.[/*]
[/list]

The Toolkit adds nothing to your save and does not edit game files.

Works in English and Chinese.

[size=5]Known issues[/size]
[list]
[*]With no window open, you can't click a game button under a toast until the toast goes away. If toasts are in your way, move them with the Position setting.[/*]
[*]Over an open window, you can't hover or click a toast, because its clicks go to the window.[/*]
[*]UiScale only changes what mods draw. The game's screens stay the same size, and a mod's windows follow the setting only once that mod supports it.[/*]
[/list]

[center][img]https://sldb.core-directive.com/img/mods-banner.jpg[/img]
[size=4][url=https://www.nexusmods.com/survivallog/mods/73]Get Mod Manager[/url] to set up all your mods in game  |  [url=https://sldb.core-directive.com/en/]Open the Survival Log wiki[/url] for items, recipes and maps[/size][/center]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.20719. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*][b]Using Mod Manager?[/b] Update it to 0.14.1 or newer together with this Toolkit. Older Mod Manager versions lose their Mods screen with Toolkit 0.19.0 and newer.[/*]
[*]Optional: [url=https://www.nexusmods.com/survivallog/mods/73]Mod Manager[/url]. With it, you can change every setting in game on its Mods screen and see what each one does, without opening the config file.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExiledToolkit.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Remove every mod that needs the Toolkit first, because they won't load without it. Then delete [font=Courier New]ExiledToolkit.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder.

[size=5]Configuration[/size]
To make mod windows bigger or smaller, move the toasts, change how long they stay or how many show, or turn them off, use the Mod Manager's Mods screen in game, where changes take effect right away. You can also edit [font=Courier New]BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg[/font] while the game is closed. The cfg file explains each setting in English and Chinese, and [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/blob/main/CONFIG.md]CONFIG.md[/url] lists them all.

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or leave a comment here. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and [font=Courier New]BepInEx\exiled-toolkit.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord. You can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit]github.com/AnExiledDev/SurvivalLog-ExiledToolkit[/url]

[line]
[size=5]简体中文[/size]

Exiled's Toolkit 是我的 Survival Log 模组共用的部分。其他模组说需要它时再安装。单独安装时，它只会改变 HUD 上手机和背包按钮的外观和大小。目前它为模组提供八样东西：
[list]
[*]通知。通知是一条简短的提示，出现在 HUD 左侧工具按钮的旁边，停留几秒后自动消失。使用 Toolkit 的模组都用这种方式显示消息，不用各自绘制。[/*]
[*]即时设置。在 Mod Manager 里改了设置后，模组可以在游戏进行中直接应用，无需重启游戏。[/*]
[*]背包里一个共用的「Mods」按钮，里面放着各个模组加的操作。[/*]
[*]HUD 工具按钮整齐排列，每列四个。[/*]
[*]手机应用整齐排列。模组加到手机上的应用放进底部的程序栏，每行四个。[/*]
[*]一项界面大小设置，能把所有支持它的模组的窗口和面板放大或缩小。[/*]
[*]打字只进文本框，不会触发游戏的快捷键。[/*]
[*]暂停菜单和标题画面里的模组按钮，总能在屏幕上放下。[/*]
[/list]
模组作者请看 README 中的 [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit#for-mod-authors]For mod authors[/url] 一节（英文）。

[size=5]主要功能[/size]
[list]
[*][b]位于 HUD 旁边[/b]：通知位于工具按钮的右侧、行动队列的上方。也可以改到屏幕的某个角落。[/*]
[*][b]小巧[/b]：紧凑的暖棕色卡片，采用游戏的金色风格。边缘的色条表示信息、成功、警告或错误，底部的细条显示通知还剩多少时间。[/*]
[*][b]层叠显示[/b]：多条通知叠放在一起，最新的在最前面。把鼠标停在后面的通知上，它会移到最前面并显示更多文字。如果模组列出了相关物品，也会一并显示。[/*]
[*][b]不碍事[/b]：通知不会抢走键盘焦点。鼠标停在工具按钮上时，通知会隐藏。打开窗口（例如工作台）时，通知仍以半透明显示在窗口上方，点击会穿过通知落到窗口上。[/*]
[*][b]悬停暂停，点击打开[/b]：鼠标停在通知上时，它的计时会暂停，最多 15 秒。如果模组为通知设置了可打开的内容，点击通知就会打开它。小 x 只关闭通知。[/*]
[*][b]重复合并[/b]：相同的通知再次出现时，第一条会计数（x2、x3 等），不会新增一个方框。[/*]
[*][b]数量有上限[/b]：默认最多同时显示 4 条通知，这个数量可以修改。其余的排队等候。[/*]
[*][b]工具按钮整齐排列[/b]：HUD 左侧的按钮列最多四个高，其余的按从上到下的顺序排到右边的新列。手机和背包保持原位。[/*]
[*][b]工具按钮大小[/b]：HudButtonSize 设置可以把 HUD 左侧的工具按钮按正常大小的 50% 到 150% 显示，初始为 80%。[/*]
[*][b]工具按钮新样式[/b]：HudButtonStyle 设置决定 HUD 工具按钮（模组的按钮以及游戏的手机和背包）的样式：灯火（深棕色，每个工具的图标各有颜色）、野外装备（橄榄色帆布，按下时像按键一样下沉）、信号灯（每个工具以各自的颜色发光）或经典（游戏默认的样式）。手机、背包和我的模组的按钮会换上新图标。HudIconColours 和 HudButtonExtras 可以进一步调整外观。[/*]
[*][b]手机应用整齐排列[/b]：模组加到手机上的应用放进底部的程序栏，排在游戏的应用之后，每行四个。[/*]
[*][b]打字不会触发快捷键[/b]：在模组窗口的文本框里打字时，游戏的快捷键和镜头按键会关闭。在搜索框里输入 p 不会拍照，WASD 也不会移动镜头。点击文本框以外的任何地方，按键就会交还给游戏。[/*]
[*][b]暂停菜单总能放下[/b]：模组加到暂停菜单和标题画面的按钮与游戏的按钮外观一致。按钮多到放不下时，菜单的按钮和文字会缩小到刚好能放下，「保存并退出」始终留在屏幕上。[/*]
[*][b]模组窗口可放大缩小[/b]：UiScale 设置可以把模组的窗口、面板和通知按正常大小的 75% 到 200% 显示。改动立即生效，无需重启。窗口只会放大到屏幕还能完整显示的程度。[/*]
[/list]

Toolkit 不会往存档里写入任何内容，也不会修改游戏文件。

支持英文和中文。

[size=5]已知问题[/size]
[list]
[*]没有打开窗口时，通知下方的游戏按钮要等通知消失后才能点击。如果通知挡路，可以用 Position 设置移动它们。[/*]
[*]打开窗口时，通知不能悬停或点击，因为点击会落到窗口上。[/*]
[*]UiScale 只改变模组绘制的界面，游戏的界面大小不变。模组更新到支持此设置后，它的窗口才会跟着变化。[/*]
[/list]

[url=https://www.nexusmods.com/survivallog/mods/73]获取 Mod Manager[/url]，在游戏里设置所有模组。[url=https://sldb.core-directive.com/zh/]打开 Survival Log 维基[/url]，物品、配方、地图一查就有。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.20719。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*][b]用了 Mod Manager？[/b]请把它和本 Toolkit 一起更新到 0.14.1 或更新版本。旧版 Mod Manager 配合 Toolkit 0.19.0 及更新版本时，「模组」界面无法打开。[/*]
[*]可选：[url=https://www.nexusmods.com/survivallog/mods/73]Mod Manager[/url]。装了它，可以在游戏里它的「模组」界面修改每项设置，并看到每项的说明，不用打开配置文件。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExiledToolkit.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]ExiledToolkit.dll[/font]。删除前，请先移除所有需要 Toolkit 的模组，否则它们无法加载。

[size=5]配置[/size]
如需放大或缩小模组窗口、移动通知、修改停留时间或显示数量，或关闭通知，可以在游戏内 Mod Manager 的「模组」界面修改，改动立即生效。也可以在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\exiled-toolkit.log[/font]。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit]github.com/AnExiledDev/SurvivalLog-ExiledToolkit[/url]
