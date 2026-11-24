// KillSounds.cs
// ---------------------------------------------------------------------------
// The sounds of typing and killing. The clips are our own, made from scratch
// by Tools/SoundDesign/make_sfx.py (oscillators, noise, filters and a
// synthetic reverb; no recordings) into Resources/Sfx/Generated.
//
//   Key()         every correct letter: a mechanical key (six takes, so fast
//                 typing never sounds like a machine gun of one sample)
//   Shot()        the LAST letter of a word: a heavier key with a suppressed
//                 shot under it (it replaces the key sound for that letter)
//   OnKills(1)    a kill: a short glassy confirm, over in about a quarter of a
//                 second. Kills within StreakSeconds of each other climb one
//                 note each (five steps), so a fast run of kills rises.
//   OnKills(2+)   one blast, several kills: 2 = a double, 3 or 4 = the big
//                 one, 5 or more = bigger still.
//
// GameManager.AddKills calls OnKills (every kill goes through it);
// TypingController calls Key and Shot. They play on the channel that keeps
// going when the game pauses, so the end of a wave never cuts one off.
// ---------------------------------------------------------------------------
using UnityEngine;

public static class KillSounds
{
    private const string Folder = "Generated/";
    private const float StreakSeconds = 2.4f;

    private const float KeyVolume = 0.22f;
    private const float ShotVolume = 0.30f;
    private const float KillVolume = 0.24f;
    private const float MultiVolume = 0.55f;

    private const int KeyTakes = 6;
    private static int lastKey = -1;
    private static int streak;
    private static float lastKillAt = -100f;

    public static void Warm()
    {
        for (int i = 0; i < KeyTakes; i++) Sfx.Preload(Folder + "key_" + i);
        Sfx.Preload(Folder + "shot");
        for (int i = 1; i <= 5; i++) Sfx.Preload(Folder + "kill_" + i);
        Sfx.Preload(Folder + "multi_2");
        Sfx.Preload(Folder + "multi_3");
        Sfx.Preload(Folder + "multi_5");
    }

    public static void Key()
    {
        int take = Random.Range(0, KeyTakes - 1);
        if (take >= lastKey) take++; // never the same take twice in a row
        lastKey = take;
        Sfx.PlayUnpaused(Folder + "key_" + take, KeyVolume);
    }

    public static void Shot()
    {
        Sfx.PlayUnpaused(Folder + "shot", ShotVolume);
    }

    // count = how many enemies ONE shot or blast just killed.
    public static void OnKills(int count)
    {
        if (count <= 0)
        {
            return;
        }
        float now = Time.unscaledTime;
        streak = now - lastKillAt <= StreakSeconds ? streak + count : count;
        lastKillAt = now;

        if (count >= 2)
        {
            string clip = count >= 5 ? "multi_5" : count >= 3 ? "multi_3" : "multi_2";
            Sfx.PlayUnpaused(Folder + clip, MultiVolume);
            return;
        }
        Sfx.PlayUnpaused(Folder + "kill_" + Mathf.Clamp(streak, 1, 5), KillVolume);
    }

    // A new run: the climb starts from the bottom again.
    public static void Reset()
    {
        streak = 0;
        lastKillAt = -100f;
    }
}
