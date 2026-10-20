// ITypingTarget.cs
// ---------------------------------------------------------------------------
// Anything the player can target by typing its word. TypingController only
// talks to this interface, so it works the same for a Zombie and for one part
// of a Boss (head, arm, leg), and for the boss's EnergyOrb.
// WaveSpawner.LayoutLabels uses it too, to place
// every target's word on screen.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;

public interface ITypingTarget
{
    string Word { get; }
    string ColoredWord { get; }     // the word as rich text: typed part yellow, rest white
    char NextLetter { get; }
    bool IsWordComplete { get; }
    bool IsAlive { get; }           // false = can no longer be typed (dead, removed or broken)
    bool IsTargeted { get; }        // true while the player is typing this word
    // false: typing is case-insensitive; the word is stored UPPERCASE A-Z and shown
    // in lowercase (zombies, boss parts).
    // true: every character must be typed exactly, including case and
    // symbols (the boss's energy orbs, e.g. "Sp@rk").
    bool IsCaseSensitive { get; }
    Vector3 Position { get; }       // used to pick the NEAREST target
    Vector3 HitPoint { get; }       // where a bullet aims (e.g. a zombie's chest)

    // The word on screen (created by HUD.CreateWordLabel) and the world point it
    // is pinned to, e.g. just above a zombie's head.
    TMP_Text Label { get; }
    Vector3 LabelAnchor { get; }

    void AdvanceProgress();
    void ResetProgress();

    // Called by the Bullet fired at this target when it hits (the whole word
    // has been typed): a zombie dies or explodes, a boss part breaks.
    void CompleteWord();
}
