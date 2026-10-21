Name: Exiled's Toolkit
Summary: The shared library my Survival Log mods use. With it, settings changed on the Mod Manager's Mods screen apply without a restart, mods show their short notices as small toasts beside the HUD, the HUD's tool buttons stay in tidy columns of four, and mods share one Mods menu in the inventory. On its own it does nothing you can see.

Description (BBCode):
Exiled's Toolkit is the shared part of my Survival Log mods. Right now it gives them four things. The first is toasts. A toast is a short notice that appears next to the phone, defense and other tool buttons, stays a few seconds, and goes away by itself. Mods that use the Toolkit show their messages this way instead of each drawing their own. The second is live settings: mods can apply a changed setting while you play, so a change in the Mod Manager works without restarting the game. The third is the inventory's Mods dropdown: mods add their own entries to it, all under one button. The fourth keeps the HUD's tool buttons tidy, in columns of four. Mod authors: see [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit#for-mod-authors]For mod authors[/url] in the README.

[size=5]Main features[/size]
[list]
[*][b]Beside the HUD[/b]: toasts sit to the right of the tool buttons, above the action queue. You can put them in a screen corner instead.[/*]
[*][b]Small and tidy[/b]: compact boxes in the game's colours and font, with a thin coloured edge for info, success, warning or error.[/*]
[*][b]Layered[/b]: several toasts lie on top of each other, newest in front. Rest the pointer on one behind to bring it to the front with more of its text.[/*]
[*][b]Never in the way[/b]: toasts never take keyboard focus, and they hide while the pointer is on the tool buttons or a full-screen window is open.[/*]
[*][b]Hover to keep one, click to open[/b]: a toast stays while the pointer rests on it. When a mod gives a toast something to open, clicking it opens that; the small x just closes it.[/*]
[*][b]Repeats merge[/b]: the same notice sent again counts up (x2, x3, ...) instead of adding another box.[/*]
[*][b]No pile-ups[/b]: at most 4 toasts show at once by default; the rest wait their turn.[/*]
[*][b]Tidy tool buttons[/b]: the column of buttons on the left of the HUD never gets taller than four. The rest go into a column to its right, top to bottom; the phone and bag stay where they are.[/*]
[/list]

The Toolkit adds nothing to your save and does not edit game files.

Works in English and Chinese.

[size=5]Known issues[/size]
[list]
[*]A toast covers whatever is under it. A game button under a toast cannot be clicked until the toast goes. Move the toasts with the Position setting if they are in your way.[/*]
[*]Toasts that hide for an open panel keep counting down, so a short one can run out before the panel closes.[/*]
[/list]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.19131. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExiledToolkit.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Remove every mod that needs the Toolkit first; they will not load without it. Then delete [font=Courier New]ExiledToolkit.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder.

[size=5]Configuration[/size]
To move the toasts, change how long they stay or how many show, or turn them off, use the Mod Manager's Mods screen in game, where changes take effect at once, or edit [font=Courier New]BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg[/font] while the game is closed (see [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/blob/main/CONFIG.md]CONFIG.md[/url]; the cfg file explains each setting in English and Chinese).

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or leave a comment here. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and [font=Courier New]BepInEx\exiled-toolkit.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].
Coming soon: a Survival Log wiki with detailed documentation for all of my mods.

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit]github.com/AnExiledDev/SurvivalLog-ExiledToolkit[/url]

[line]
[size=5]简体中文[/size]

Exiled's Toolkit 是我的 Survival Log 模组共用的部分。目前它为这些模组提供四样东西。第一样是通知。通知是一条简短的提示，出现在手机、防御和其他工具按钮旁边，停留几秒后自动消失。使用 Toolkit 的模组都用这种方式显示消息，而不是各自绘制。第二样是即时设置：模组可以在游戏进行中应用改动过的设置，因此在 Mod Manager 里改设置无需重启游戏。第三样是背包的「Mods」下拉菜单：各个模组把自己的条目加到这一个按钮下。第四样是让 HUD 的工具按钮保持整齐，每列最多四个。模组作者请看 README 中的 [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit#for-mod-authors]For mod authors[/url] 一节（英文）。

[size=5]主要功能[/size]
[list]
[*][b]位于 HUD 旁边[/b]：通知位于工具按钮的右侧、行动队列的上方。也可以改到屏幕的某个角落。[/*]
[*][b]小巧整洁[/b]：紧凑的方框，使用游戏的配色和字体，边缘的细色条表示信息、成功、警告或错误。[/*]
[*][b]层叠显示[/b]：多条通知叠放在一起，最新的在最前面。把鼠标停在后面的通知上，它会移到最前面并显示更多文字。[/*]
[*][b]不碍事[/b]：通知不会抢走键盘焦点；鼠标停在工具按钮上或打开全屏窗口时，通知会隐藏。[/*]
[*][b]悬停保留，点击打开[/b]：鼠标停在通知上时，通知不会消失。如果模组为通知设置了可打开的内容，点击通知就会打开它；小 x 只关闭通知。[/*]
[*][b]重复合并[/b]：再次发送相同的通知时，会计数（x2、x3 等），而不会新增一个方框。[/*]
[*][b]不会堆积[/b]：默认最多同时显示 4 条通知，其余的排队等候。[/*]
[*][b]工具按钮整齐排列[/b]：HUD 左侧的按钮列最多四个高，其余的按从上到下的顺序排到右边的新列；手机和背包保持原位。[/*]
[/list]

Toolkit 不会往存档里写入任何内容，也不会修改游戏文件。

支持英文和中文。

[size=5]已知问题[/size]
[list]
[*]通知会挡住下面的内容。通知下方的游戏按钮要等通知消失后才能点击。如果通知挡路，可以用 Position 设置移动它们。[/*]
[*]因面板打开而隐藏的通知仍在倒计时，所以时间短的通知可能在面板关闭前就已结束。[/*]
[/list]

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.19131。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
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
如需移动通知、修改停留时间或显示数量，或关闭通知，可以在游戏内 Mod Manager 的「模组」界面修改，改动立即生效；也可以在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.exiledtoolkit.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\exiled-toolkit.log[/font]。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。
即将推出：Survival Log 维基，收录我所有模组的详细说明。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit]github.com/AnExiledDev/SurvivalLog-ExiledToolkit[/url]
