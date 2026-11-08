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
// Each kill is worth 10 x combo points and earns one independent skill energy.
// Skills spend energy; the scoring combo is never charged.
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
    Lost,     // health reached 0, "You died" panel is showing (Enter / Restart: back to the Start panel)
    Reward    // choose rewards after completing a wave in endless mode
}

public class GameManager : MonoBehaviour
{
    // The playable levels, in order. The separate Easy tutorial level was removed:
    // Normal's first wave teaches the game itself (GuidedIntro).
    private static readonly string[] levelScenes = { "Normal", "Hard" };
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
    public int SkillEnergy { get; private set; }
    public int Health { get; private set; }

    // Full health (the health bar's length).
    public int MaxHealth
    {
        get { return startingHealth; }
    }

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
        // ENDLESS has no typing difficulty choice: one fixed style for everybody.
        TypingSettings.Override = spawner != null && spawner.IsEndlessMode ? EndlessTypingDifficulty : (TypingDifficulty?)null;
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

        // Died and chose to go on: straight back into the run, at the start
        // of the wave they died in (CHECKPOINT), with that wave's score and energy.
        if (Checkpoint.TakeResume(SceneManager.GetActiveScene().name))
        {
            Score = Checkpoint.Score;
            SkillEnergy = Checkpoint.Energy;
            hud.SetScore(Score);
            resumingFromCheckpoint = true;
            spawner.ResumeFromCheckpoint();
            StartGame();
            resumingFromCheckpoint = false;
            powers.OnComboChanged(Combo);
        }
    }

    private bool resumingFromCheckpoint;

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

    // Optional first-time cues never pause the game or open an instruction panel.
    private static readonly HashSet<string> tipsShown = new HashSet<string>();
    private static bool tipsEnabled = true;
    public void SetTipsEnabled(bool on)
    {
        tipsEnabled = on;
        AnalyticsEvent e = Analytics.NewEvent("tips_toggle"); e.Add("on",on); Analytics.Send(e);
    }
    public void RequestTip(string key, string title, string text, Color titleColor)
    {
        if (EasyTutorial.Active != null || !tipsEnabled || !tipsShown.Add(key)) return;
        hud.ShowHint(title,2f);
        AnalyticsEvent e = Analytics.NewEvent("hint"); e.Add("key",key); e.Add("nonblocking",true); Analytics.Send(e);
    }
    public void RequestPowerTip(PowerKind kind, string title, string text)
    {
        RequestTip("power " + kind,title,"",Color.white);
    }
    // Kept for existing serialized OK-button bindings in older scenes.
    public void ConfirmTip()
    {
        if (State != GameState.Tip) return;
        hud.HidePowerTip(); State = GameState.Playing; Time.timeScale = 1f;
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
        if (resumingFromCheckpoint)
        {
            DynamicDifficulty.ResumeRun(); // the speed measured before: no new first-wave measurement
        }
        else
        {
            DynamicDifficulty.BeginRun(); // ADAPTIVE DIFFICULTY: measure this run's typing from a first guess
        }

        // Logged before the waves begin: the first ride logs right away.
        Analytics.StartRun();
        AnalyticsEvent e = Analytics.NewEvent("run_start");
        e.Add("run_number", Analytics.RunNumber);              // runs ever started in this browser
        e.Add("runs_this_session", Analytics.RunsThisSession); // runs since the page was loaded (each tip shows once per page load)
        e.Add("tips_enabled", tipsEnabled);
        e.Add("hard_level", HardLevel.Active != null);
        e.Add("from_checkpoint_wave", resumingFromCheckpoint ? Checkpoint.WaveIndex + 1 : 0);
        DynamicDifficulty.AddTo(e);
        Analytics.Send(e);
        Analytics.FlushSoon();

        spawner.BeginWaves();

    }

    // ---- Typing difficulty (picked on the Start panel, see TypingSettings) ----

    // ENDLESS: no choice, every run is typed in this style (TypingSettings.Override).
    public const TypingDifficulty EndlessTypingDifficulty = TypingDifficulty.Easy;

    // Called by the difficulty buttons on the Start panel (HUD). Only before
    // the run starts: every word is styled when its enemy appears.
    public void SetTypingDifficulty(TypingDifficulty level)
    {
        if (State != GameState.Start || EasyTutorial.Active != null || TypingSettings.Override.HasValue)
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

    // The most skill energy the player can hold (more is lost): powers cannot be
    // saved up over a whole level.
    public const int MaxSkillEnergy = 20;

    // Skills spend independent energy; the scoring combo is never charged.
    public bool TrySpendSkillEnergy(int amount)
    {
        if (State != GameState.Playing || amount <= 0 || SkillEnergy < amount) return false;
        SkillEnergy -= amount;
        powers.OnComboChanged(Combo);
        return true;
    }

    // Practice energy never increases combo, score, or earned kill statistics.
    public bool AddSkillEnergy(int amount)
    {
        if (State != GameState.Playing || amount <= 0) return false;
        SkillEnergy = Mathf.Min(MaxSkillEnergy, SkillEnergy + amount);
        powers.OnComboChanged(Combo);
        return true;
    }

    public void GrantTrainingEnergy(int minimum)
    {
        SkillEnergy = Mathf.Max(SkillEnergy, minimum);
        powers.OnComboChanged(Combo);
    }

    // Called by the Restart buttons (pause panel and the end screens) and by
    // Enter on the end screens: reloads the scene, which shows the Start panel,
    // the same screen as when the game is first opened.
    // After a death with a checkpoint saved, it goes on from that wave instead
    // (Checkpoint); any other Restart starts the level from the beginning.
    public void RestartGame()
    {
        if (State == GameState.Paused)
        {
            hud.ShowConfirmation("Restart this level?", RestartGameConfirmed);
            return;
        }
        RestartGameConfirmed();
    }

    private void RestartGameConfirmed()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (State == GameState.Lost && Checkpoint.Has(scene))
        {
            Checkpoint.RequestResume();
        }
        else
        {
            Checkpoint.Clear();
        }
        LoadLevelScene(scene);
    }

    // Called by ordinary MENU buttons: return to the menu's main page.
    public void LoadMenu()
    {
        if (State == GameState.Paused)
        {
            hud.ShowConfirmation("Return to main menu?", LoadMenuConfirmed);
            return;
        }

        LoadMenuConfirmed();
    }

    private void LoadMenuConfirmed()
    {
        Checkpoint.Clear();
        LoadLevelScene("Menu");
    }

    // Called after the final campaign level: open Menu directly on SELECT LEVEL.
    public void LoadLevelSelectionMenu()
    {
        Checkpoint.Clear();
        LoadLevelScene("Menu", true);
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
        Checkpoint.Clear();
        LoadLevelScene(levelScenes[index]);
    }

    public void NextLevel()
    {
        if (State == GameState.Won && NextLevelName != null)
        {
            LoadLevelScene(NextLevelName);
        }
    }

    private void LoadLevelScene(string sceneName, bool openLevelSelection = false)
    {
        if (loadingLevel) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Level is missing from Build Settings: " + sceneName);
            return;
        }
        if (openLevelSelection)
        {
            MainMenu.RequestLevelSelection();
        }
        loadingLevel = true;
        // Restarted in the middle of a run (from the pause panel): it ends here.
        if (State == GameState.Playing || State == GameState.Paused || State == GameState.Tip || State == GameState.Reward)
        {
            LogRunEnd("restart");
        }

        Time.timeScale = 1f; // in case we restart from the pause panel
        Analytics.EndRun();  // the Start panel's events belong to no run
        // All scene transitions initiated by GameManager use the same fade.
        ScreenFade.LoadSceneWithFade(sceneName, 0f);
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
            // Esc closes the confirmation instead of resuming the game.
            if (hud != null && hud.IsConfirmationOpen)
            {
                hud.CancelConfirmation();
                return;
            }
        
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

    // ---- Endless wave rewards ----

    // After a normal wave / after a BOSS wave (every 10th): the boss's rewards are bigger.
    public const float EndlessRewardHealthFraction = 0.1f;      // HEALTH: 10% of full health
    public const float EndlessBossRewardHealthFraction = 0.2f;  // ...20% after a boss
    public const int EndlessRewardEnergy = 5;
    public const int EndlessBossRewardEnergy = 12;
    public const int EndlessRewardScore = 100;
    public const int EndlessBossRewardScore = 500;
    // FREE card: in the first EndlessEasyRewardWaves waves a free LURE BOMB or
    // FREEZE (one of the two, at random); later a free RPG or FRENZY; after a
    // boss BOTH weapons.
    public const int EndlessEasyRewardWaves = 5;

    private bool rewardAfterBoss; // the reward being chosen now follows a boss wave

    // Health points the HEALTH reward gives (a share of MaxHealth, at least 1).
    public int EndlessRewardHealth
    {
        get { return Mathf.Max(1, Mathf.RoundToInt(MaxHealth * (rewardAfterBoss ? EndlessBossRewardHealthFraction : EndlessRewardHealthFraction))); }
    }

    // Called by WaveSpawner after an Endless wave has been cleared.
    // afterBoss: the wave was a BOSS fight (bigger rewards, see above).
    public void BeginEndlessWaveReward(bool afterBoss = false)
    {
        if (State != GameState.Playing || spawner == null || !spawner.IsEndlessMode)
            return;

        State = GameState.Reward;
        Time.timeScale = 0f;
        rewardAfterBoss = afterBoss;

        // Decide the free item once, before displaying the reward card:
        // early waves a skill (lure bomb / freeze), later a weapon (RPG / frenzy).
        bool rewardSecond = Random.value < 0.5f; // RPG (not frenzy) / FREEZE (not lure)
        bool rewardSkill = !afterBoss && spawner.CurrentWave <= EndlessEasyRewardWaves;
        string freeName = afterBoss ? "RPG + FRENZY"
            : rewardSkill ? (rewardSecond ? "FREEZE" : "LURE BOMB")
            : rewardSecond ? "RPG" : "FRENZY";

        hud.ShowEndlessWaveReward(
            freeName,
            choice => CompleteEndlessWaveReward(choice, rewardSecond, rewardSkill),
            afterBoss);
    }

    // rewardSecond: the FREE card's second item (RPG / FREEZE); rewardSkill: it is a skill, not a weapon.
    private void CompleteEndlessWaveReward(EndlessWaveRewardChoice choice, bool rewardSecond, bool rewardSkill)
    {
        if (State != GameState.Reward)
            return;

        int healthBefore = Health;
        int energyBefore = SkillEnergy;
        int scoreBefore = Score;

        string weapon = null;

        switch (choice)
        {
            case EndlessWaveRewardChoice.Health:
                Heal(EndlessRewardHealth);
                break;

            case EndlessWaveRewardChoice.Energy:
                SkillEnergy = Mathf.Min(
                    MaxSkillEnergy,
                    SkillEnergy + (rewardAfterBoss ? EndlessBossRewardEnergy : EndlessRewardEnergy));

                powers.OnComboChanged(Combo);
                break;

            case EndlessWaveRewardChoice.FreeWeapon:
                if (rewardAfterBoss)
                {
                    // Both weapons (each is skipped if one is already kept).
                    string rocket = powers.GrantFreeWeaponReward(true);
                    string frenzy = powers.GrantFreeWeaponReward(false);
                    weapon = rocket != null && frenzy != null ? rocket + "+" + frenzy : rocket ?? frenzy;
                }
                else if (rewardSkill)
                {
                    weapon = powers.GrantFreePowerReward(rewardSecond); // a free freeze / lure bomb
                }
                else
                {
                    weapon = powers.GrantFreeWeaponReward(rewardSecond);
                }
                break;

            case EndlessWaveRewardChoice.Skip:
                Score += rewardAfterBoss ? EndlessBossRewardScore : EndlessRewardScore;
                hud.SetScore(Score);
                break;

            default:
                return;
        }

        int healthGained = Health - healthBefore;
        int energyGained = SkillEnergy - energyBefore;
        int scoreGained = Score - scoreBefore;

        // Record the reward selection.
        string choiceName = choice == EndlessWaveRewardChoice.FreeWeapon
            ? "free_weapon"
            : choice.ToString().ToLowerInvariant();

        AnalyticsEvent e = Analytics.NewEvent("wave_reward");
        e.Add("wave_number", spawner.CurrentWave);
        e.Add("after_boss", rewardAfterBoss);
        e.Add("choice", choiceName);
        e.Add("health_gained", healthGained);
        e.Add("energy_gained", energyGained);
        e.Add("score_gained", scoreGained);

        if (!string.IsNullOrEmpty(weapon))
            e.Add("weapon", weapon);

        Analytics.AddPlayerState(e);
        Analytics.Send(e);
        Analytics.FlushSoon();

        // Show the actual amount gained (rewards may be capped).
        switch (choice)
        {
            case EndlessWaveRewardChoice.Health:
                hud.ShowHint(
                    healthGained > 0 ? "+" + healthGained + " HEALTH" : "HEALTH FULL",
                    2f);
                break;

            case EndlessWaveRewardChoice.Energy:
                hud.ShowHint(
                    energyGained > 0 ? "+" + energyGained + " ENERGY" : "ENERGY FULL",
                    2f);
                break;

            case EndlessWaveRewardChoice.Skip:
                hud.ShowHint("+" + scoreGained + " SCORE", 2f);
                break;
        }

        // Resume the existing Endless run.
        State = GameState.Playing;
        Time.timeScale = 1f;
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
    // earnsEnergy: false for the kills of a blast a POWER set off (a lure bomb,
    // the RPG), so a power never pays for the next one. Typed kills and the
    // kills of a blast the player typed off (a barrel, a red zombie) earn 1 each.
    public int AddKills(int count, bool earnsEnergy = true)
    {
        if (EasyTutorial.Active != null && EasyTutorial.Active.IsDemonstrating) return 0;
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
            if (earnsEnergy)
            {
                SkillEnergy = Mathf.Min(MaxSkillEnergy, SkillEnergy + 1);
            }
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

        // ADAPTIVE DIFFICULTY: every hit is a little lighter for slower typists
        // and a little heavier for faster ones (bites, spit, boss orbs alike).
        amount = Mathf.Max(1, Mathf.RoundToInt(amount * DynamicDifficulty.DamageFactor));
        if (GuidedIntro.Running)
        {
            amount = Mathf.Max(1, amount / 2); // Normal's guided first wave is for learning: half damage
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

        if (EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave) return;
        DynamicDifficulty.OnDamage(amount); // getting hurt eases the adaptive difficulty a little, for a while

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

#if UNITY_EDITOR
    public void CompleteLevelForDebug()
    {
        if (State != GameState.Playing)
            return;

        if (spawner != null)
            spawner.StopWaves();

        GuidedIntro.Abandon();

        if (spawner != null && spawner.IsEndlessMode)
        {
            EndGame(false);
        }
        else
        {
            WinGame();
        }
    }

    public void CompleteCurrentWaveForDebug()
    {
        if (State != GameState.Playing || spawner == null)
        {
            return;
        }

        spawner.CompleteCurrentWaveForDebug();
    }
#endif

    private void EndGame(bool won)
    {
        if (State != GameState.Playing)
        {
            return;
        }

        // Logged while the state still says Playing (and before the waves stop).
        LogRunEnd(won ? "won" : "lost");

        State = won ? GameState.Won : GameState.Lost;
        if (won)
        {
            Checkpoint.Clear();
        }
        if (!won)
        {
            spawner.StopWaves();
        }

        Stats.Score = Score;

        EndlessRecordSnapshot endlessRecord = null;
        
        if (spawner != null && spawner.IsEndlessMode)
        {
            endlessRecord = EndlessRecords.SubmitRun(spawner.CurrentWave, Score);
        }
        
        hud.ShowResults(won, Stats, endlessRecord);
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
        DynamicDifficulty.AddTo(e); // the adaptive difficulty's measured speed (active typing only) and level
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
