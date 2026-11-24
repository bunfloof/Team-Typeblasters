// KillSounds.cs
// ---------------------------------------------------------------------------
// The sounds that reward good play. Nothing here is an audio file: every
// sound is SYNTHESIZED IN CODE once (sine partials, a pitch sweep, a little
// noise) into an AudioClip and cached, like HudIcons and MenuArt do for
// pictures. They play through Sfx, so the volume slider covers them.
//
// The idea (the way competitive shooters do it): the sound of a kill is a
// clear, musical "ping", and every kill that follows quickly plays the NEXT
// NOTE UP. The ear hears the climb and wants the next one.
//
//   Key()            a small crisp tick for every correct letter (four takes,
//                    so a fast run of letters never sounds like a machine)
//   Shot()           the bullet leaving: a short falling "pew"
//   OnKills(1)       a kill: a thump and a bell. Kills within StreakSeconds of
//                    each other climb D - F# - A - B - D; the 5th in a row
//                    also gets a sparkling run upward (the "ace")
//   OnKills(2+)      ONE blast killing several at once: a deep boom, then one
//                    note per kill running up into a chord. 5 or more is the
//                    big one: a two-octave run, a full chord and a shimmer.
//
// GameManager.AddKills calls OnKills (every kill of the game goes through
// it); TypingController calls Key and Shot.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class KillSounds
{
    private const int Rate = 44100;
    private const float StreakSeconds = 2.4f;   // a kill this soon after the last one continues the climb

    // How loud each sound is played (Sfx multiplies by the volume slider).
    private const float KeyVolume = 0.10f;
    private const float ShotVolume = 0.10f;
    private const float KillVolume = 0.20f;
    private const float AceVolume = 0.26f;
    private const float MultiVolume = 0.26f;

    // D major pentatonic, two octaves: bright, and any run of it sounds right.
    private static readonly float[] Scale =
    {
        587.33f, 739.99f, 880.00f, 987.77f, 1174.66f, 1479.98f, 1760.00f, 1975.53f, 2349.32f
    };

    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private static int streak;
    private static float lastKillAt = -100f;
    private static int keyTake;

    // Builds every clip now (a few milliseconds), so the first kill does not hitch.
    public static void Warm()
    {
        for (int take = 0; take < 4; take++) KeyClip(take);
        ShotClip();
        for (int step = 1; step <= 5; step++) KillClip(step);
        AceClip();
        for (int count = 2; count <= 5; count++) MultiClip(count);
    }

    public static void Key()
    {
        keyTake = (keyTake + 1 + Random.Range(0, 2)) % 4;
        Sfx.PlayClip(KeyClip(keyTake), KeyVolume);
    }

    public static void Shot()
    {
        Sfx.PlayClip(ShotClip(), ShotVolume);
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
            Sfx.PlayClip(MultiClip(Mathf.Min(count, 5)), MultiVolume);
            return;
        }
        Sfx.PlayClip(KillClip(Mathf.Min(streak, 5)), KillVolume);
        if (streak >= 5 && streak % 5 == 0)
        {
            Sfx.PlayClip(AceClip(), AceVolume);   // the 5th, 10th... kill in a row
        }
    }

    // A new run (a new level): the climb starts from the bottom again.
    public static void Reset()
    {
        streak = 0;
        lastKillAt = -100f;
    }

    // ======================================================================
    // The clips
    // ======================================================================

    private static AudioClip KeyClip(int take)
    {
        return Cached("key" + take, 0.05f, buffer =>
        {
            // A dry click and a very short high tone, a little different each take.
            Noise(buffer, 0f, 0.012f, 0.55f, 0.004f, 900 + take * 37);
            Tone(buffer, 0f, 1700f + take * 180f, 0.03f, 0.5f, 0.008f);
            Tone(buffer, 0f, 3400f + take * 260f, 0.02f, 0.2f, 0.005f);
        });
    }

    private static AudioClip ShotClip()
    {
        return Cached("shot", 0.16f, buffer =>
        {
            Sweep(buffer, 0f, 1500f, 320f, 0.13f, 0.8f, 0.045f);   // the "pew"
            Sweep(buffer, 0f, 3000f, 640f, 0.08f, 0.25f, 0.025f);
            Noise(buffer, 0f, 0.02f, 0.5f, 0.006f, 4211);            // the snap of the trigger
        });
    }

    private static AudioClip KillClip(int step)
    {
        return Cached("kill" + step, 0.6f, buffer =>
        {
            float note = Scale[step - 1];
            Sweep(buffer, 0f, 190f, 65f, 0.11f, 0.9f, 0.04f);     // the thump of the hit
            Noise(buffer, 0f, 0.015f, 0.35f, 0.004f, 77 + step);
            Bell(buffer, 0.008f, note, 0.55f, 0.75f, 0.16f + 0.01f * step);
        });
    }

    // The 5th kill in a row: the scale runs away upward and rings.
    private static AudioClip AceClip()
    {
        return Cached("ace", 1.3f, buffer =>
        {
            for (int i = 0; i < 5; i++)
            {
                Bell(buffer, 0.06f + i * 0.055f, Scale[4 + i], 0.5f, 0.5f, 0.14f);
            }
            Chord(buffer, 0.36f, Scale[4], 0.9f, 0.5f);
            Shimmer(buffer, 0.36f, 0.9f, 0.16f);
        });
    }

    // One blast, several kills: a boom, one note per kill running up, a chord.
    private static AudioClip MultiClip(int count)
    {
        bool big = count >= 5;
        return Cached("multi" + count, big ? 1.8f : 1.1f, buffer =>
        {
            Sweep(buffer, 0f, 140f, 38f, 0.32f, 1f, 0.12f);       // the boom under it
            Noise(buffer, 0f, 0.06f, 0.4f, 0.02f, 1009 + count);
            int notes = big ? 8 : count;
            float gap = big ? 0.05f : 0.065f;
            for (int i = 0; i < notes; i++)
            {
                Bell(buffer, 0.05f + i * gap, Scale[Mathf.Min(i + (big ? 0 : 1), Scale.Length - 1)], 0.5f, 0.55f, 0.15f);
            }
            float chordAt = 0.05f + notes * gap;
            Chord(buffer, chordAt, big ? Scale[4] : Scale[Mathf.Min(count, 4)], big ? 1.2f : 0.7f, big ? 0.75f : 0.5f);
            if (big)
            {
                Shimmer(buffer, chordAt, 1.2f, 0.22f);
            }
        });
    }

    // ======================================================================
    // The synthesizer
    // ======================================================================

    private delegate void Fill(float[] buffer);

    private static AudioClip Cached(string name, float seconds, Fill fill)
    {
        AudioClip clip;
        if (clips.TryGetValue(name, out clip) && clip != null)
        {
            return clip;
        }
        float[] buffer = new float[Mathf.CeilToInt(seconds * Rate)];
        fill(buffer);

        // Same peak for every clip (the Volume constants decide the rest), and a
        // few milliseconds of fade at the end so nothing clicks off.
        float peak = 0.0001f;
        for (int i = 0; i < buffer.Length; i++)
        {
            peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));
        }
        int fade = Mathf.Min(buffer.Length, Rate / 100);
        for (int i = 0; i < buffer.Length; i++)
        {
            float tail = Mathf.Clamp01((buffer.Length - 1 - i) / (float)fade);
            buffer[i] = buffer[i] / peak * 0.9f * tail;
        }

        clip = AudioClip.Create("KillSounds " + name, buffer.Length, 1, Rate, false);
        clip.SetData(buffer, 0);
        clips[name] = clip;
        return clip;
    }

    // A sine that dies away. decay = seconds to fall to about a third.
    private static void Tone(float[] buffer, float start, float frequency, float seconds, float volume, float decay)
    {
        int from = Mathf.FloorToInt(start * Rate);
        int count = Mathf.Min(Mathf.CeilToInt(seconds * Rate), buffer.Length - from);
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            float attack = Mathf.Clamp01(time / 0.002f);   // 2 ms in: no click
            buffer[from + i] += Mathf.Sin(2f * Mathf.PI * frequency * time) * Mathf.Exp(-time / decay) * attack * volume;
        }
    }

    // A sine whose pitch slides from one frequency to another (thumps, booms, the "pew").
    private static void Sweep(float[] buffer, float start, float fromHz, float toHz, float seconds, float volume, float decay)
    {
        int from = Mathf.FloorToInt(start * Rate);
        int count = Mathf.Min(Mathf.CeilToInt(seconds * Rate), buffer.Length - from);
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            float along = i / (float)count;
            float frequency = fromHz * Mathf.Pow(toHz / fromHz, along);   // falls evenly to the ear
            phase += 2f * Mathf.PI * frequency / Rate;
            float attack = Mathf.Clamp01(time / 0.002f);
            buffer[from + i] += Mathf.Sin(phase) * Mathf.Exp(-time / decay) * attack * volume;
        }
    }

    // A burst of hiss (its own little random series, so a clip is the same every time).
    private static void Noise(float[] buffer, float start, float seconds, float volume, float decay, int seed)
    {
        int from = Mathf.FloorToInt(start * Rate);
        int count = Mathf.Min(Mathf.CeilToInt(seconds * Rate), buffer.Length - from);
        uint state = (uint)seed * 2654435761u + 1u;
        float previous = 0f;
        for (int i = 0; i < count; i++)
        {
            state = state * 1664525u + 1013904223u;
            float white = (state >> 8) / 8388608f - 1f;
            float sample = white - previous * 0.5f;          // a little brighter than plain hiss
            previous = white;
            buffer[from + i] += sample * Mathf.Exp(-(i / (float)Rate) / decay) * volume;
        }
    }

    // A struck bell: the note, and quieter partials above it that die sooner.
    private static void Bell(float[] buffer, float start, float frequency, float seconds, float volume, float decay)
    {
        Tone(buffer, start, frequency, seconds, volume, decay);
        Tone(buffer, start, frequency * 2f, seconds, volume * 0.42f, decay * 0.6f);
        Tone(buffer, start, frequency * 3.01f, seconds, volume * 0.20f, decay * 0.38f);
        Tone(buffer, start, frequency * 4.24f, seconds * 0.6f, volume * 0.10f, decay * 0.22f);
    }

    // A major chord on a note, ringing.
    private static void Chord(float[] buffer, float start, float root, float seconds, float volume)
    {
        Bell(buffer, start, root, seconds, volume * 0.5f, seconds * 0.36f);
        Bell(buffer, start, root * 1.25f, seconds, volume * 0.36f, seconds * 0.34f);
        Bell(buffer, start, root * 1.5f, seconds, volume * 0.36f, seconds * 0.34f);
        Bell(buffer, start, root * 0.5f, seconds, volume * 0.4f, seconds * 0.4f);
    }

    // A glitter over the top: very high notes that tremble.
    private static void Shimmer(float[] buffer, float start, float seconds, float volume)
    {
        int from = Mathf.FloorToInt(start * Rate);
        int count = Mathf.Min(Mathf.CeilToInt(seconds * Rate), buffer.Length - from);
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            float tremble = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 9f * time);
            float envelope = Mathf.Clamp01(time / 0.05f) * Mathf.Exp(-time / (seconds * 0.4f));
            float sample = Mathf.Sin(2f * Mathf.PI * 4698.6f * time) + 0.7f * Mathf.Sin(2f * Mathf.PI * 5919.9f * time)
                + 0.5f * Mathf.Sin(2f * Mathf.PI * 7040f * time);
            buffer[from + i] += sample * tremble * envelope * volume * 0.3f;
        }
    }
}
