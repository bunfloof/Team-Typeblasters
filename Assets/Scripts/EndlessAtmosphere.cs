// EndlessAtmosphere.cs
// ---------------------------------------------------------------------------
// Endless mode only: the scene's light changes colour every N waves, so a long
// run feels like it is getting darker. Put this component on any object in
// Endless.unity; nothing else is needed.
//
//   Waves Per Mood   N: how many waves each mood lasts (set it in the Inspector).
//   Fade Seconds     how long the change from one mood to the next takes.
//
// Mood 0 is the scene's own look (read when the scene loads). The others are
// in Moods below; after the last one it starts again from mood 0. It changes
// the directional light's colour and intensity and the flat ambient colour
// (RenderSettings). Both belong to the scene and come back when it reloads
// (Restart), so nothing leaks into Normal or Hard. No fog, no new assets.
// Keep the colours soft: zombies are colour-coded, and a strong tint hides it.
// ---------------------------------------------------------------------------
using UnityEngine;

public class EndlessAtmosphere : MonoBehaviour
{
    [SerializeField, Min(1)] private int wavesPerMood = 5;
    [SerializeField, Min(0.1f)] private float fadeSeconds = 5f;
    [SerializeField, Range(0f, 1f)] private float strength = 1f; // 0 = no change at all, 1 = the full mood colours below

    private struct Mood
    {
        public Color Sun;
        public float SunIntensity;
        public Color Ambient;
        public float Strength; // this mood's own share of 'strength' (the blue one is weaker)

        public Mood(Color sun, float sunIntensity, Color ambient, float strength = 1f)
        {
            Sun = sun;
            SunIntensity = sunIntensity;
            Ambient = ambient;
            Strength = strength;
        }
    }

    // Moods 1..: dusk, blood red, toxic green, cold night. (Mood 0 = the scene as made.)
    // They keep about the scene's own brightness (sun intensity 1, ambient as
    // bright as the original), so they change the COLOUR, not how dark it is.
    // Each is then mixed with the scene's own look by 'strength'.
    private static readonly Mood[] Moods =
    {
        new Mood(new Color(1.00f, 0.66f, 0.38f), 1f, new Color(0.62f, 0.50f, 0.45f), 0.6f),
        new Mood(new Color(0.95f, 0.32f, 0.26f), 1f, new Color(0.62f, 0.36f, 0.34f), 0.4f),
        new Mood(new Color(0.42f, 0.95f, 0.45f), 1f, new Color(0.42f, 0.40f, 0.42f), 0.4f),
        new Mood(new Color(0.55f, 0.65f, 0.95f), 1f, new Color(0.40f, 0.48f, 0.65f), 0.7f),
    };

    private Light sun;                 // the directional light (none found: only the ambient changes)
    private Mood original;             // mood 0
    private Color currentSun;
    private float currentIntensity;
    private Color currentAmbient;

    private void Awake()
    {
        foreach (Light candidate in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (candidate.type == LightType.Directional)
            {
                sun = candidate;
                break;
            }
        }
        original = new Mood(sun != null ? sun.color : Color.white, sun != null ? sun.intensity : 1f, RenderSettings.ambientLight);
        currentSun = original.Sun;
        currentIntensity = original.SunIntensity;
        currentAmbient = original.Ambient;
    }

    private void Update()
    {
        GameManager game = GameManager.Instance;
        if (game == null || game.Spawner == null || !game.Spawner.IsEndlessMode)
        {
            return;
        }

        // Which mood the current wave belongs to: waves 1..N = 0, N+1..2N = 1, ...
        int wave = Mathf.Max(1, game.Spawner.CurrentWave);
        int index = ((wave - 1) / wavesPerMood) % (Moods.Length + 1);
        Mood target = original;
        if (index > 0)
        {
            Mood mood = Moods[index - 1];
            float mix = strength * mood.Strength; // how far from the scene's own look
            target = new Mood(Color.Lerp(original.Sun, mood.Sun, mix), Mathf.Lerp(original.SunIntensity, mood.SunIntensity, mix),
                Color.Lerp(original.Ambient, mood.Ambient, mix));
        }

        // Glide toward it (game time: it also waits while paused).
        float blend = 1f - Mathf.Exp(-Time.deltaTime * 3f / fadeSeconds);
        currentSun = Color.Lerp(currentSun, target.Sun, blend);
        currentIntensity = Mathf.Lerp(currentIntensity, target.SunIntensity, blend);
        currentAmbient = Color.Lerp(currentAmbient, target.Ambient, blend);

        RenderSettings.ambientLight = currentAmbient;
        if (sun != null)
        {
            sun.color = currentSun;
            sun.intensity = currentIntensity;
        }
    }
}
