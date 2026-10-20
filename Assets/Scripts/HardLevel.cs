// HardLevel.cs
// ---------------------------------------------------------------------------
// Present only in Hard.unity (the Hollow Forest). Enemies there move faster
// and normal zombies carry long words. Normal scenes keep their original rules.
//
// How words look and how exactly they must be typed (case, punctuation,
// symbols) and the word-changing PURPLE SHIFTERS come from the TYPING
// DIFFICULTY the player picks on the Start panel (TypingSettings), in every
// level, this one included.
// ---------------------------------------------------------------------------
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class HardLevel : MonoBehaviour
{
    public static HardLevel Active { get; private set; }
    [Min(1f)] public float enemySpeedMultiplier = 1.30f;

    private void Awake() { Active = this; }
    private void OnDestroy() { if (Active == this) Active = null; }
}
