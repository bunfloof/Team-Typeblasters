// WordBank.cs
// ---------------------------------------------------------------------------
// The words enemies carry: about a thousand common English words (see
// EnglishWords, like a typing test's word list), split into three buckets:
//   short (3-4 letters), medium (5-7 letters), long (8-12 letters).
// All words here are UPPERCASE A-Z only (no spaces, no punctuation). The
// TYPING DIFFICULTY then styles them (TypingSettings.Style): lowercase on
// Easy, mixed case on Medium, punctuation on Hard, numbers and symbols on
// Extreme. Orb words on Extreme are the hand-made list below.
//
// PickWord() picks a bucket by zombie kind (explosive: long; runner: short;
// armored: medium; normal: about 60% short, 40% medium) and tries to return a
// word whose FIRST LETTER is not already used by anything on screen, so that
// typing a first letter always points at exactly one target.
// Barrels and supply crates have their own word lists further down: their
// word tells what they are (FUEL, MEDKIT, FREEZE...).
//
// To add common words: add them to EnglishWords (the length picks the bucket).
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class WordBank
{
    // The common English words (EnglishWords), sorted by length. Built once
    // in the static constructor below.
    //   short  3-4 letters:  small blast, quick and safe to type.
    //   medium 5-7 letters:  medium blast.
    //   long   8-12 letters: big blast, but a long commitment while the horde closes in.
    //   boss   10-14 letters: only the boss's head.
    private static readonly string[] ShortWords;
    private static readonly string[] MediumWords;
    private static readonly string[] LongWords;
    private static readonly string[] BossWords;

    static WordBank()
    {
        List<string> shortList = new List<string>();
        List<string> mediumList = new List<string>();
        List<string> longList = new List<string>();
        foreach (string word in SplitWords(EnglishWords.Common))
        {
            if (word.Length >= 3 && word.Length <= 4)
            {
                shortList.Add(word);
            }
            else if (word.Length >= 5 && word.Length <= 7)
            {
                mediumList.Add(word);
            }
            else if (word.Length >= 8 && word.Length <= 12)
            {
                longList.Add(word);
            }
        }
        ShortWords = shortList.ToArray();
        MediumWords = mediumList.ToArray();
        LongWords = longList.ToArray();
        BossWords = SplitWords(EnglishWords.Boss).ToArray();
    }

    // Where a compound word splits into its two parts ("SUNFLOWER" -> 3:
    // "SUN" + "FLOWER"), or 0 if it is not one. Used for Title Case like
    // "SunFlower" (TypingSettings). Picked by hand (EnglishWords.Compounds):
    // finding them automatically also splits "carpet" into "CarPet".
    public static int CompoundSplit(string word)
    {
        if (compoundSplits == null)
        {
            compoundSplits = new Dictionary<string, int>();
            foreach (string piece in EnglishWords.Compounds.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string upper = piece.ToUpperInvariant();
                int bar = upper.IndexOf('|');
                if (bar > 0)
                {
                    compoundSplits[upper.Replace("|", "")] = bar;
                }
            }
        }
        int split;
        return compoundSplits.TryGetValue(word, out split) ? split : 0;
    }

    private static Dictionary<string, int> compoundSplits; // built on first use

    // The words of a list (any whitespace between them), UPPERCASE, letters
    // A-Z only, each word once.
    private static List<string> SplitWords(string text)
    {
        List<string> words = new List<string>();
        HashSet<string> seen = new HashSet<string>();
        foreach (string piece in text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string word = piece.Trim().ToUpperInvariant();
            bool lettersOnly = word.Length > 0;
            foreach (char character in word)
            {
                if (character < 'A' || character > 'Z')
                {
                    lettersOnly = false;
                    break;
                }
            }
            if (lettersOnly && seen.Add(word))
            {
                words.Add(word);
            }
        }
        return words;
    }

    // 3-6 characters, for the boss's energy orbs. Short, but typed EXACTLY:
    // they mix upper and lower case, digits and symbols. Never use '<' or '>'
    // (TextMeshPro would read them as rich-text tags) or spaces.
    private static readonly string[] OrbWords =
    {
        "Zap!", "Hex#", "B00m", "Fry!", "Rot?", "@Burn", "#Doom", "$Void", "Kr@ck", "Sp@rk",
        "Fl@re", "V0lt", "!Hit", "&Fear", "*Ash", "%Rip", "Nuk#", "Sh0ck", "Ion+", "Jolt!",
        "Qu@ke", "Wisp~", "Emb#r", "T0xic", "Omen!", "Pyr0", "Lava*", "Mana=", "Ray^", "EMP!",
        "Xen0n", "yELL", "dOOm", "cRaCk", "gl0w", "uRGe", "HeX", "Z@p", "k!LL", "BuRn!"
    };
    // (Extreme only. No orb word may contain the digits 1-4: those keys are the powers, see Powers.)

    // Explosive barrels (orange words).
    private static readonly string[] BarrelWords =
    {
        "FUEL", "GAS", "OIL", "TNT", "BOOM", "BLAST", "NAPALM", "PROPANE", "DIESEL", "KABOOM",
        "PETROL", "METHANE", "NITRO", "BUTANE", "KEROSENE", "DYNAMITE", "HAZARD", "IGNITE",
        "VOLATILE", "WARNING", "ACID", "JETFUEL", "LIGHTER", "ETHANOL", "RUMBLE", "SPARK",
        "UNSTABLE", "CRUDE", "ZAPPER", "YIKES", "QUAKE"
    };

    // Supply crates (green words). The word tells you what is inside.
    private static readonly string[] HealthCrateWords =
    {
        "MEDKIT", "BANDAGE", "HEALTH", "FIRSTAID", "PILLS", "TONIC", "REMEDY", "CURE", "VITAMIN",
        "SPLINT", "GAUZE", "ANTIDOTE", "ELIXIR", "OINTMENT", "NURSE", "DOCTOR", "LIFE", "KIT"
    };
    private static readonly string[] LureCrateWords =
    {
        "LURE", "BAIT", "DECOY", "SIREN", "MAGNET", "WHISTLE", "CHUM", "TRAP", "HONEY", "RATTLE",
        "GRENADE", "BOMB", "ALARM", "NOISE", "FIRECRACKER", "PINATA", "UKULELE", "EGGTIMER"
    };
    private static readonly string[] FreezeCrateWords =
    {
        "FREEZE", "ICE", "FROST", "CHILL", "COLD", "BLIZZARD", "SNOW", "POLAR", "GLACIER", "WINTER",
        "ARCTIC", "ICICLE", "TUNDRA", "HAIL", "SLEET", "KELVIN", "YETI", "DEEPFREEZE", "NORTH", "VORTEX"
    };

    // Returns a random word for an explosive barrel (first letter avoids usedFirstLetters if possible).
    public static string PickBarrelWord(List<char> usedFirstLetters)
    {
        return PickFrom(BarrelWords, usedFirstLetters);
    }

    // Returns a random word for a supply crate of this kind.
    public static string PickCrateWord(SupplyKind kind, List<char> usedFirstLetters)
    {
        if (kind == SupplyKind.Health)
        {
            return PickFrom(HealthCrateWords, usedFirstLetters);
        }
        if (kind == SupplyKind.Lure)
        {
            return PickFrom(LureCrateWords, usedFirstLetters);
        }
        return PickFrom(FreezeCrateWords, usedFirstLetters);
    }

    // The SHIELD WORD (Tab, see Powers): what the player types to raise the shield.
    private static readonly string[] ShieldWords =
    {
        "SHIELD", "PROTECT", "DEFEND", "GUARD", "BARRIER", "ARMOR", "BUNKER", "CASTLE", "COVER",
        "FORTRESS", "HELMET", "HAVEN", "RAMPART", "REFUGE", "SAFETY", "SHELTER", "TOWER", "VAULT",
        "BUBBLE", "OUTPOST", "KNIGHT", "ESCORT", "IGLOO", "LOCKDOWN", "UMBRELLA", "WALL", "ZONE"
    };

    // Returns a shield word whose first letter nothing on screen uses (if it can).
    public static string PickShieldWord(List<char> usedFirstLetters)
    {
        return PickFrom(ShieldWords, usedFirstLetters);
    }

    // Returns the first word of an armored zombie (breaks its armour).
    public static string PickArmorWord(List<char> usedFirstLetters)
    {
        return PickFrom(MediumWords, usedFirstLetters);
    }

    // A word from list with an unused first letter if there is one, otherwise any word from list.
    private static string PickFrom(string[] list, List<char> usedFirstLetters)
    {
        string word = PickWordWithUnusedFirstLetter(list, usedFirstLetters);
        if (word != null)
        {
            return word;
        }
        return list[Random.Range(0, list.Length)];
    }

    // Returns a random energy-orb word whose first character (ignoring case) is
    // not in usedFirstLetters, so typing it never picks a boss part by mistake.
    // usedFirstLetters must be UPPERCASE (fold with char.ToUpperInvariant).
    // A short common word styled for the typing difficulty; on Extreme, one of
    // the hand-made OrbWords (case, digits and symbols mixed).
    public static string PickOrbWord(List<char> usedFirstLetters)
    {
        if (TypingSettings.Difficulty != TypingDifficulty.Extreme)
        {
            return TypingSettings.Style(PickFrom(ShortWords, usedFirstLetters), usedFirstLetters);
        }

        List<string> candidates = new List<string>();
        foreach (string word in OrbWords)
        {
            if (!usedFirstLetters.Contains(char.ToUpperInvariant(word[0])))
            {
                candidates.Add(word);
            }
        }

        if (candidates.Count == 0)
        {
            return OrbWords[Random.Range(0, OrbWords.Length)];
        }
        return candidates[Random.Range(0, candidates.Count)];
    }

    // Returns a random hard word for a boss part, with a first letter that is
    // not in usedFirstLetters (the other parts' words), so each part can be
    // targeted by its first letter.
    public static string PickBossWord(List<char> usedFirstLetters)
    {
        string word = PickWordWithUnusedFirstLetter(BossWords, usedFirstLetters);
        if (word != null)
        {
            return word;
        }
        return BossWords[Random.Range(0, BossWords.Length)];
    }

    // Returns a random word for a new zombie of this kind.
    // usedFirstLetters = the first letter of every word that can be typed right now.
    // An explosive zombie always gets a long word (a long word = a big blast), a
    // runner a short one (it is fast, so it must be quick to type).
    public static string PickWord(List<char> usedFirstLetters, ZombieKind kind)
    {
        bool explosive = kind == ZombieKind.Explosive;

        // 1. Pick the length bucket.
        string[] rolledBucket;
        if (explosive)
        {
            rolledBucket = LongWords;
        }
        else if (kind == ZombieKind.Ghost)
        {
            rolledBucket = LongWords; // a ghost's word is typed from memory: a long one
        }
        else if (kind == ZombieKind.Runner || kind == ZombieKind.Splitling)
        {
            rolledBucket = ShortWords;
        }
        else if (kind == ZombieKind.Armored || kind == ZombieKind.Splitter || kind == ZombieKind.Spitter)
        {
            rolledBucket = MediumWords;
        }
        else if (Random.value < 0.60f)
        {
            rolledBucket = ShortWords;
        }
        else
        {
            rolledBucket = MediumWords;
        }

        // 2. Best case: a word from the rolled bucket with an unused first letter.
        string word = PickWordWithUnusedFirstLetter(rolledBucket, usedFirstLetters);
        if (word != null)
        {
            return word;
        }

        // 3. The rolled bucket has no unused first letter left. A normal zombie
        //    tries the other normal bucket. (An explosive zombie keeps a long word:
        //    it must stay longer than the normal ones.)
        if (!explosive)
        {
            word = PickWordWithUnusedFirstLetter(ShortWords, usedFirstLetters);
            if (word == null)
            {
                word = PickWordWithUnusedFirstLetter(MediumWords, usedFirstLetters);
            }
            if (word != null)
            {
                return word;
            }
        }

        // 4. Every first letter is taken (only with a huge crowd). A duplicate
        //    letter cannot be avoided; typing it will target the NEAREST match.
        return rolledBucket[Random.Range(0, rolledBucket.Length)];
    }

    // ---- SPLITTER words ----
    // A splitter's word breaks into two halves, and each half must be HIDDEN
    // inside another common word: "SUNSET" = "SUN" + "SET" -> "SUNSHINE" and
    // "UPSET". Found once (on first use) from every common word of 6-10
    // letters: the split where both halves (3+ letters) are inside some other,
    // longer word, preferring halves that are words themselves ("SUN", "SET")
    // and then a split near the middle. Only words where at least one half is
    // a word are kept, so the break always makes sense to the player.

    private static Dictionary<string, List<string>> fragmentWords; // 3-8 letter piece -> common words containing it
    private static Dictionary<string, int> splitterSplits;         // splitter word -> where it breaks
    private static string[] splitterWords;

    // A word for a new splitter (first letter unused on screen if possible).
    public static string PickSplitterWord(List<char> usedFirstLetters)
    {
        BuildSplitters();
        return PickFrom(splitterWords, usedFirstLetters);
    }

    // Where a splitter word breaks in two (0 = it is not a splitter word).
    public static int SplitterSplit(string word)
    {
        BuildSplitters();
        int split;
        return word != null && splitterSplits.TryGetValue(word, out split) ? split : 0;
    }

    // A common word that contains 'piece' (and is longer than it), not
    // 'exclude', whose first letter is unused on screen if possible.
    public static string PickWordContaining(string piece, string exclude, List<char> usedFirstLetters)
    {
        BuildSplitters();
        List<string> containing;
        List<string> candidates = new List<string>();
        if (fragmentWords.TryGetValue(piece, out containing))
        {
            foreach (string word in containing)
            {
                if (word != exclude && word.Length > piece.Length)
                {
                    candidates.Add(word);
                }
            }
        }
        if (candidates.Count == 0)
        {
            return piece; // cannot happen for a splitter word, but never leave a splitling without a word
        }
        return PickFrom(candidates.ToArray(), usedFirstLetters);
    }

    private static void BuildSplitters()
    {
        if (splitterSplits != null)
        {
            return;
        }

        List<string> all = new List<string>(ShortWords);
        all.AddRange(MediumWords);
        all.AddRange(LongWords);
        HashSet<string> isWord = new HashSet<string>(all);

        fragmentWords = new Dictionary<string, List<string>>();
        foreach (string word in all)
        {
            HashSet<string> pieces = new HashSet<string>(); // each piece once per word
            for (int start = 0; start < word.Length; start++)
            {
                for (int length = 3; length <= 8 && start + length <= word.Length; length++)
                {
                    pieces.Add(word.Substring(start, length));
                }
            }
            foreach (string piece in pieces)
            {
                List<string> list;
                if (!fragmentWords.TryGetValue(piece, out list))
                {
                    list = new List<string>();
                    fragmentWords[piece] = list;
                }
                list.Add(word);
            }
        }

        splitterSplits = new Dictionary<string, int>();
        foreach (string word in all)
        {
            if (word.Length < 6 || word.Length > 10)
            {
                continue;
            }
            int bestSplit = 0;
            int bestScore = 0;
            for (int split = 3; split <= word.Length - 3; split++)
            {
                string first = word.Substring(0, split);
                string second = word.Substring(split);
                if (!HiddenElsewhere(first, word) || !HiddenElsewhere(second, word))
                {
                    continue;
                }
                // Halves that are words count most; a split near the middle breaks ties.
                int score = (isWord.Contains(first) ? 100 : 0) + (isWord.Contains(second) ? 100 : 0)
                    - Mathf.Abs(word.Length - 2 * split);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSplit = split;
                }
            }
            if (bestSplit > 0 && bestScore >= 90) // at least one half is a word
            {
                splitterSplits[word] = bestSplit;
            }
        }
        splitterWords = new List<string>(splitterSplits.Keys).ToArray();
    }

    // True if some other, longer common word contains piece.
    private static bool HiddenElsewhere(string piece, string word)
    {
        List<string> containing;
        if (!fragmentWords.TryGetValue(piece, out containing))
        {
            return false;
        }
        foreach (string other in containing)
        {
            if (other != word && other.Length > piece.Length)
            {
                return true;
            }
        }
        return false;
    }

    // A BRUTE's PHRASE: 2 or 3 common words with single spaces between them
    // ("COLD DARK NIGHT"), like a line of a typing test. The first word's
    // first letter is unused on screen; the phrase stays at most MaxPhraseLength.
    private const int MaxPhraseLength = 20;

    public static string PickPhrase(List<char> usedFirstLetters)
    {
        int words = Random.value < 0.5f ? 2 : 3;
        string phrase = PickFrom(ShortWords, usedFirstLetters);
        for (int i = 1; i < words; i++)
        {
            string[] bucket = Random.value < 0.7f ? ShortWords : MediumWords;
            string next = bucket[Random.Range(0, bucket.Length)];
            if (phrase.Length + 1 + next.Length > MaxPhraseLength)
            {
                break;
            }
            phrase += " " + next;
        }
        return phrase;
    }

    // WORD CHAIN PAIRS: a short word and a longer word that starts with it
    // ("HUNT" and "HUNTER"), both normal-zombie words (short / medium lists),
    // whose first letter nothing on screen uses. Typing the long word kills
    // both (see TypingController). Returns null if no pair fits.
    public static string[] PickChainPair(List<char> usedFirstLetters)
    {
        if (chainPairs == null)
        {
            chainPairs = FindChainPairs();
        }

        List<string[]> pairs = new List<string[]>();
        foreach (string[] pair in chainPairs)
        {
            if (!usedFirstLetters.Contains(pair[0][0]))
            {
                pairs.Add(pair);
            }
        }

        if (pairs.Count == 0)
        {
            return null;
        }
        return pairs[Random.Range(0, pairs.Count)];
    }

    private static List<string[]> chainPairs; // every possible pair, found once

    private static List<string[]> FindChainPairs()
    {
        List<string> words = new List<string>(ShortWords);
        words.AddRange(MediumWords);

        List<string[]> pairs = new List<string[]>();
        foreach (string shortWord in words)
        {
            foreach (string longWord in words)
            {
                if (longWord.Length > shortWord.Length && longWord.StartsWith(shortWord, System.StringComparison.Ordinal))
                {
                    pairs.Add(new[] { shortWord, longWord });
                }
            }
        }
        return pairs;
    }

    // FRENZY (combo weapon): a tiny word for an enemy while the frenzy lasts,
    // 1 to 4 letters (the length is rolled at random, so it can be a SINGLE
    // letter), with a first letter not in usedFirstLetters (UPPERCASE).
    public static string PickFrenzyWord(List<char> usedFirstLetters)
    {
        // Try the rolled length first; if every first letter of that length is
        // taken, try the other lengths.
        int rolled = Random.Range(1, 5);
        for (int step = 0; step < 4; step++)
        {
            int length = (rolled - 1 + step) % 4 + 1;
            string word = PickWordWithUnusedFirstLetter(FrenzyBucket(length), usedFirstLetters);
            if (word != null)
            {
                return word;
            }
        }
        return ShortWords[Random.Range(0, ShortWords.Length)]; // every letter taken: a duplicate cannot be avoided
    }

    // Single letters and two-letter words for the frenzy (3 and 4 letters come from ShortWords).
    private static readonly string[] FrenzyLetters =
    {
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"
    };

    private static readonly string[] FrenzyTwoLetterWords =
    {
        "AH", "AX", "BE", "BY", "DO", "EH", "FX", "GO", "HI", "IF", "IN", "IT", "JO",
        "KO", "LO", "ME", "MY", "NO", "OH", "OK", "ON", "OX", "PI", "QI", "RE", "SO",
        "TO", "UP", "US", "VS", "WE", "XO", "YO", "ZZ"
    };

    private static readonly Dictionary<int, string[]> frenzyBuckets = new Dictionary<int, string[]>();

    // The frenzy words of one length (1..4).
    private static string[] FrenzyBucket(int length)
    {
        if (length == 1)
        {
            return FrenzyLetters;
        }
        if (length == 2)
        {
            return FrenzyTwoLetterWords;
        }
        string[] bucket;
        if (!frenzyBuckets.TryGetValue(length, out bucket))
        {
            List<string> words = new List<string>();
            foreach (string word in ShortWords)
            {
                if (word.Length == length)
                {
                    words.Add(word);
                }
            }
            bucket = words.ToArray();
            frenzyBuckets[length] = bucket;
        }
        return bucket;
    }

    // Returns a random word from the bucket whose first letter is not in
    // usedFirstLetters, or null if the bucket has no such word.
    private static string PickWordWithUnusedFirstLetter(string[] bucket, List<char> usedFirstLetters)
    {
        List<string> candidates = new List<string>();
        foreach (string word in bucket)
        {
            if (!usedFirstLetters.Contains(word[0]))
            {
                candidates.Add(word);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }
        return candidates[Random.Range(0, candidates.Count)];
    }
}
