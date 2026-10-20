# Easy training academy

Open `Assets/Scenes/Menu.unity` and select **Easy / Training Academy**, or open `Assets/Scenes/Easy.unity` directly. The typing difficulty on a level's Start panel changes spelling rules only; it does not load a different scene.

Easy has its own six-station return route. Normal retains the campus route, and Hard retains the dark forest route and 30% speed increase. Rebuild only Easy with `./Tools/CreateEasyScene.ps1`; scene and script GUIDs stay stable.

| Wave | Enemies | Required action |
| --- | --- | --- |
| 1 | Normal, Runner | Aim and throw a lure bomb |
| 2 | Armored, Explosive | Use Freeze |
| 3 | Shifter, Ghost | Hold to buy Frenzy, then use it |
| 4 | Splitter, Splitling, Spitter | Hold to buy RPG, load it, then finish a target word to fire |
| 5 | Brute, linked Normal pair | Open Shield and finish its word |
| 6 | Boss and its summoned enemies | Final practice |

Each wave adds 25 spendable combo and refills bomb, freeze and shield supplies. While a required lesson is unfinished, supplies and a minimum 25 combo recover every three seconds. Purchase limits are reset for these retries, only in Easy. Completing a wave also requires clearing its enemies. If the RPG lesson runs out of targets, a slow practice enemy appears. Tool actions from previous waves do not satisfy a new lesson. Chains are still introduced if the saved typing difficulty normally disables them.

The on-screen objective stays visible even with optional tips disabled, and follows the usable HUD area on phones. Enemy introductions are deterministic, move slowly, and have no ride ambushes or overhead-camera waves. The tutorial uses a dedicated component; the old elite/word-swapping/speed-adjustment components are not attached to the new scene.

Existing kill, typing, damage and power analytics remain in place. Teaching allowances do not directly add kills, score or correct keystrokes. `tutorial_lesson_start` and `tutorial_lesson_complete` identify the teaching steps; RPG shots also report the `power` action `fire`.
