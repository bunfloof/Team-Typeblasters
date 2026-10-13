// EasyWordListSwapper.cs
// ---------------------------------------------------------------------------
// EASY SCENE ONLY.
//
// Replaces newly spawned eligible zombies' default words with words from
// this script's own 2-5 letter lists. Word length is configurable per wave
// in the Inspector. This avoids changing the shared WordBank for other
// difficulties.
//
// Explosive zombies keep their original words because their blast radius
// is calculated from word length at spawn. Armored zombies and chain-linked
// zombies are skipped; Easy does not need to introduce those mechanics.
// The elite's word is set by EasyEliteZombie, which runs after this script.
//
// Tries to avoid first-letter duplicates using WaveSpawner.UsedFirstLetters,
// so typing a letter identifies one target whenever possible.
//
// Runs at execution order -50, before WaveSpawner lays out word labels.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class EasyWordListSwapper : MonoBehaviour
{
    // How many letters the words of one wave have (inclusive). Lists exist for 2 to 5 letters.
    [System.Serializable]
    public struct LetterRange
    {
        public int minLetters;
        public int maxLetters;

        public LetterRange(int min, int max)
        {
            minLetters = min;
            maxLetters = max;
        }
    }

    private const int ShortestList = 2;
    private const int LongestList = 5;

    [Header("Word length per wave (element 0 = wave 1)")]
    [SerializeField] private LetterRange[] lengthsPerWave =
    {
        new LetterRange(2, 3), // wave 1
        new LetterRange(3, 5), // wave 2
        new LetterRange(3, 5)  // wave 3 (and any later wave uses the last entry)
    };

    [Header("References")]
    [SerializeField] private WaveSpawner spawner; // left empty: found automatically

    private readonly HashSet<Zombie> seen = new HashSet<Zombie>(); // zombies already given a word

    // ---- The word lists (UPPERCASE A-Z only; each list has one length) ----

    private static readonly string[] TwoLetterWords =
    {
        "AH", "BE", "BY", "EH", "GO", "HI", "IF", "IN", "IT", "AY", 
        "ME", "NO", "OH", "OK", "ON", "RE", "SO", "TO", "UP", "YO"
    };

    private static readonly string[] ThreeLetterWords =
    {
        "AXE", "ARM", "ASH", "BAT", "BUG", "BIT", "CRY", "CUT", "DIG", "EEL", "EYE", "FOG", "FLY",
        "GUT", "HEX", "ICE", "INK", "JAW", "JAR", "KEY", "KIN", "LEG", "MUD", "MOB", "NET", "OWL",
        "PIT", "RAT", "ROT", "RIP", "SAW", "TAR", "URN", "VAT", "VEX", "WEB", "WAR", "ZAP", "ZOO"
    };

    private static readonly string[] FourLetterWords =
    {
        "BITE", "BONE", "BURN", "CLAW", "COLD", "DARK", "DEAD", "DOOM", "EVIL", "FANG", "FEAR", "FIRE",
        "GORE", "GRIM", "HOWL", "HUNT", "JUMP", "KILL", "LAIR", "LOST", "MOAN", "MIST", "NAIL", "OOZE",
        "PAIN", "PALE", "RAGE", "RUIN", "SCAR", "SKIN", "SOUL", "TOMB", "VEIN", "VILE", "WAIL", "WORM",
        "YELL", "ZERO", "ZONE"
    };

    private static readonly string[] FiveLetterWords =
    {
        "ASHES", "ABYSS", "AGONY", "BLOOD", "BEAST", "BLEED", "CRYPT", "CURSE", "DECAY", "DEMON", "DREAD",
        "EERIE", "FLESH", "GHOUL", "GRAVE", "GROWL", "HAUNT", "IVORY", "JELLY", "KNIFE", "LEECH", "MUMMY",
        "NIGHT", "NOOSE", "PANIC", "QUAKE", "RAVEN", "SKULL", "SCARE", "TALON", "TOXIC", "ULCER", "VENOM",
        "WITCH", "WRATH", "YEARN"
    };

    private void Awake()
    {
        if (spawner == null)
        {
            spawner = FindFirstObjectByType<WaveSpawner>();
        }
        CheckLists();
    }

    private void LateUpdate()
    {
        if (spawner == null)
        {
            return;
        }

        seen.RemoveWhere(zombie => zombie == null); // forget zombies Unity has destroyed

        List<Zombie> alive = spawner.AliveZombies;
        for (int i = 0; i < alive.Count; i++)
        {
            Zombie zombie = alive[i];
            if (zombie == null || !zombie.IsAlive || !seen.Add(zombie))
            {
                continue; // gone, or already given a word
            }

            // Red and armored zombies, and chain pairs, keep the word they were born with.
            if (zombie.Kind == ZombieKind.Armored || zombie.IsChainLinked)
            {
                continue;
            }

            GiveShortWord(zombie);
        }
    }

    private void GiveShortWord(Zombie zombie)
    {
        LetterRange range = RangeForWave(spawner.CurrentWave);

        // Every first letter in use, except this zombie's own (it is about to change).
        List<char> used = spawner.UsedFirstLetters();
        used.Remove(char.ToUpperInvariant(zombie.Word[0]));

        zombie.Wake(PickWord(range, used)); // Wake sets the word and refreshes its label
    }

    // The range of the wave (1 = first); waves past the end of the array use its last entry.
    private LetterRange RangeForWave(int wave)
    {
        if (lengthsPerWave == null || lengthsPerWave.Length == 0)
        {
            return new LetterRange(ShortestList, LongestList);
        }
        int index = Mathf.Clamp(wave - 1, 0, lengthsPerWave.Length - 1);
        return lengthsPerWave[index];
    }

    // A random word with the allowed number of letters whose first letter is not
    // in 'used'; if every first letter is taken, any word of those lengths.
    private static string PickWord(LetterRange range, List<char> used)
    {
        int min = Mathf.Clamp(range.minLetters, ShortestList, LongestList);
        int max = Mathf.Clamp(range.maxLetters, min, LongestList);

        List<string> free = new List<string>(); // allowed lengths, first letter not in use
        List<string> any = new List<string>();  // allowed lengths, whatever the first letter
        for (int length = min; length <= max; length++)
        {
            foreach (string word in ListFor(length))
            {
                any.Add(word);
                if (!used.Contains(word[0]))
                {
                    free.Add(word);
                }
            }
        }

        List<string> pool = free.Count > 0 ? free : any;
        return pool[Random.Range(0, pool.Count)];
    }

    private static string[] ListFor(int length)
    {
        switch (length)
        {
            case 2: return TwoLetterWords;
            case 3: return ThreeLetterWords;
            case 4: return FourLetterWords;
            default: return FiveLetterWords;
        }
    }

    // Prints a red error in the Console for a word that is not UPPERCASE A-Z or
    // is in the wrong list (a typo would otherwise show up only mid-fight).
    private static void CheckLists()
    {
        for (int length = ShortestList; length <= LongestList; length++)
        {
            foreach (string word in ListFor(length))
            {
                bool ok = word.Length == length;
                foreach (char c in word)
                {
                    if (c < 'A' || c > 'Z')
                    {
                        ok = false;
                    }
                }
                if (!ok)
                {
                    Debug.LogError("EasyWordListSwapper: \"" + word + "\" must be " + length + " UPPERCASE letters A-Z. Fix it in EasyWordListSwapper.cs.");
                }
            }
        }
    }
}
