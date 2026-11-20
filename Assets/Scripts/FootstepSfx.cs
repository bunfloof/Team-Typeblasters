using UnityEngine;

// Loops a footsteps clip while the player rides between fights.
// Creates itself, like Sfx and AnalyticsSender. No scene setup needed.
public class FootstepSfx : MonoBehaviour
{
    private const string ClipPath = "Sfx/Footsteps/footsteps";
    private const float BaseVolume = 0.015f;
    private const float FadeInSeconds = 0.15f;
    private const float FadeOutSeconds = 0.25f;
    private const float TopSpeed = 9f;          // RailMover.railSpeed
    private const float MinPitch = 0.85f;       // slower ride (ease in/out, ambush slow-down)
    private const float MaxPitch = 1.05f;

    private static FootstepSfx instance;

    private AudioSource source;
    private RailMover rail;
    private float nextSearch;
    private float lastTravelled;
    private float speed;
    private float level;      // 0..1 fade
    private bool held;        // paused because the game is not in Playing

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("FootstepSfx");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FootstepSfx>();
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = Resources.Load<AudioClip>(ClipPath);
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        if (source.clip == null)
        {
            Debug.LogWarning("[FootstepSfx] Missing clip: Resources/" + ClipPath);
            enabled = false;
            return;
        }
        if (source.clip.loadState == AudioDataLoadState.Unloaded) source.clip.LoadAudioData();
    }

    private void Update()
    {
        GameManager game = GameManager.Instance;
        bool playing = game != null && game.State == GameState.Playing;

        // Pause / resume with the game (Time.timeScale does not stop audio).
        if (!playing)
        {
            if (!held) { source.Pause(); held = true; }
            return;
        }
        if (held) { source.UnPause(); held = false; }

        // The RailMover lives in the level scenes; find it again after a reload.
        if (rail == null)
        {
            if (Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 0.5f;
                rail = FindFirstObjectByType<RailMover>();
            }
            speed = 0f;
        }

        bool riding = rail != null && rail.IsRiding;
        float dt = Time.deltaTime;
        if (rail != null && dt > 0f)
        {
            float travelled = rail.Travelled;
            float measured = riding ? Mathf.Max(0f, travelled - lastTravelled) / dt : 0f;
            lastTravelled = travelled; // Travelled restarts at 0 on every ride
            speed = Mathf.Lerp(speed, measured, 10f * dt);
        }

        float target = riding ? 1f : 0f;
        float fade = riding ? FadeInSeconds : FadeOutSeconds;
        level = Mathf.MoveTowards(level, target, dt / fade);

        if (riding && !source.isPlaying)
        {
            source.Play();
        }
        else if (!riding && level <= 0f && source.isPlaying)
        {
            source.Stop();
        }

        source.pitch = Mathf.Lerp(MinPitch, MaxPitch, Mathf.Clamp01(speed / TopSpeed));
        source.volume = level * BaseVolume * Sfx.MasterVolume;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
