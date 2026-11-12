Name: Mod Manager
Summary: An in-game Mods screen. See every installed mod, turn mods on or off, change their settings, install updates and browse Nexus mods without leaving the game. It also warns about mods that may not work with your game version, newer versions on Nexus and mods installed twice or in the wrong folder. Works with every BepInEx mod. Needs Exiled's Toolkit.

Description (BBCode):
[center][img]https://sldb.core-directive.com/media/save/1-save-safe.png[/img][/center]

Mod Manager adds a Mods screen to Survival Log. It lists every BepInEx mod you have installed, shows whether each one loaded, and lets you change their settings, turn them on or off, uninstall them and install their updates, all without opening a .cfg file. Open it with the Mods button in the Esc menu or on the title screen, or set a key for it (OpenKey in the config, unbound by default).

[size=5]Main features[/size]
[list]
[*][b]Every mod in one list[/b]: icon, version and DLL for each mod, with the ones that did not start listed apart along with BepInEx's load errors. Shared libraries sit in a folded group at the bottom.[/*]
[*][b]Change settings in-game[/b]: switches, sliders, number boxes, drop-down lists, text boxes and key capture for hotkeys. Each change is saved to the mod's .cfg file at once.[/*]
[*][b]Knows what needs a restart[/b]: changed settings that apply at the next start are marked NEEDS RESTART. Settings a mod applies at once get no mark.[/*]
[*][b]Item pickers[/b]: settings that hold game items or categories show the items themselves, with pictures and names, instead of id numbers. Item lists show as cards you swap, drag and count up or down, and ADD ITEM opens a searchable window of every item in the game, sorted by category, with each item's weight and value. A crafting recipe shows what it makes and what it is worth, and a list of what dismantling gives back warns you when it would give back more than the recipe is worth. A recipe list also warns you when another recipe in the game already uses exactly those materials, since two recipes can't share them.[/*]
[*][b]Mod lists[/b]: a mod can give a list of its own to pick from, such as buffs, beds or furniture actions. It shows as a list, one row per entry with its picture, name and what it does, and an entry can carry a choice you pick on its row or a number you step. ADD opens a searchable window of everything the mod lists, filed under the mod's groups, with an Add button on each row. Entries you have are marked, with a "You have" group of their own.[/*]
[*][b]Settings for one world[/b]: when a mod lets each world keep its own value of a setting, the Mods screen shows, while you play, whether this world keeps its own. THIS WORLD ONLY makes a value this world's own, and USE GLOBAL sends it back to the value every other world uses.[/*]
[*][b]Search, reset, clashes[/b]: find any setting across all mods, see which ones differ from the default and put them back, and spot hotkeys two mods share.[/*]
[*][b]Errors tab[/b]: warnings and errors logged this session show up under the mod they came from, with an ERROR or WARNING mark in the list and an "Errors" filter. When an error went through game code another mod also changes, the tab names that mod. Errors from BepInEx or the game itself are listed on the HEALTH page.[/*]
[*][b]Copy mod list[/b]: one button copies every mod with its version and state, and this session's errors, ready to paste into a bug report.[/*]
[*][b]Turn mods on or off[/b]: a switch on each mod's page, applied at the next start. It warns about mods that need the one you turn off, and turns on what a mod needs with one button.[/*]
[*][b]Uninstall[/b]: UNINSTALL moves a mod, and its settings file if you want, to a trash folder. It first tells you whether the mod can get your save ready to load without it, and which mods stop with it. The TRASH page lets you RESTORE a mod, or EMPTY TRASH to delete them for good.[/*]
[*][b]Changes waiting for a restart[/b]: a bar at the bottom lists every change that applies at the next start, with UNDO for each and QUIT GAME to quit and use them. The game saves your run as it does when you close its window.[/*]
[*][b]Health check[/b]: a HEALTH page shows what is wrong with how your mods are installed: the same mod or DLL twice, a folder unpacked one level too deep, a BepInEx build these mods are not made for, a mod that needs a newer Exiled's Toolkit, and settings files left from mods you removed.[/*]
[*][b]Browse[/b]: a BROWSE page lists every mod for the game on Nexus, with search, filters and sorting. Mods whose authors set them up for the Mod Manager install with one click, along with the mods they need, and UNDO takes them out again. The rest are marked as manual downloads, with an ASK THE AUTHOR button that copies a short request for Mod Manager support and opens the mod's posts on Nexus for you to paste it.[/*]
[*][b]Profiles[/b]: save a setup (which mods are on and every setting you changed) and go back to it any time. COPY CODE gives a short code to share; paste one, or a mod list, and the PROFILES page shows everything it would change before it changes anything, installs what it can from BROWSE, and links the rest on Nexus.[/*]
[*][b]Dependencies[/b]: each mod's page lists what it needs (with the version it asks for), what it can use, what it does not work with, and which mods need or use it. A missing, off or wrong-version mod is marked red.[/*]
[*][b]Update checks and installs[/b]: at each start it asks GitHub and the Survival Log Wiki which mods have a newer release and shows one message saying how many. INSTALL UPDATE downloads the new version and puts it in place for the next start, keeping the old files. Turn on WikiDownloads and my mods download from the wiki first, so they update even where GitHub is slow or blocked. INSTALL ALL UPDATES does every mod at once and marks any update it stopped, the page shows the notes of every release since yours, and ROLL BACK puts the old version back if the new one gives you trouble. Each update is checked first: it must be the same mod, from the same GitHub repository as before or the exact file the wiki lists, with no program files. An update that newly starts programs, loads other code or uses the registry is refused, and one that can do something else the old version could not (like connect to the internet) asks you before it installs, unless it is the exact file the wiki lists.[/*]
[*][b]Conflicts tab[/b]: each mod's page lists the other mods that patch the same parts of the game, marked LIKELY TO CLASH, MAY CLASH or PROBABLY HARMLESS, so you know where to look first when two mods misbehave together. Mods known not to work together are listed at the top.[/*]
[*][b]Nexus Mods versions[/b]: mods from Nexus show when Nexus has a newer version, with a NEXUS button to the mod's page there. It asks Nexus at most once a day and needs no account.[/*]
[*][b]Known broken versions[/b]: a mod version known not to work gets a BROKEN mark with the reason, and an update to such a version is marked HELD and never installed.[/*]
[*][b]Changelog tab[/b]: a mod that links its changelog gets a CHANGELOG tab that shows it right on the mod's page.[/*]
[*][b]Tabs from other mods[/b]: a mod can add a tab with its own page to its entry. New Loop Plus adds TALENTS, an editor for its loop rewards.[/*]
[*][b]Game version warning[/b]: a mod not known to work on your game version gets a grey mark, which turns red if it logs errors. Mod authors can list newer game versions their mod works on without a new release. A mod that uses game code your game version no longer has gets an OUTDATED mark, and its page lists what is missing.[/*]
[/list]

The Mod Manager adds nothing to your save and does not edit game files. It only goes online for the checks at each start (GitHub, the Survival Log Wiki, and Nexus Mods once a day), for update downloads and for changelogs. It needs no account or key, and sends none of your files, settings or saves.

[size=5]For mod authors[/size]
Your mod shows up with no work at all. To give it a name in both languages, an icon, a banner, a category, links, item pickers, live settings, update installs and more, add a few lines of metadata to your .csproj; nothing needs a reference to the Mod Manager. The [url=https://github.com/AnExiledDev/SurvivalLog-ModManager#for-mod-authors]full guide is in the README[/url].

Works in English and Chinese.

[size=5]Known issues[/size]
[list]
[*]It only shows settings that mods register with BepInEx.[/*]
[*]Many mods read a setting only when the game starts. Such changes are saved at once and used after the next start.[/*]
[*]The Mod Manager and Exiled's Toolkit cannot be turned off from the screen. Delete their DLLs to remove them.[/*]
[*]While a mod's update downloads, and after an install or rollback until the next start, its switch is locked.[/*]
[*]Update checks only cover mods that name a GitHub repository, and INSTALL UPDATE needs the release to carry a .zip with the mod's DLL. Otherwise GET UPDATE opens the release page.[/*]
[*]GitHub allows 60 requests an hour without an account. Past that, release notes and installs say to try again later.[/*]
[*]A Nexus version is only shown when the mod's DLL is the one uploaded to Nexus. With many mods from Nexus, some show their version a day or two later.[/*]
[*]An uninstalled mod keeps running until you quit the game.[/*]
[/list]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.20719. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.19.0 or later. The Mods screen does not open without it. Its UI size setting also draws the Mods screen bigger or smaller, and Mod Manager's own UiScale setting can set a size for this mod alone.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Install Exiled's Toolkit 0.19.0 or later.[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ModManager.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]ModManager.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder. Settings you changed with it stay in the other mods' .cfg files.

[size=5]Configuration[/size]
The Mod Manager's own settings (the key that opens it, update checks, downloads from the wiki, the update message) can be changed on the Mods screen, or in [font=Courier New]BepInEx\config\com.dev.survivallog.modmanager.cfg[/font] while the game is closed (see [url=https://github.com/AnExiledDev/SurvivalLog-ModManager/blob/main/CONFIG.md]CONFIG.md[/url]; the cfg file explains each setting in English and Chinese).

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or leave a comment here. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and [font=Courier New]BepInEx\mod-manager.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].
Check out my [url=https://sldb.core-directive.com/en/]Survival Log wiki[/url] for items, crafting, maps and more.

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-ModManager]github.com/AnExiledDev/SurvivalLog-ModManager[/url]

[line]
[size=5]简体中文[/size]

Mod Manager 为 Survival Log 加入一个模组界面。它列出你安装的每个 BepInEx 模组，显示每个模组是否已加载，并让你修改设置、开启或关闭模组、卸载模组、安装更新，全程不用打开 .cfg 文件。点击 Esc 菜单或标题画面中的「模组」按钮即可打开，也可以为它设置按键（配置中的 OpenKey，默认不绑定）。

[size=5]主要功能[/size]
[list]
[*][b]所有模组一览[/b]：每个模组都显示图标、版本和 DLL；没有启动的模组单独列出，并附上 BepInEx 报告的加载错误。共用的前置库折叠在列表底部。[/*]
[*][b]游戏内修改设置[/b]：开关、滑块、数字框、下拉列表、文本框，以及热键的按键录入。每次修改都会立即保存到模组的 .cfg 文件。[/*]
[*][b]知道哪些需要重启[/b]：下次启动才生效的已修改设置会标记为「需要重启」。模组立即应用的设置没有标记。[/*]
[*][b]物品选择器[/b]：保存游戏物品或物品类别的设置会直接显示物品的图片和名称，不用再输入编号。物品清单以卡片显示，可以替换、拖动排序、加减数量；「添加物品」会打开游戏全部物品的窗口，按类别分好，可以搜索，并显示每件物品的重量和价值。制作配方会显示做出的物品和配方的总价值；拆除返还的清单在返还价值高于配方时会提醒你。如果游戏里另一个配方已经用了完全相同的材料，配方清单也会提醒你，因为两个配方不能用一样的材料。[/*]
[*][b]模组清单[/b]：模组可以提供自己的清单供你挑选，例如状态、床或家具行动。它以列表显示，每项一行，带图片、名称和作用；每一项还可以带一个在行上选择的选项，或一个可加减的数值。「添加」会打开一个可搜索的窗口，按模组的分组列出所有项目，每行都有添加按钮。你拥有的项目会被标出，并有自己的「你有的」分组。[/*]
[*][b]每个世界自己的设置[/b]：模组允许每个世界保存自己的设置值时，游戏中「模组」界面会标明当前世界是否用自己的值。THIS WORLD ONLY 让这个值只属于当前世界，USE GLOBAL 让它恢复使用其他世界共用的值。[/*]
[*][b]搜索、重置、冲突[/b]：在所有模组中搜索任意设置，查看哪些与默认值不同并一键恢复，还能发现两个模组共用的热键。[/*]
[*][b]「错误」页[/b]：这次游戏中记录的警告和错误会显示在产生它的模组下，列表中标出「错误」或「警告」，还可以用「错误」筛选。如果错误经过了另一个模组也修改的游戏代码，会写出那个模组。来自 BepInEx 或游戏本身的错误列在「安装检查」页上。[/*]
[*][b]复制模组列表[/b]：一个按钮即可复制所有模组及其版本和状态，以及这次游戏的错误，方便粘贴到问题反馈中。[/*]
[*][b]开启或关闭模组[/b]：每个模组页面上都有开关，下次启动时生效。关闭模组时会提醒哪些模组需要它；一个按钮就能开启某个模组需要的模组。[/*]
[*][b]卸载[/b]：「卸载」会把模组移到回收站，也可以连同它的设置文件一起移走。卸载前会告诉你这个模组能否先让存档在没有它时也能读取，以及哪些模组会随之停止。在「回收站」页面可以「恢复」模组，或用「清空回收站」永久删除。[/*]
[*][b]等待重启的改动[/b]：底部的栏会列出所有要在下次启动时生效的改动，每项都可以「撤销」，「退出游戏」则直接退出以使用这些改动。游戏会像关闭窗口时一样保存你的进度。[/*]
[*][b]安装检查[/b]：「安装检查」页会列出模组安装上的问题：同一个模组或 DLL 装了两次、解压时多套了一层文件夹、不是这些模组所用的 BepInEx 版本、需要更新版 Exiled's Toolkit 的模组，以及已删除模组留下的设置文件。[/*]
[*][b]浏览[/b]：「浏览」页列出 Nexus 上本游戏的所有模组，可搜索、筛选和排序。作者为模组管理器做好设置的模组一键即可安装，所需的模组也会一起装上，「撤销」可以再移除。其他模组标为需要手动下载，并有「请作者支持」按钮：它会复制一段请求模组管理器支持的简短留言，并打开该模组在 Nexus 上的帖子页面，由你粘贴发送。[/*]
[*][b]配置方案[/b]：保存一套配置（哪些模组开启，以及你改过的每项设置），随时换回去。「复制代码」会生成一段可分享的短代码；粘贴代码或模组列表后，「配置方案」页会先列出它将改变的所有内容，再进行更改，能从「浏览」安装的模组会自动安装，其余的给出 Nexus 链接。[/*]
[*][b]依赖[/b]：每个模组页面会列出它需要的模组（及要求的版本）、可配合的模组、不兼容的模组，以及需要或使用它的模组。缺失、已关闭或版本不符的模组会标红。[/*]
[*][b]检查并安装更新[/b]：每次启动时向 GitHub 和 Survival Log 维基查询哪些模组有新版本，并用一条消息告诉你有几个。「安装更新」会下载新版本，下次启动时生效，旧文件会保留。开启 WikiDownloads 后，我的模组会先从维基下载，所以在 GitHub 很慢或打不开的地方也能更新。「安装全部更新」可一次更新所有模组，并标出它没有安装的更新，页面会显示你的版本之后每次发布的更新说明；如果新版本有问题，「退回」可以换回旧版本。每次更新都会先检查：必须是同一个模组、来自与之前相同的 GitHub 仓库或正是维基列出的文件、不含程序文件。新版本如果开始启动其他程序、加载其他代码或使用注册表，会被拒绝；如果能做到旧版本做不到的其他事（比如连接互联网），安装前会先询问你，除非它正是维基列出的文件。[/*]
[*][b]「冲突」页[/b]：每个模组的页面会列出修改了游戏相同部分的其他模组，并标出「很可能冲突」「可能冲突」或「大概无害」，两个模组一起出问题时可以先从这里查起。已知不能一起使用的模组会列在最上面。[/*]
[*][b]Nexus Mods 版本[/b]：来自 Nexus 的模组在 Nexus 上有新版本时会标出来，并有「NEXUS」按钮打开它在那里的页面。每天最多查询 Nexus 一次，不需要账号。[/*]
[*][b]已知故障的版本[/b]：已知无法正常工作的模组版本会标为「已知故障」并写明原因；更新到这种版本会标为「已暂缓」，永远不会安装。[/*]
[*][b]「更新日志」页[/b]：链接了更新日志的模组会多出一个「更新日志」页，直接在模组页面上显示它。[/*]
[*][b]其他模组的页签[/b]：模组可以在自己的条目中加入一个带有自己页面的页签。New Loop Plus 加入了「天赋」页签，用来编辑它的轮回奖励。[/*]
[*][b]游戏版本提醒[/b]：尚未确认可在你的游戏版本上运行的模组会显示灰色标记；如果它记录了错误，标记会变红。模组作者无需发布新版本，即可列出模组可运行的新游戏版本。如果模组用到的游戏代码在你的游戏版本中已经不存在，它会标为「可能过时」，页面上会列出缺少的部分。[/*]
[/list]

Mod Manager 不会向存档添加任何内容，也不会修改游戏文件。它只在每次启动检查时（GitHub、Survival Log 维基，以及每天一次的 Nexus Mods）、下载更新和查看更新日志时联网。不需要账号或密钥，也不会发送你的任何文件、设置或存档。

[size=5]致模组作者[/size]
你的模组无需任何改动就会显示在列表中。如果想提供中英文名称、图标、横幅、分类、链接、物品选择器、实时生效的设置、一键安装更新等，只需在 .csproj 中加几行元数据，不需要引用 Mod Manager。[url=https://github.com/AnExiledDev/SurvivalLog-ModManager#for-mod-authors]完整说明见 README[/url]（英文）。

[size=5]已知问题[/size]
[list]
[*]只显示模组注册到 BepInEx 的设置。[/*]
[*]很多模组只在游戏启动时读取设置。这类修改会立即保存，但要下次启动后才生效。[/*]
[*]Mod Manager 和 Exiled's Toolkit 无法在界面中关闭。如需移除，请删除它们的 DLL。[/*]
[*]模组更新下载期间，以及安装或回滚之后直到下次启动前，它的开关会被锁定。[/*]
[*]只有标明了 GitHub 仓库的模组才会检查更新；「安装更新」需要发布版本附带包含该模组 DLL 的 .zip 文件，否则「获取更新」会打开发布页面。[/*]
[*]不登录时 GitHub 每小时只允许 60 次请求。超出后，更新说明和安装会提示稍后再试。[/*]
[*]只有模组的 DLL 与上传到 Nexus 的相同时，才会显示 Nexus 版本。来自 Nexus 的模组很多时，有些会晚一两天才显示版本。[/*]
[*]卸载的模组在你退出游戏之前会继续运行。[/*]
[/list]

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.20719。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.19.0 或更高版本。没有它，模组界面无法打开。它的界面大小设置还能调整模组界面的大小，Mod Manager 自己的 UiScale 设置也可以单独调整本模组的大小。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]安装 Exiled's Toolkit 0.19.0 或更高版本。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ModManager.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]ModManager.dll[/font]。用它修改过的设置会保留在其他模组的 .cfg 文件中。

[size=5]配置[/size]
Mod Manager 自己的设置（打开界面的按键、检查更新、从维基下载、更新消息）可以在模组界面中修改，也可以在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.modmanager.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-ModManager/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\mod-manager.log[/font]。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。
也欢迎看看我做的 [url=https://sldb.core-directive.com/zh/]Survival Log 维基[/url]，收录物品、制作、地图等资料。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-ModManager]github.com/AnExiledDev/SurvivalLog-ModManager[/url]
