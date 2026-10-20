// TypingSettings.cs
// ---------------------------------------------------------------------------
// The TYPING DIFFICULTY the player picks on the Start panel (HUD), saved
// between sessions (PlayerPrefs). It only changes how enemy words LOOK and
// how exactly they must be typed; the level itself (map, waves, speed) is the
// same. Every word starts as a plain UPPERCASE word from WordBank and goes
// through Style() before an enemy gets it:
//
//   EASY     lowercase letters only. Typing ignores case (Caps Lock is fine).
//            The words are kept UPPERCASE inside and shown in lowercase.
//   MEDIUM   mostly Title Case: "Water", and compound words with a capital
//            on each part: "SunFlower", "WaterFall". Some lowercase and a few
//            short UPPERCASE words. Case-sensitive: typed exactly as shown.
//   HARD     + punctuation, like a typing test: "water,", "Water.", "help?",
//            "world's". PURPLE SHIFTERS appear: their word changes if you
//            wait too long (start typing it to lock it).
//   EXTREME  + symbols: "#water", "(Paper)", "Rate*", "help?!", "Sp@rk".
//            Shifters appear more often and change faster.
//
// A word NEVER contains a digit: the number keys are the powers and combo
// weapons (1-4, see TypingController), and TypingController ignores every
// digit key while typing. A word never contains '<' or '>' (TextMeshPro would
// read them as rich-text tags) or spaces.
//
// A styled word keeps the FIRST LETTER rule (WordBank): a symbol in front
// ("#water", "(paper)") is only used when no other word on screen starts with
// that symbol, so typing a first character still points at one target.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public enum TypingDifficulty
{
    Easy,
    Medium,
    Hard,
    Extreme
}

public static class TypingSettings
{
    public const int DifficultyCount = 4;

    private const string PrefKey = "TypingDifficulty";

    private static bool loaded;
    private static TypingDifficulty difficulty;

    // The difficulty picked on the Start panel (Easy until the player picks one).
    public static TypingDifficulty Difficulty
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                int saved = PlayerPrefs.GetInt(PrefKey, (int)TypingDifficulty.Easy);
                difficulty = (TypingDifficulty)Mathf.Clamp(saved, 0, DifficultyCount - 1);
            }
            return difficulty;
        }
        set
        {
            loaded = true;
            difficulty = value;
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    // Easy accepts either case; every other difficulty wants the exact word.
    public static bool IsCaseSensitive
    {
        get { return Difficulty != TypingDifficulty.Easy; }
    }

    // ---- Word-changing enemies (ZombieKind.Shifter) ----

    // Hard and Extreme bring PURPLE SHIFTERS.
    public static bool HasShifters
    {
        get { return Difficulty >= TypingDifficulty.Hard; }
    }

    // One normal zombie in this many is a shifter.
    public static int ShifterEvery
    {
        get { return Difficulty == TypingDifficulty.Extreme ? 3 : 4; }
    }

    // A shifter nobody is typing gets a new word every this many seconds.
    public static float ShifterSeconds
    {
        get { return Difficulty == TypingDifficulty.Extreme ? 3.5f : 4.5f; }
    }

    // ---- Shown on the Start panel and the end screen ----

    public static string Name(TypingDifficulty level)
    {
        switch (level)
        {
            case TypingDifficulty.Easy: return "EASY";
            case TypingDifficulty.Medium: return "MEDIUM";
            case TypingDifficulty.Hard: return "HARD";
            default: return "EXTREME";
        }
    }

    public static string Description(TypingDifficulty level)
    {
        switch (level)
        {
            case TypingDifficulty.Easy:
                return "lowercase letters only     <color=#9AA0AA>e.g.</color>  water   house   friend";
            case TypingDifficulty.Medium:
                return "mostly Title Case, some lowercase and CAPS     <color=#9AA0AA>e.g.</color>  House   SunFlower   water";
            case TypingDifficulty.Hard:
                return "+ punctuation, and words that CHANGE     <color=#9AA0AA>e.g.</color>  water,   House.   world's";
            default:
                return "+ symbols, faster shifters     <color=#9AA0AA>e.g.</color>  #water   (House)   Sp@rk   help?!";
        }
    }

    // The colour of each difficulty's button and name.
    public static Color Tint(TypingDifficulty level)
    {
        switch (level)
        {
            case TypingDifficulty.Easy: return new Color(0.35f, 0.8f, 0.45f);
            case TypingDifficulty.Medium: return new Color(0.95f, 0.75f, 0.1f);
            case TypingDifficulty.Hard: return new Color(1f, 0.45f, 0.15f);
            default: return new Color(0.9f, 0.2f, 0.25f);
        }
    }

    // ---- Styling a word ----

    // Hard: punctuation after the word (a comma most often, as in real text).
    private static readonly string[] EndPunctuation = { ",", ",", ",", ".", ".", "!", "?", ";", ":", "'s" };

    // Extreme: a symbol in front, a pair around the word, a symbol after it, or
    // a symbol in place of a letter. Never a digit (see the top of this file).
    private static readonly string[] FrontSymbols = { "#", "@", "$", "&" };
    private static readonly string[][] Wraps =
    {
        new[] { "(", ")" }, new[] { "\"", "\"" }, new[] { "'", "'" }, new[] { "[", "]" }, new[] { "{", "}" }
    };
    private static readonly string[] EndSymbols = { "+", "*", "=", "~", "^", "-", "?!", "!!", "..." };

    // Extreme: letters that can be swapped for a look-alike symbol ("Sp@rk").
    private const string SwapLetters = "asi";
    private const string SwapSymbols = "@$!";

    // A WordBank word (UPPERCASE A-Z) styled for the current difficulty.
    //   used: the first characters already on screen (UPPERCASE, see
    //   WaveSpawner.UsedFirstLetters). With it, Extreme may put a symbol in
    //   front of the word when nothing else starts with that symbol; without
    //   it, the word always starts with its own first letter.
    // A word that already has lowercase letters was styled before (a word
    // chain pair, see StyleChainPair) and is returned unchanged.
    public static string Style(string word, List<char> used = null)
    {
        if (!IsCaseSensitive || string.IsNullOrEmpty(word) || HasLowercase(word))
        {
            return word;
        }

        // A PHRASE (a Brute's "COLD DARK NIGHT"): each word styled on its own;
        // only the first one may get a symbol in front.
        if (word.IndexOf(' ') >= 0)
        {
            string[] parts = word.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = Style(parts[i], i == 0 ? used : null);
            }
            return string.Join(" ", parts);
        }

        if (used != null)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                string styled = StyleOnce(word, true);
                if (!used.Contains(char.ToUpperInvariant(styled[0])))
                {
                    return styled;
                }
            }
        }
        return StyleOnce(word, false);
    }

    // A tiny FRENZY word (1-4 letters): only its case changes, never symbols,
    // so a frenzy always stays quick to type.
    public static string StyleShort(string word)
    {
        if (!IsCaseSensitive || string.IsNullOrEmpty(word) || HasLowercase(word))
        {
            return word;
        }
        float lowerChance = Difficulty == TypingDifficulty.Medium ? 0.35f : 0.6f; // Medium is mostly Title Case
        return Random.value < lowerChance ? word.ToLowerInvariant() : TitleCase(word);
    }

    // A WORD CHAIN pair ("hunt" / "hunter"): the long word must start with the
    // short one exactly, so both get the same case and no punctuation. Only
    // Easy and Medium have word chains (returns null otherwise).
    public static string[] StyleChainPair(string[] pair)
    {
        if (pair == null || Difficulty >= TypingDifficulty.Hard)
        {
            return null;
        }
        if (Difficulty == TypingDifficulty.Easy)
        {
            return pair;
        }
        bool title = Random.value < 0.7f; // Medium is mostly Title Case
        return new[]
        {
            title ? TitleCase(pair[0]) : pair[0].ToLowerInvariant(),
            title ? TitleCase(pair[1]) : pair[1].ToLowerInvariant()
        };
    }

    // How a word is shown: as stored when case matters, otherwise in lowercase.
    public static string Shown(string word, bool caseSensitive)
    {
        return caseSensitive ? word : word.ToLowerInvariant();
    }

    private static string StyleOnce(string word, bool allowFront)
    {
        string cased = Casing(word);
        switch (Difficulty)
        {
            case TypingDifficulty.Medium:
                return cased;
            case TypingDifficulty.Hard:
                return Random.value < 0.5f ? cased : Punctuate(cased);
            default:
                return Extreme(cased, allowFront);
        }
    }

    // lowercase, Title Case (or a compound's TitleParts) or UPPERCASE (short
    // words only); Extreme also mIxEs it. Medium leans on Title Case.
    private static string Casing(string word)
    {
        string lower = word.ToLowerInvariant();
        int roll = Random.Range(0, 100);
        switch (Difficulty)
        {
            case TypingDifficulty.Medium:
                if (roll < 25) return lower;
                if (roll < 90 || word.Length > 5) return TitleVariant(word, 0.6f);
                return word;
            case TypingDifficulty.Hard:
                if (roll < 45) return lower;
                if (roll < 88 || word.Length > 6) return TitleVariant(word, 0.35f);
                return word;
            default:
                if (roll < 40) return lower;
                if (roll < 72) return TitleVariant(word, 0.35f);
                if (roll < 86 && word.Length <= 6) return word;
                return MixedCase(lower);
        }
    }

    private static string Punctuate(string word)
    {
        string mark = EndPunctuation[Random.Range(0, EndPunctuation.Length)];
        if (mark == "'s" && (word.EndsWith("s") || word.EndsWith("S")))
        {
            mark = ",";
        }
        return word + mark;
    }

    private static string Extreme(string word, bool allowFront)
    {
        int roll = Random.Range(0, 100);
        if (roll < 12)
        {
            return word;
        }
        if (roll < 32)
        {
            return Punctuate(word);
        }
        if (roll < 52)
        {
            return word + EndSymbols[Random.Range(0, EndSymbols.Length)]; // Rate*  help?!
        }
        if (roll < 74)
        {
            return SwapForSymbol(word);                        // Sp@rk  gla$s  qu!t
        }
        if (!allowFront)
        {
            return Punctuate(word);
        }
        if (roll < 87)
        {
            return FrontSymbols[Random.Range(0, FrontSymbols.Length)] + word; // #water
        }
        string[] wrap = Wraps[Random.Range(0, Wraps.Length)];
        return wrap[0] + word + wrap[1];                       // (paper)
    }

    // One a / s / i (never the first letter, so the word still starts with its
    // own letter) becomes @ / $ / !. A word with none of them gets a symbol
    // after it instead.
    private static string SwapForSymbol(string word)
    {
        List<int> spots = new List<int>();
        for (int i = 1; i < word.Length; i++)
        {
            if (SwapLetters.IndexOf(char.ToLowerInvariant(word[i])) >= 0)
            {
                spots.Add(i);
            }
        }
        if (spots.Count == 0)
        {
            return word + EndSymbols[Random.Range(0, EndSymbols.Length)];
        }
        int spot = spots[Random.Range(0, spots.Count)];
        char symbol = SwapSymbols[SwapLetters.IndexOf(char.ToLowerInvariant(word[spot]))];
        return word.Substring(0, spot) + symbol + word.Substring(spot + 1);
    }

    // Title Case, or for a compound word ("SUNFLOWER") a capital on each part
    // ("SunFlower") with this chance.
    private static string TitleVariant(string word, float partsChance)
    {
        int split = WordBank.CompoundSplit(word);
        if (split > 0 && Random.value < partsChance)
        {
            return TitleCase(word.Substring(0, split)) + TitleCase(word.Substring(split));
        }
        return TitleCase(word);
    }

    private static string TitleCase(string word)
    {
        string lower = word.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
    }

    // Random letters uppercase, at least one of each case when it can.
    private static string MixedCase(string lower)
    {
        char[] letters = lower.ToCharArray();
        int offset = Random.Range(0, 2);
        for (int i = offset; i < letters.Length; i += 2)
        {
            letters[i] = char.ToUpperInvariant(letters[i]);
        }
        return new string(letters);
    }

    private static bool HasLowercase(string word)
    {
        foreach (char character in word)
        {
            if (char.IsLower(character))
            {
                return true;
            }
        }
        return false;
    }
}
