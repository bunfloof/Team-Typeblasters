using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class Sfx : MonoBehaviour
{
    private const string ResourceFolder = "Sfx/";
    private const string VolumePrefKey = "Sfx.MasterVolume";
    private const float DefaultMasterVolume = 0.5f;

    private static Sfx instance;

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    private AudioSource gameplaySource;
    private AudioSource uiSource;
    private float masterVolume;
    private bool gameplayPaused;


    public static float MasterVolume
    {
        get { return Instance.masterVolume; }
    }


    public static bool Play(string clipName, float volume = 1f)
    {
        return Instance.PlayInternal(clipName, volume, false);
    }


    public static bool PlayUI(string clipName, float volume = 1f)
    {
        return Instance.PlayInternal(clipName, volume, true);
    }


    public static bool PlayUnpaused(string clipName, float volume = 1f)
    {
        return Instance.PlayInternal(clipName, volume, true);
    }


    // A clip made in code (see KillSounds). It plays on the channel that keeps
    // going when the game pauses, so a kill's ring is never cut off by the
    // reward screen or the end of a wave.
    public static void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }
        Instance.uiSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }


    public static bool PlayBossIntro(float volume = 1f)
    {
        return Play("Boss/boss_intro", volume);
    }


    public static bool PlayBossDefeated(float volume = 1f)
    {
        return PlayUnpaused("Boss/boss_defeated", volume);
    }


    public static void SetMasterVolume(float volume)
    {
        Instance.ApplyMasterVolume(volume);
    }


    public static void SavePreferences()
    {
        PlayerPrefs.Save();
    }


    public static bool Preload(string clipName)
    {
        return Instance.LoadClip(clipName) != null;
    }

    public static void HookButton(Button button)
    {
        if (button == null || button.GetComponent<SfxButtonClickHook>() != null)
        {
            return;
        }

        button.gameObject.AddComponent<SfxButtonClickHook>();
    }

    private static Sfx Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject manager = new GameObject("SfxManager");
                instance = manager.AddComponent<Sfx>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefKey, DefaultMasterVolume));
        gameplaySource = CreateAudioSource(false);
        uiSource = CreateAudioSource(true);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private AudioSource CreateAudioSource(bool ignoreListenerPause)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = masterVolume;
        source.ignoreListenerPause = ignoreListenerPause;
        return source;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
        }
    }

    private bool PlayInternal(string clipName, float volume, bool useUiChannel)
    {
        if (!useUiChannel && gameplayPaused)
        {
            return false;
        }

        AudioClip clip = LoadClip(clipName);
        if (clip == null)
        {
            return false;
        }

        AudioSource source = useUiChannel ? uiSource : gameplaySource;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
        return true;
    }

    private AudioClip LoadClip(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            Debug.LogWarning("[Sfx] Empty clip name.");
            return null;
        }

        string key = clipName.Trim().Replace('\\', '/').Trim('/');
        AudioClip clip;
        if (clipCache.TryGetValue(key, out clip))
        {
            return clip;
        }

        clip = Resources.Load<AudioClip>(ResourceFolder + key);
        clipCache.Add(key, clip);

        if (clip == null)
        {
            Debug.LogWarning("[Sfx] Could not find audio clip: Resources/" + ResourceFolder + key);
        }

        return clip;
    }

    private void ApplyMasterVolume(float volume)
    {
        if (float.IsNaN(volume) || float.IsInfinity(volume))
        {
            volume = DefaultMasterVolume;
        }

        masterVolume = Mathf.Clamp01(volume);
        if (gameplaySource != null) gameplaySource.volume = masterVolume;
        if (uiSource != null) uiSource.volume = masterVolume;

        PlayerPrefs.SetFloat(VolumePrefKey, masterVolume);
    }

    private void Update()
    {
        GameManager manager = GameManager.Instance;
        bool shouldPauseGameplay = manager != null &&
            (manager.State == GameState.Paused ||
             manager.State == GameState.Tip ||
             manager.State == GameState.Reward ||
             manager.State == GameState.Won ||
             manager.State == GameState.Lost);

        SetGameplayPaused(shouldPauseGameplay);
    }

    private void SetGameplayPaused(bool paused)
    {
        if (gameplayPaused == paused)
        {
            return;
        }

        gameplayPaused = paused;
        if (gameplaySource != null)
        {
            if (paused) gameplaySource.Pause();
            else gameplaySource.UnPause();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Do not resume a one-shot that was paused in the scene we just left.
        if (gameplaySource != null)
        {
            gameplaySource.Stop();
        }

        // A newly loaded scene starts at its own Start state/timeScale.
        SetGameplayPaused(false);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}
