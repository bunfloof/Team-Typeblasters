// Checkpoint.cs
// ---------------------------------------------------------------------------
// A CHECKPOINT after every wave: when the player dies, Enter (or Restart) on
// the "You died" panel brings them back at the start of the wave they died
// in, with the score and energy they had when it began, full health, and the
// kinds of zombie they had already met (so nothing is introduced twice).
//
//   WaveSpawner  Save(...) when a wave is won (before the ride to the next)
//   GameManager  on Restart after a death: Resume (the scene reloads and
//                the run starts at once from the saved wave); a Restart from
//                the pause panel, a win or the menu forget the checkpoint.
// Static: it survives the scene reload, and nothing else.
// ---------------------------------------------------------------------------
using System.Collections.Generic;

public static class Checkpoint
{
    public static string Scene { get; private set; }
    public static int WaveIndex { get; private set; }   // the encounter to start at (0 = the first)
    public static int Score { get; private set; }
    public static int Energy { get; private set; }
    public static bool ChainSeen { get; private set; }
    public static readonly HashSet<ZombieKind> SeenKinds = new HashSet<ZombieKind>();

    // True right after a death's Restart, until the reloaded level starts from it.
    public static bool ResumePending { get; private set; }

    public static bool Has(string scene)
    {
        return Scene == scene && WaveIndex > 0;
    }

    public static void Save(string scene, int waveIndex, int score, int energy, IEnumerable<ZombieKind> seen, bool chainSeen)
    {
        Scene = scene;
        WaveIndex = waveIndex;
        Score = score;
        Energy = energy;
        ChainSeen = chainSeen;
        SeenKinds.Clear();
        foreach (ZombieKind kind in seen)
        {
            SeenKinds.Add(kind);
        }
    }

    public static void RequestResume()
    {
        ResumePending = true;
    }

    // The level has started from it (or will not): back to normal.
    public static bool TakeResume(string scene)
    {
        bool resume = ResumePending && Has(scene);
        ResumePending = false;
        return resume;
    }

    public static void Clear()
    {
        Scene = null;
        WaveIndex = 0;
        ResumePending = false;
        SeenKinds.Clear();
    }
}
