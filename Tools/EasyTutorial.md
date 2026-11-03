# Stationary tutorial

Open `Assets/Scenes/Menu.unity` and select **Easy / Tutorial**, or open `Assets/Scenes/Easy.unity` directly.

Easy uses one dark practice arena. The player stays in the same place throughout. Rebuild its scene with `./Tools/CreateEasyScene.ps1`; scene and script GUIDs stay stable. Normal retains the campus route; Hard retains the forest route and its 30% speed increase.

| Step | On-screen cue | Actual learning task |
| --- | --- | --- |
| 1 | TYPE THE WORD! / KILL THE ENEMY | Type the word above one stationary enemy. |
| 2 | SHIELD / TAP TAB | Open the shield prompt, spell its word, and watch the next enemy hit the shield. |
| 3 | LURE BOMB / TAP 1 | Watch a real bomb attract and explode; tap to spend 4 energy and repeat. |
| 4 | FREEZE / TAP 2 | Tap to spend 5 energy, freeze the practice group, then type their words. |
| 5 | FRENZY / TAP 3 | Spend 10 skill energy and activate with one press, then type the shortened words. |
| 6 | RPG / TAP 4 | Spend 15 skill energy and load with one press, then finish a word to fire. |

Only the current control is shown and enabled. The large key card also works as a touch button. Energy skills show their exact price and current energy; insufficient energy is red, and active effects remain highlighted. Pressing 3 or 4 activates immediately, with no purchase, inventory, hold timer, or per-wave purchase limit. Each real kill earns one energy, which is independent of the scoring combo and survives mistakes and damage. Using a skill never reduces combo, score, or best-combo statistics. REPLAY repeats a demonstration and resets its practice group.

Demonstrations use real powers, projectiles, word transformations, shield blocks and explosions. Bomb attraction includes moving direction markers. Demonstration kills do not add player kills or score. Practice completion requires actual power events and cleared enemies, not a timer or a next button. Existing typing and power analytics remain; `tutorial_step` reports `demo`, `practice` and `complete` phases.

After all six guided steps, the coaching overlay closes and the normal health, score, combo and weapon HUD returns. On phones, all five power buttons become visible and clickable again. One ordinary Wave 1 runs in the same stationary arena, with normal damage and power durations; all controls are unlocked. Initial supplies and at least 25 energy are granted once, with no automatic refills during combat. The tutorial finishes only after that wave's spawns and enemies have cleared; there is no second wave or boss. Losing uses the normal retry screen.

The guided steps are safe: failed attempts do not remove health. Guided supplies and at least 25 energy refill every three seconds. Freeze and Frenzy practice effects last 15 seconds; the shield spelling prompt allows 30 seconds and can be reopened. The tutorial temporarily uses simple case-insensitive words and disables adaptive pressure without changing saved preferences. Pausing suspends the demonstration; skills only activate while playing.

All levels remove large paused instruction panels; optional first-time cues are short and nonblocking. During guided steps, each moment has only a short cue and the relevant word or key. Ordinary Normal/Hard HUD, controls and difficulty settings retain their existing behavior.

All four numbered skills use the same independent energy: Lure Bomb 6, Freeze 8, Frenzy 12, RPG 18. Energy comes only from words typed to the end (1 per kill; blast kills earn none) and is capped at 20. Bomb aiming is free; energy is charged on release, and release rechecks affordability. Pausing cancels the aim without a charge. Active Freeze cannot charge again. Lure and Freeze crates grant 4 and 5 shared energy respectively. The main energy number is now 56-point text in its own line; combo remains the score multiplier and best-combo record.
