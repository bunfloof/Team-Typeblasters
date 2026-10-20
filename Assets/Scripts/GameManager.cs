// GameManager.cs
// ---------------------------------------------------------------------------
// The "brain" of the game. It owns:
//   - the game state (Start -> Playing -> Won or Lost)
//   - score, combo and player health
//   - the run's statistics (accuracy, speed, best combo...) for the end screen
//   - starting the game and restarting (reloading the scene)
//   - pausing (Esc, or the browser tab being hidden: PauseForHiddenTab) and
//     resuming with a 3-2-1 countdown. While paused,
//     Time.timeScale is 0, so everything that uses game time (movement, spawn
//     timers, boss attacks) freezes; the countdown uses real time.
//
// THE COMBO: +1 for every kill, -1 on a wrong key, back to 1 when you get hurt
// (only -1 for a bite from a zombie met on the run between two fights).
// Each kill is worth 10 x combo points, and the combo buys the combo weapons
// (FRENZY and RPG, see Powers). So clean typing pays twice.
//
// Other scripts reach it through GameManager.Instance, for example:
//   GameManager.Instance.AddKill();
//   if (GameManager.Instance.State == GameState.Playing) { ... }
//
// The references below (hud, spawner, powers, typing) are wired in the scene.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The states the game can be in. Most scripts only do their work while Playing.
public enum GameState
{
    Start,    // Start panel is showing, waiting for the Start button / Enter
    Playing,  // riding, fighting, typing
    Paused,   // Esc was pressed or the browser tab was hidden: pause panel, or the 3-2-1 countdown before play resumes
    Tip,      // a first-time tip box is showing (game frozen); Enter or OK continues
    Won,      // the last area was cleared, "You survived" panel is showing (Enter / Restart: back to the Start panel)
    Lost      // health reached 0, "You died" panel is showing (Enter / Restart: back to the Start panel)
}

public class GameManager : MonoBehaviour
{
    private static readonly string[] levelScenes = { "Easy", "Normal", "Hard" };
    public static int LevelCount => levelScenes.Length;
    public static string LevelName(int index) => levelScenes[index];
    public static int CurrentLevelIndex => System.Array.IndexOf(levelScenes, SceneManager.GetActiveScene().name);
    public static string NextLevelName => CurrentLevelIndex >= 0 && CurrentLevelIndex < LevelCount - 1
        ? levelScenes[CurrentLevelIndex + 1] : null;
    private bool loadingLevel;

    // The one GameManager in the scene. Set in Awake.
    public static GameManager Instance { get; private set; }

    [Header("Tuning")]
    [SerializeField] private int startingHealth = 100;

    [Header("Difficulty")]
    [SerializeField] private bool showComboWeaponsTip = true;

    [Header("References (wired in the scene)")]
    [SerializeField] private HUD hud;
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private Powers powers;
    [SerializeField] private TypingController typing;

    // Each kill is worth PointsPerKill x the current combo (x the multi-kill
    // multiplier when one blast kills several, see AddKills).
    private const int PointsPerKill = 10;

    public GameState State { get; private set; }
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Health { get; private set; }

    // The numbers shown on the end screen.
    public RunStats Stats { get; private set; }

    // The player's powers (lure bomb, freeze).
    public Powers Powers
    {
        get { return powers; }
    }

    // Used by Analytics to describe what is going on.
    public WaveSpawner Spawner
    {
        get { return spawner; }
    }

    public TypingController Typing
    {
        get { return typing; }
    }

    private void Awake()
    {
        Instance = this;
        State = GameState.Start;
        Time.timeScale = 1f; // never start frozen (timeScale survives a scene reload)
        Stats = new RunStats();
    }

    private void Start()
    {
        Score = 0;
        Combo = 1;
        Health = startingHealth;

        hud.SetScore(Score);
        hud.SetCombo(Combo);
        hud.SetHealth(Health, startingHealth);
        hud.ShowStartPanel();
        hud.SetTipsToggle(tipsEnabled); // after a Restart, show the choice the player made before
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (State != GameState.Playing)
        {
            return;
        }

        Stats.Seconds += Time.deltaTime;
    }

    // Tips are opened here, at the END of the frame, so everything that was
    // already happening this frame (bullets landing, a chain of kills) finishes
    // first. A tip also waits while the player is busy (see PlayerIsBusy).
    private void LateUpdate()
    {
        if (State == GameState.Playing && pendingTips.Count > 0 && !PlayerIsBusy())
        {
            ShowNextTip();
        }
    }

    // ---- First-time tips ----
    // Something new the first time (the first lure bomb, the first freeze, the
    // first WORD CHAIN pair) pauses the game with a box that explains it (built
    // by HUD). Enter or the OK button continues. Static, like Tutorial: after a
    // Restart they are not shown again.
    //
    // A tip never cuts into typing: it waits until no word is half-typed, no
    // lure bomb is being aimed, and nothing was typed for TipWaitSeconds (so
    // it does not pop up between two words typed one after the other). A tip
    // still waiting when the run ends is shown in the next run (RestartGame).
    //
    // The "Show tips" toggle on the Start panel turns them off (SetTipsEnabled).

    private const float TipWaitSeconds = 0.75f;

    private struct PendingTip
    {
        public string Key;
        public string Title;
        public string Text;
        public Color Color;
    }

    private static readonly HashSet<string> tipsShown = new HashSet<string>();
    private static bool tipsEnabled = true; // static: the choice survives a Restart
    private readonly Queue<PendingTip> pendingTips = new Queue<PendingTip>();

    // Called by the "Show tips" toggle on the Start panel (wired in the scene).
    public void SetTipsEnabled(bool on)
    {
        tipsEnabled = on;

        AnalyticsEvent e = Analytics.NewEvent("tips_toggle");
        e.Add("on", on);
        Analytics.Send(e);
    }

    // True while the player is in the middle of something a tip box would
    // interrupt (see the note above).
    private bool PlayerIsBusy()
    {
        return typing.IsTypingWord
            || typing.SecondsSinceTyping < TipWaitSeconds
            || powers.IsAiming;
    }

    // Shows the tip called key once (it opens as soon as the player is not busy).
    // With tips turned off, nothing is shown (and nothing is marked as shown).
    public void RequestTip(string key, string title, string text, Color titleColor)
    {
        if (!tipsEnabled)
        {
            return;
        }
        if (!tipsShown.Add(key))
        {
            return; // already explained
        }
        PendingTip tip;
        tip.Key = key;
        tip.Title = title;
        tip.Text = text;
        tip.Color = titleColor;
        pendingTips.Enqueue(tip);
    }

    // Called by Powers when a charge is gained. Only the first one of each kind shows a tip.
    public void RequestPowerTip(PowerKind kind, string title, string text)
    {
        RequestTip("power " + kind, title, text, kind == PowerKind.Lure ? Palette.LureCrate : Palette.FreezeCrate);
    }

    // The tip box on screen now, and when it opened (real time), for analytics.
    private string openTipKey;
    private float openTipSince;

    private void ShowNextTip()
    {
        PendingTip tip = pendingTips.Dequeue();
        State = GameState.Tip;
        Time.timeScale = 0f; // everything waits for the player
        hud.ShowPowerTip(tip.Title, tip.Text, tip.Color);
        openTipKey = tip.Key;
        openTipSince = Time.realtimeSinceStartup;
    }

    // Called by the tip box's OK button and by Enter (TypingController).
    public void ConfirmTip()
    {
        if (State != GameState.Tip)
        {
            return;
        }

        AnalyticsEvent e = Analytics.NewEvent("tip");
        e.Add("key", openTipKey);
        e.Add("seconds_open", Time.realtimeSinceStartup - openTipSince);
        Analytics.Send(e);

        hud.HidePowerTip();
        if (pendingTips.Count > 0)
        {
            ShowNextTip(); // got both powers at once: explain the next one too
            return;
        }
        State = GameState.Playing;
        Time.timeScale = 1f;
    }

    // Called by the Start button (wired in the scene) and by StartOrRestart().
    public void StartGame()
    {
        if (State != GameState.Start)
        {
            return;
        }

        State = GameState.Playing;
        hud.HideAllPanels();

        // Logged before the waves begin: the first ride logs right away.
        Analytics.StartRun();
        AnalyticsEvent e = Analytics.NewEvent("run_start");
        e.Add("run_number", Analytics.RunNumber);              // runs ever started in this browser
        e.Add("runs_this_session", Analytics.RunsThisSession); // runs since the page was loaded (each tip shows once per page load)
        e.Add("tips_enabled", tipsEnabled);
        e.Add("hard_level", HardLevel.Active != null);
        Analytics.Send(e);
        Analytics.FlushSoon();

        spawner.BeginWaves();

        // Before anything happens, explain what the combo buys (a tip box; the
        // game waits for Enter / OK). Shown once, not again after a Restart.
        if (showComboWeaponsTip)
        {
        RequestTip("combo weapons", "COMBO WEAPONS",
            "Type without mistakes to build your COMBO (top of the screen), then SPEND it on a weapon:\n\n"
            + "<color=#FF4D33>[3] FRENZY</color> - costs " + Powers.FrenzyCost + " combo: for 5 seconds every enemy word\n"
            + "turns into a TINY red word (1 to 4 letters - even a single letter!).\n"
            + "<color=#FF8C1A>[4] RPG</color> - costs " + Powers.RocketCost + " combo: your next finished word fires a rocket\n"
            + "that blows up everything around its target (it hurts the boss too).\n\n"
            + "HOLD 3 / 4 to GET a weapon (a bar fills under its slots): it is kept bottom-left,\n"
            + "2 slots each, until you want it. TAP 3 / 4 (or click its slot) to USE one.\n"
            + "Weaker against the boss!\n"
            + "One weapon per wave. A wrong key costs 1 combo; a bite resets it\n"
            + "(a bite on the run between two fights only costs 1).\n"
            + "Lure bombs [1] and freezes [2] come from supply crates.\n\n"
            + "<color=#59F2D9>[TAB] SHIELD</color> - once per level: press Tab, then TYPE the shield word\n"
            + "in time. No damage for a few seconds and every enemy slows down!",
            Palette.BlastOrange);
        }
    }

    // ---- Typing difficulty (picked on the Start panel, see TypingSettings) ----

    // Called by the difficulty buttons on the Start panel (HUD). Only before
    // the run starts: every word is styled when its enemy appears.
    public void SetTypingDifficulty(TypingDifficulty level)
    {
        if (State != GameState.Start)
        {
            return;
        }
        TypingSettings.Difficulty = level;
        hud.RefreshDifficulty();
    }

    // Called by TypingController: LEFT / RIGHT arrows on the Start panel.
    public void CycleTypingDifficulty(int step)
    {
        int count = TypingSettings.DifficultyCount;
        int next = ((int)TypingSettings.Difficulty + step % count + count) % count;
        SetTypingDifficulty((TypingDifficulty)next);
    }

    // Spends combo on a weapon (Powers): the combo goes down by 'amount' (never below 1).
    public void SpendCombo(int amount)
    {
        Combo = Mathf.Max(1, Combo - amount);
        hud.SetCombo(Combo);
        powers.OnComboChanged(Combo);
    }

    // Called by the Restart buttons (pause panel and the end screens) and by
    // Enter on the end screens: reloads the scene, which shows the Start panel,
    // the same screen as when the game is first opened.
    public void RestartGame()
    {
        LoadLevelScene(SceneManager.GetActiveScene().name);
    }

    // Called by the MENU buttons (Start, pause, win and lose panels): back to the Menu scene.
    public void LoadMenu()
    {
        LoadLevelScene("Menu");
    }

    // Level choices are available before play and after a run ends.
    public void SelectLevel(int index)
    {
        if (index < 0 || index >= LevelCount ||
            (State != GameState.Start && State != GameState.Won && State != GameState.Lost))
        {
            return;
        }
        if (State == GameState.Start && index == CurrentLevelIndex) return;
        LoadLevelScene(levelScenes[index]);
    }

    public void NextLevel()
    {
        if (State == GameState.Won && NextLevelName != null)
        {
            LoadLevelScene(NextLevelName);
        }
    }

    private void LoadLevelScene(string sceneName)
    {
        if (loadingLevel) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Level is missing from Build Settings: " + sceneName);
            return;
        }
        loadingLevel = true;
        // Restarted in the middle of a run (from the pause panel): it ends here.
        if (State == GameState.Playing || State == GameState.Paused || State == GameState.Tip)
        {
            LogRunEnd("restart");
        }

        // Tips still waiting (the player was busy until the end) were never
        // seen: forget them, so they are shown in the next run.
        foreach (PendingTip tip in pendingTips)
        {
            tipsShown.Remove(tip.Key);
        }

        Time.timeScale = 1f; // in case we restart from the pause panel
        Analytics.EndRun();  // the Start panel's events belong to no run
        SceneManager.LoadScene(sceneName);
    }

    // Called by TypingController when Enter is pressed outside of play
    // (Start panel, results screens, or a first-time power tip).
    public void StartOrRestart()
    {
        if (State == GameState.Start)
        {
            StartGame();
        }
        else if (State == GameState.Won || State == GameState.Lost)
        {
            RestartGame();
        }
        else if (State == GameState.Tip)
        {
            ConfirmTip();
        }
    }

    // ---- Pause ----

    private const int ResumeCountdownSeconds = 3;
    private Coroutine resumeCountdown; // running while the 3-2-1 countdown is on screen

    // Called by TypingController when Esc is pressed.
    //   Playing          -> pause
    //   paused (panel)   -> start the 3-2-1 countdown
    //   during countdown -> back to the pause panel
    public void OnPauseKey()
    {
        if (State == GameState.Playing)
        {
            Pause("esc_key");
        }
        else if (State == GameState.Paused)
        {
            if (resumeCountdown != null)
            {
                Pause("esc_key");
            }
            else
            {
                ResumeGame();
            }
        }
    }

    // For a pause button, if one is added (none is wired now).
    public void PauseGame()
    {
        Pause("button");
    }

    // Called by AnalyticsSender when the browser tab is hidden (the player
    // switched tabs or minimised the window): pause, so nobody dies while away.
    public void PauseForHiddenTab()
    {
        if (State == GameState.Playing || (State == GameState.Paused && resumeCountdown != null))
        {
            Pause("tab_hidden");
        }
    }

    private float pausedSince; // real time the current pause began (for analytics)

    // reason: what paused it, for analytics ("esc_key", "tab_hidden", "button").
    private void Pause(string reason)
    {
        if (State != GameState.Playing && State != GameState.Paused)
        {
            return;
        }

        bool duringCountdown = resumeCountdown != null;
        if (resumeCountdown != null)
        {
            StopCoroutine(resumeCountdown);
            resumeCountdown = null;
        }
        if (State == GameState.Playing)
        {
            pausedSince = Time.realtimeSinceStartup; // a pause during the countdown continues the same pause
        }

        State = GameState.Paused;
        Time.timeScale = 0f;
        hud.HideCountdown();
        hud.ShowPausePanel();

        AnalyticsEvent e = Analytics.NewEvent("pause");
        e.Add("reason", reason);
        e.Add("during_countdown", duringCountdown);
        Analytics.AddPlayerState(e);
        Analytics.Send(e);
        Analytics.FlushSoon();
    }

    // Called by the Resume button on the pause panel (wired in the scene) and by OnPauseKey().
    public void ResumeGame()
    {
        if (State != GameState.Paused || resumeCountdown != null)
        {
            return;
        }

        hud.HidePausePanel();
        resumeCountdown = StartCoroutine(ResumeCountdown());
    }

    private System.Collections.IEnumerator ResumeCountdown()
    {
        for (int number = ResumeCountdownSeconds; number >= 1; number--)
        {
            hud.ShowCountdown(number.ToString());
            yield return new WaitForSecondsRealtime(1f); // real time: game time is frozen
        }

        hud.HideCountdown();
        resumeCountdown = null;
        State = GameState.Playing;
        Time.timeScale = 1f;

        AnalyticsEvent e = Analytics.NewEvent("resume");
        e.Add("paused_seconds", Time.realtimeSinceStartup - pausedSince); // the countdown included
        Analytics.Send(e);
    }

    // ---- Typing statistics (called by TypingController) ----

    public void OnCorrectKey()
    {
        Stats.CorrectKeys += 1;
    }

    // A wrong key: counts against accuracy and costs one step of combo.
    public void OnWrongKey()
    {
        Stats.WrongKeys += 1;
        LoseComboStep();
    }

    // A wrong key costs one step of combo (never below 1). Getting hurt still
    // resets it completely (ResetCombo).
    public void LoseComboStep()
    {
        if (Combo <= 1)
        {
            return;
        }
        Combo -= 1;
        hud.SetCombo(Combo);
        powers.OnComboChanged(Combo);
    }

    // ---- Kills and score ----

    // WORD CHAIN (called by TypingController): one run of typing finished
    // 'words' words, e.g. typing "hunter" finished "hunt" on the way. Every one
    // of those kills already counts as usual (score, combo +1 each) when its
    // bullet lands; the chain adds a bonus of PointsPerKill x combo x words.
    public void AddWordChain(int words)
    {
        if (words < 2 || State == GameState.Lost)
        {
            return;
        }

        int bonus = PointsPerKill * Combo * words;
        Score += bonus;
        hud.SetScore(Score);
        hud.ShowWordChain(words, bonus);
    }

    // Called for a single kill (a normal zombie, a boss part, an orb...).
    // Returns the points it was worth.
    public int AddKill()
    {
        return AddKills(1);
    }

    // Called with every enemy killed by ONE shot at once (an explosion or a
    // chain reaction can kill several). Each kill is worth PointsPerKill x combo,
    // and the combo goes up by 1 per kill as usual. MULTI-KILL: when one shot
    // kills 2 or more, every one of those kills is also multiplied by that number
    // (3 kills at once = x3 points each), and the HUD shows "TRIPLE KILL!".
    // Returns the points gained.
    public int AddKills(int count)
    {
        if (count <= 0 || State == GameState.Lost)
        {
            return 0;
        }

        int multiplier = count >= 2 ? count : 1;
        int gained = 0;
        for (int i = 0; i < count; i++)
        {
            gained += PointsPerKill * Combo * multiplier;
            Combo += 1;
            powers.OnComboChanged(Combo); // the combo weapons line on the HUD follows the combo
        }

        Score += gained;
        Stats.Kills += count;
        Stats.BestCombo = Mathf.Max(Stats.BestCombo, Combo);
        Stats.BiggestMultiKill = Mathf.Max(Stats.BiggestMultiKill, count);
        hud.SetScore(Score);
        hud.SetCombo(Combo);

        if (count >= 2)
        {
            hud.ShowMultiKill(count, gained);
        }
        return gained;
    }

    // Called when the player takes damage (a wrong key only costs one step, see LoseComboStep).
    public void ResetCombo()
    {
        if (Combo == 1)
        {
            return;
        }
        Combo = 1;
        hud.SetCombo(Combo);
        powers.OnComboChanged(Combo);
    }

    // ---- Health ----

    // Gives health back (never above the starting health). Used by supply crates.
    public void Heal(int amount)
    {
        Health = Mathf.Min(startingHealth, Health + amount);
        hud.SetHealth(Health, startingHealth);
    }

    // Called by a zombie that reached the player, and by a boss orb that hit.
    // lightHit: a bite from a zombie met on the run between two fights (an
    // ambush, see WaveSpawner.RideAmbush) only costs one step of combo; any
    // other hit resets the combo.
    // hit: the "damage" analytics event, with what hit the player already
    // filled in (attacker: the zombie biting, or null, so it is not counted as
    // one of the zombies around). The player's state is added here, BEFORE the
    // damage. No default values on purpose: a call that forgets the analytics
    // does not compile.
    public void TakeDamage(int amount, bool lightHit, AnalyticsEvent hit, Zombie attacker)
    {
        if (State != GameState.Playing)
        {
            return;
        }

        // The shield (Tab) blocks every hit while it is up.
        if (Powers.IsShielded)
        {
            powers.OnShieldHit();
            if (hit != null)
            {
                hit.Add("amount", 0);
                hit.Add("attempted_amount", amount);
                hit.Add("shield_blocked", true);
                Analytics.AddPlayerState(hit, attacker);
                hit.Add("health_after", Health);
                hit.Add("combo_after", Combo);
                hit.Add("fatal", false);
                Analytics.Send(hit);
            }
            return;
        }

        if (hit != null)
        {
            hit.Add("amount", amount);
            hit.Add("shield_blocked", false);
            Analytics.AddPlayerState(hit, attacker); // health, combo... before the hit
        }

        Health -= amount;
        if (Health < 0)
        {
            Health = 0;
        }
        hud.SetHealth(Health, startingHealth);
        hud.FlashRed();
        CameraDirector.Shake(0.45f);
        if (lightHit)
        {
            LoseComboStep();
        }
        else
        {
            ResetCombo();
        }

        if (hit != null)
        {
            hit.Add("health_after", Health);
            hit.Add("combo_after", Combo);
            hit.Add("fatal", Health <= 0);
            Analytics.Send(hit); // sent before "run_end", so a death reads in order
        }

        if (Health <= 0)
        {
            fatalAttacker = attacker; // logs its own "zombie" event (bite) right after
            EndGame(false);
        }
    }

    private Zombie fatalAttacker; // the zombie whose bite ended the run (analytics)

    // ---- The end ----

    // Called by WaveSpawner when the last area has been cleared.
    public void WinGame()
    {
        EndGame(true);
    }

    private void EndGame(bool won)
    {
        if (State != GameState.Playing)
        {
            return;
        }

        // Logged while the state still says Playing (and before the waves stop).
        LogRunEnd(won ? "won" : "lost");

        State = won ? GameState.Won : GameState.Lost;
        if (!won)
        {
            spawner.StopWaves();
        }

        Stats.Score = Score;
        hud.ShowResults(won, Stats);
    }

    // The "run_end" analytics event: how the run ended and its statistics.
    // outcome: "won", "lost" or "restart" (Restart pressed in the middle of a run).
    // Then what was still going on is logged too: the word being typed
    // ("word", outcome "run_ended") and every zombie still alive ("zombie",
    // outcome "run_over"), which would otherwise never be logged.
    private void LogRunEnd(string outcome)
    {
        AnalyticsEvent e = Analytics.NewEvent("run_end");
        e.Add("outcome", outcome);
        e.Add("run_number", Analytics.RunNumber);
        e.Add("kills", Stats.Kills);
        e.Add("best_combo", Stats.BestCombo);
        e.Add("biggest_multi_kill", Stats.BiggestMultiKill);
        e.Add("correct_keys", Stats.CorrectKeys);
        e.Add("wrong_keys", Stats.WrongKeys);
        e.Add("accuracy", Stats.Accuracy);
        e.Add("words_per_minute", Stats.WordsPerMinute); // over all play time, rides included
        Analytics.AddPlayerState(e, fatalAttacker); // the biter logs itself ("bite"), not "run_over"
        Analytics.Send(e);

        typing.LogOpenAttempt();
        foreach (Zombie zombie in spawner.AliveZombies)
        {
            if (zombie != null && zombie.IsAlive && zombie != fatalAttacker)
            {
                zombie.LogRunOver();
            }
        }
        spawner.LogLurkersRunOver(); // ambushers still hiding on the road (not in AliveZombies)
        Analytics.FlushSoon();
    }
}
