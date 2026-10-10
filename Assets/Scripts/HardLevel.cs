using UnityEngine;

// Present only in Hard.unity. Normal scenes retain their original rules.
[DefaultExecutionOrder(-1000)]
public class HardLevel : MonoBehaviour
{
    public static HardLevel Active { get; private set; }
    [Min(1f)] public float enemySpeedMultiplier = 1.30f;
    [Min(1f)] public float wordChangeSeconds = 4.5f;
    private int normalSpawns;

    private void Awake() { Active = this; }
    private void OnDestroy() { if (Active == this) Active = null; }

    public bool NextIsShifter() { return normalSpawns++ % 4 == 0; }

    // Mutually exclusive word styles, rolled independently for each new word:
    // 35% lowercase, 40% title case, 15% mixed case, 10% punctuation.
    // Only casing changes: spelling is preserved. No power digits or TMP tags.
    public static string ComplexWord(string source)
    {
        return FormatWord(source, Random.Range(0, 100));
    }

    private static string FormatWord(string source, int styleRoll)
    {
        string lower = source.ToLowerInvariant();
        if (styleRoll < 35) return lower;
        string title = char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        if (styleRoll < 75) return title;
        if (styleRoll < 90)
        {
            char[] letters = lower.ToCharArray();
            int offset = Random.Range(0, 2);
            for (int i = offset; i < letters.Length; i += 2)
                letters[i] = char.ToUpperInvariant(letters[i]);
            return new string(letters);
        }
        string[] punctuation = { "!", "?", ".", ";", ":", "-" };
        return title + punctuation[Random.Range(0, punctuation.Length)];
    }
}
