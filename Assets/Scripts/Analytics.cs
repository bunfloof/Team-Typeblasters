// Analytics.cs
// ---------------------------------------------------------------------------
// Records what happens in the game for the class playtest analytics. The
// events are sent by AnalyticsSender to the team's log server
// (https://typeblasterslogs.joey.ma/v1/batch, see the Typeblasters-Log repo).
// Nothing here changes the game: it only writes down what happened.
//
// Every script logs the same way:
//
//   AnalyticsEvent e = Analytics.NewEvent("crate");
//   e.Add("kind", "Health");
//   Analytics.AddPlayerState(e);   // optional: health, combo, powers...
//   Analytics.Send(e);
//
// Send() adds the fields every event carries (seq, event_id, run_id, level,
// state, wave, phase, t_play, t_real) and puts it in the queue.
//
// THE IDs (sent with every batch, see AnalyticsSender):
//   install_id - made the first time the game runs in this browser and kept
//                in PlayerPrefs (the browser's storage for the game's page).
//                It works like a cookie: the same browser keeps it; clearing
//                the site's data, a private window, another browser or
//                another link (each Vercel build link) give a new one.
//   session_id - new every time the page is loaded (static: Restart keeps it).
//   run_id     - new every time Start is pressed; cleared when the scene
//                reloads (Restart), so Start-panel events carry none.
//   seq        - 1, 2, 3... for the events of one session, in order;
//                event_id = session_id + "-" + seq (duplicates share it).
//   zombie_id  - 1, 2, 3... for the zombies of one run (NextZombieId), so
//                a zombie's spawn, end, bites, typed words and blast join.
//
// TEST DATA: events from the Unity Editor, a development build, or a page
// opened with "qa=1" in its address get an install_id starting with "qa-",
// so the team's own test plays are easy to remove before the exam.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Analytics
{
    public const int Format = 1;        // the batch format the log server expects

    // Safety cap (about 6 MB) for when no AnalyticsSender is running; with
    // one, events move into its batches every few seconds.
    private const int MaxQueued = 20000;

    private const string InstallIdKey = "analytics_install_id";
    private const string RunCountKey = "analytics_run_count";

    // Events waiting to be sent, oldest first, each one a finished JSON object.
    // AnalyticsSender takes them from here.
    public static readonly List<string> Queue = new List<string>();

    public static string InstallId { get; private set; }  // with "qa-" in front for test plays
    public static string SessionId { get; private set; }
    public static string Build { get; private set; }      // which build of the game this is

    private static bool started;
    private static string runId = "";
    private static int zombieCount;      // zombies created in this run (each gets the next zombie_id)
    private static int runNumber;        // runs ever started in this browser, this one included
    private static int runsThisSession;  // runs started since the page was loaded
    private static int seq;

    // Set by FlushSoon(); AnalyticsSender sends right away when it sees it.
    public static bool FlushRequested { get; set; }

    // ---- Logging ----

    public static AnalyticsEvent NewEvent(string eventName)
    {
        return new AnalyticsEvent(eventName);
    }

    // Adds the fields every event carries and queues the event.
    public static void Send(AnalyticsEvent e)
    {
        Begin();

        seq += 1;
        e.Add("seq", seq);
        e.Add("event_id", SessionId + "-" + seq);
        if (runId.Length > 0)
        {
            e.Add("run_id", runId);
        }
        e.Add("level", SceneManager.GetActiveScene().name);
        e.Add("typing_difficulty", TypingSettings.Name(TypingSettings.Difficulty));
        e.Add("t_real", Time.realtimeSinceStartup); // seconds since the page was loaded

        GameManager game = GameManager.Instance;
        if (game != null)
        {
            e.Add("state", game.State.ToString());
            e.Add("t_play", game.Stats.Seconds);    // seconds of play in this run (pauses not counted)
            if (game.Spawner != null)
            {
                e.Add("wave", game.Spawner.CurrentWave);
                e.Add("phase", game.Spawner.PhaseName);
            }
        }

        Queue.Add(e.ToJson());
        if (Queue.Count > MaxQueued)
        {
            Queue.RemoveAt(0);
        }
    }

    // Asks AnalyticsSender to send what is queued now instead of waiting for
    // its timer (used at checkpoints: wave end, death, pause...).
    public static void FlushSoon()
    {
        FlushRequested = true;
    }

    // ---- Session and runs ----

    // Sets up the IDs once per page load and logs "session_start". Called by
    // AnalyticsSender.Awake, and by Send() in case something logs earlier.
    public static void Begin()
    {
        if (started)
        {
            return;
        }
        started = true;

        SessionId = NewId();
        // A build's GUID is different for every build; the Editor has none (all zeros).
        Build = Application.isEditor ? "editor" : Application.version + "-" + Application.buildGUID;

        bool isNew = !PlayerPrefs.HasKey(InstallIdKey);
        if (isNew)
        {
            PlayerPrefs.SetString(InstallIdKey, NewId());
            PlayerPrefs.Save(); // the Web build has no "quit" moment that would save it later
        }
        string storedId = PlayerPrefs.GetString(InstallIdKey, "");
        bool storageWorks = storedId.Length > 0; // false if the browser refused to store it
        if (!storageWorks)
        {
            storedId = SessionId;
        }
        bool testPlay = IsTestPlay();
        InstallId = (testPlay ? "qa-" : "") + storedId;
        runNumber = PlayerPrefs.GetInt(RunCountKey, 0);

        AnalyticsEvent e = NewEvent("session_start");
        e.Add("install_new", isNew);
        e.Add("storage_ok", storageWorks);
        e.Add("runs_before", runNumber);
        e.Add("test_play", testPlay);
        e.Add("page_url", Application.absoluteURL);
        e.Add("platform", Application.platform.ToString());
        e.Add("unity_version", Application.unityVersion);
        e.Add("app_version", Application.version);
        e.Add("os", SystemInfo.operatingSystem);
        e.Add("device_model", SystemInfo.deviceModel);
        e.Add("gpu", SystemInfo.graphicsDeviceName);
        e.Add("cpu_count", SystemInfo.processorCount);
        e.Add("memory_mb", SystemInfo.systemMemorySize);
        e.Add("screen_width", Screen.width);
        e.Add("screen_height", Screen.height);
        e.Add("screen_dpi", Screen.dpi);
        e.Add("language", Application.systemLanguage.ToString());
        e.Add("touch_controls", TouchControls.Active); // a phone / tablet playing with the on-screen keyboard
        Send(e);
    }

    // Called by GameManager.StartGame: a new run begins.
    public static void StartRun()
    {
        Begin();
        runId = NewId();
        runNumber += 1;
        runsThisSession += 1;
        zombieCount = 0;
        PlayerPrefs.SetInt(RunCountKey, runNumber);
        PlayerPrefs.Save();
    }

    // Called by GameManager.RestartGame (from the end screens or the pause
    // panel): the scene reloads, so the Start panel's events belong to no run.
    public static void EndRun()
    {
        runId = "";
    }

    public static int RunNumber
    {
        get { return runNumber; }
    }

    // Called by Zombie.Setup: the zombie's number within the run (zombie_id),
    // so its zombie_spawn, zombie, damage, word and blast events can be joined.
    public static int NextZombieId()
    {
        zombieCount += 1;
        return zombieCount;
    }

    // zombie_id of a typed target, or -1 when it is not a zombie.
    public static int TargetId(ITypingTarget target)
    {
        if (target is Zombie)
        {
            return ((Zombie)target).Id;
        }
        return -1;
    }

    public static int RunsThisSession
    {
        get { return runsThisSession; }
    }

    // A random ID, 38 letters and digits. Two random sources are joined, so
    // two browsers can never end up with the same ID even if one is weak.
    private static string NewId()
    {
        return System.Guid.NewGuid().ToString("N") + Random.Range(0, 0x1000000).ToString("x6");
    }

    // The Editor, a development build, or "qa=1" in the page's address.
    private static bool IsTestPlay()
    {
        if (Application.isEditor || Debug.isDebugBuild)
        {
            return true;
        }
        string address = Application.absoluteURL;
        return address != null && address.Contains("qa=1");
    }

    // ---- Shared fields ----

    // The player's situation right now: health, combo, powers, what is being
    // typed, how many zombies are around. 'ignore': a zombie not to count
    // (the one biting right now).
    public static void AddPlayerState(AnalyticsEvent e, Zombie ignore = null)
    {
        GameManager game = GameManager.Instance;
        if (game == null)
        {
            return;
        }

        e.Add("health", game.Health);
        e.Add("combo", game.Combo);
        e.Add("score", game.Score);

        Powers powers = game.Powers;
        e.Add("lure_charges", powers.LureCharges);
        e.Add("freeze_charges", powers.FreezeCharges);
        e.Add("frenzy_kept", powers.FrenzyKept);
        e.Add("rpg_kept", powers.RocketKept);
        e.Add("rpg_loaded", powers.RocketLoaded);
        e.Add("can_buy_frenzy", powers.CanBuyNow(false));
        e.Add("can_buy_rpg", powers.CanBuyNow(true));
        e.Add("frenzy_on", Powers.IsFrenzy);
        e.Add("shield_on", Powers.IsShielded);
        e.Add("shield_charges", powers.ShieldCharges);
        e.Add("shield_word_open", ShieldWord.Current != null);
        e.Add("freeze_on", Powers.IsFrozen);
        e.Add("aiming_lure", powers.IsAiming);

        int zombies = 0;
        if (game.Spawner != null)
        {
            foreach (Zombie zombie in game.Spawner.AliveZombies)
            {
                if (zombie != null && zombie.IsAlive && zombie != ignore)
                {
                    zombies += 1;
                }
            }
        }
        e.Add("zombies_alive", zombies);

        if (game.Spawner != null)
        {
            Vector3 player = game.Spawner.PlayerPosition;
            e.Add("player_x", player.x);
            e.Add("player_z", player.z);
        }

        ITypingTarget target = game.Typing != null ? game.Typing.CurrentTarget : null;
        if (target != null)
        {
            e.Add("typing_target", TargetType(target));
            e.Add("typing_word_length", target.Word.Length);
            e.Add("typing_letters_done", game.Typing.LettersTypedInWord);
        }
        else
        {
            e.Add("typing_target", "none");
        }
    }

    // The "sample" heartbeat: AnalyticsSender sends one for every second of
    // play, so the timeline can follow the player between the other events.
    // fps: frames per second since the last sample.
    public static void LogSample(float fps)
    {
        GameManager game = GameManager.Instance;
        if (game == null || game.Spawner == null)
        {
            return;
        }

        AnalyticsEvent e = NewEvent("sample");
        AddPlayerState(e);
        e.Add("yaw", game.Spawner.PlayerYaw);

        Vector3 player = game.Spawner.PlayerPosition;
        Zombie nearest = null;
        float nearestDistance = -1f;
        foreach (Zombie zombie in game.Spawner.AliveZombies)
        {
            if (zombie == null || !zombie.IsAlive)
            {
                continue;
            }
            Vector3 offset = zombie.Position - player;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (nearest == null || distance < nearestDistance)
            {
                nearest = zombie;
                nearestDistance = distance;
            }
        }
        e.Add("nearest_zombie_m", nearestDistance); // -1 = no zombie around
        e.Add("nearest_zombie_id", nearest != null ? nearest.Id : -1);
        e.Add("kills", game.Stats.Kills);
        e.Add("correct_keys", game.Stats.CorrectKeys);
        e.Add("wrong_keys", game.Stats.WrongKeys);
        e.Add("fps", fps);
        Send(e);
    }

    // A short name for what kind of target something typeable is, e.g.
    // "zombie_runner", "barrel", "crate_lure", "orb", "boss_arm", "quiz_answer".
    // "is" is used on purpose: it still works for a zombie destroyed a moment
    // ago (Unity's "== null" would already say it is gone).
    public static string TargetType(ITypingTarget target)
    {
        if (target == null)
        {
            return "none";
        }
        if (target is Zombie)
        {
            return "zombie_" + ((Zombie)target).Kind.ToString().ToLowerInvariant();
        }
        if (target is SupplyCrate)
        {
            return "crate_" + ((SupplyCrate)target).Kind.ToString().ToLowerInvariant();
        }
        if (target is BossPart)
        {
            BossLimb limb = ((BossPart)target).Limb;
            if (limb.IsHead)
            {
                return "boss_head";
            }
            return limb.Name.Contains("Arm") ? "boss_arm" : "boss_leg";
        }
        if (target is Barrel)
        {
            return "barrel";
        }
        if (target is EnergyOrb)
        {
            return "orb";
        }
        if (target is ShieldWord)
        {
            return "shield_word";
        }
        if (target is QuizAnswer)
        {
            return "quiz_answer";
        }
        return target.GetType().Name;
    }
}
