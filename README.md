# Gangster Coin Pusher Plugin
Collection of fixes for the game "Gangster Coin Pusher":

- Fixes collection of dolls/props not saving between sessions. Considering how many of them exist and that they are required for upgrading the cannon the collection was clearly not ment to be session-only.
- Fixes the Lucky Wheel dropping "Pig Skill" or "AdvSkill" which are both basically dud "rewards".
- Fixes the equipped skill breaking the game completely once the skill is fully upgraded.
- Fixes Steam achievements not unlocking directly after achieving them.
- Fixes the audio setting not saving. Small caveat: there's still about half a second of music on startup.
- Fixes the game not running in the background on Alt+Tab. Also mutes the game when it's not focused.
- Fixes community task 21 to require 200 dropped gold coins instead of the outrageous 20,000
- Fixes the "Upgrade Hood" button showing even if the Hood is already at maximum level
- Fixes the silver coin display to no longer cut off text
- Fixes the player names on the rank screen not scaling properly
- Fixes an endless loop in the Daily Rewards window when the user tries to close it on day 8+
- Moved the translation data outside of the game files and fixed some translations to be less terrible (work in progress). Locale files can be found in /Localization and belong into the %gamedir%\Gangster coin pusher_Data\StreamingAssets directory.

# Installation

Download the latest release and unpack the archive into the game directory. 

# Building

Clone the repository and add a Libs folder to it containing the following files. All of them can be found in %gamedir%\Gangster coin pusher_Data\Managed.
- Assembly-CSharp.dll
- com.rlabrecque.steamworks.net.dll
- UnityEngine.dll
- UnityEngine.CoreModule.dll
- UnityEngine.UI.dll

 From [BepInEx](https://github.com/BepInEx/BepInEx) download the x64 5.4.21.0 release and copy **BepInEx.dll** into your Libs folder as well.

 Download the release from [MonoMod](https://github.com/MonoMod/MonoMod), unpack and use MonoMod.RuntimeDetour.HookGen.exe to create MMHOOK_Assembly-CSharp.dll:

 ```
MonoMod.RuntimeDetour.HookGen.exe --private %gamedir%\Gangster coin pusher_Data\Managed\Assembly-CSharp.dll
```

Copy the resulting DLL file to your Libs directory.

Load the project in Visual Studio 2019 and add all DLLs in your Libs directory as references. Press F6 to build.

# Acknowledgements

Uses [Doorstop](https://github.com/NeighTools/UnityDoorstop) and [BepInEx](https://github.com/BepInEx/BepInEx)
