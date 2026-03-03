ORE LIMITER – MineMogul

Version 0.3.0

A configurable Quality-of-Life mod that limits AutoMiner-spawned Ore and Crushed Ore to prevent excessive object buildup and performance degradation in long-running saves.

:: REQUIREMENTS ::

• MineMogul (latest version recommended)
• BepInEx 5 (x64)

Optional:
• BepInEx Configuration Manager (for in-game config UI)

:: FEATURES ::

• Limits only AutoMiner output (Ore + Crushed Ore)
• Does not affect gems, manual mining, or other machines
• Clean integration into Settings → Accessibility
• Toggleable Unlimited mode
• Adjustable numeric limit
• Live enforcement when limits change
• Automatically detects and tracks existing ore on world load
• Uses the game’s native pooling system (safe removal)

:: HOW IT WORKS ::

• On world load, all active Ore and Crushed Ore are detected
• When the limit is exceeded, newest AutoMiner spawns are removed first
• If ore is picked up, processed, or despawned naturally, the count updates correctly
• Optional ResourceType filtering is available via the config file

:: SETTINGS ::

Settings can be configured in two places:

• In-game:
Esc → Settings → Accessibility

• Config file:
BepInEx/config/orelimiter.cfg

If BepInEx Configuration Manager is installed, press F1 to open
Ore Limiter vX.X.X directly.

:: DEBUG OVERLAY (OPTIONAL) ::

An optional debug overlay can be enabled via the config file.

When enabled, it displays:
• Current tracked ore count
• Configured limit
• Number of culled items
• Last removed ore type

Disabled by default. Intended for troubleshooting and verification.

:: COMPATIBILITY ::

• Does not modify save files
• Compatible with existing worlds
• Safe to add or remove mid-save

:: INSTALLATION ::

1. Install BepInEx 5 (x64)
Download BepInEx_win_x64_5.4.23.4.zip
Extract the entire contents directly into your MineMogul game folder:

SteamLibrary\steamapps\common\MineMogul


Run the game once, then close it.

2. (Optional) Install Configuration Manager
Download BepInEx.ConfigurationManager_BepInEx5_v18.4.1.zip
Extract into:

MineMogul\BepInEx\plugins


3. Install Ore Limiter
Download this mod and extract the contents directly into your MineMogul folder.

The mod will be placed automatically in:

MineMogul\BepInEx\plugins\ObjectLimiter


4. Launch the game

:: KNOWN ISSUES ::

• None currently known
• Please report issues on the Nexus page

:: NOTES ::

• This is a Quality-of-Life mod
• No balance changes are made
• No advantage is gained beyond performance stability

:: CREDITS ::

♦ BepInEx team – mod framework
♦ Harmony – runtime patching
♦ MineMogul developers Gvarados & Diamonder <3
♦ Community testers and feedback