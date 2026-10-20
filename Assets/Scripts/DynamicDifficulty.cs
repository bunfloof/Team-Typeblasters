// DynamicDifficulty.cs
// ---------------------------------------------------------------------------
// ADAPTIVE DIFFICULTY (on by default, switched in the SETTINGS panel, see
// SettingsPanel). It measures how fast the player really types in THIS game
// and tunes the pressure to it, so a 25 WPM beginner and a 110 WPM typist
// both get a fight that is hard but survivable. Each level keeps its own
// character (Easy is still easier than Hard): everything here is a
// MULTIPLIER on the level's own tuning.
//
// 1. MEASURING (TypingController reports every key, see OnCorrectKey)
//    Speed is WPM on the same scale as a typing test: correct characters per
//    second x 12 (one word = 5 characters, so WPM = chars/s x 60 / 5),
//    measured from the gap between two keys INSIDE a word. The gap before a
//    word's first key (finding and reading the next target) is left out of
//    the speed: it is about the same for everybody (half a second to a
//    second), so counting it would squeeze every player toward the middle
//    (a 120 WPM typist read as about 80 in testing).
//    The time lost to wrong keys stays in (it is in the gap to the next
//    correct key), so this is a NET speed, like a typing test's.
//      - a gap inside a word longer than MaxWordGapSeconds counts as that;
//      - keys typed during a FRENZY (1-4 letter words) are not measured.
//    Because the words themselves are measured, the typing difficulty
//    (Extreme's "#water", "Rate*9") and the touch keyboard on phones are taken
//    into account by themselves: harder words read as a slower typist.
//
// 2. CALIBRATION: the first CalibrationSeconds (10 s) of ACTIVE typing.
//    Active time counts every key's gap (word starts included, up to
//    MaxGapSeconds each) but never the time when there was NOTHING to type
//    (the ride, a cleared area): that clock starts again when a target
//    appears (Tick).
//    Until then the estimate is pulled toward a cautious PRIOR (PriorWpm =
//    40, a bit under the average typist, or this session's last measured
//    speed) worth PriorCharacters of typing (a shrinkage estimate: the first
//    couple of words cannot swing it). At the end of calibration the prior
//    is dropped: the measured speed is the player's own keys only (within
//    about 5% at every speed from 15 to 120 WPM in a simulation). After that
//    the estimate keeps following the player more slowly (an exponential
//    average over the last ~TrackingHalfLifeChars characters), so warming
//    up, getting tired or improving over a run are followed too.
//
// 3. THE TARGET LEVEL (a number around 0: below = gentler, above = harder)
//    level = log2(WPM / ReferenceWpm)              (0 at 50 WPM, -1 at 25, +1 at 100)
//          - accuracy penalty  (up to 0.35 below 92% correct keys)
//          - damage penalty    (up to 0.30 right after taking hits, fades)
//    clamped to [MinLevel, MaxLevel]. A log scale because typing speed is
//    a ratio: going from 25 to 50 WPM is the same step as from 50 to 100.
//
// 4. SMOOTHING: the level actually used (Level) moves toward the target at
//    most CatchUpRate per second until it first reaches it after calibration
//    (a few seconds, so the change lands inside the first wave), then at most
//    TrackingRate per second, with a small dead band. Zombies never jump in
//    speed, and the level cannot swing back and forth with each word.
//
// 5. WHAT IT CHANGES (all neutral at level 0). Above 0 the curves are
//    steeper than below: a fast typist kills a zombie the moment it can be
//    read, so walking speed alone barely matters; what challenges them is
//    MORE zombies on screen at once. Below 0 nothing is added or removed.
//    PaceFactor          2^(0.42 x level)   zombie walking speed, spitters,
//                        (0.55 above 0)     the boss's attack clock, orbs
//                                           (Powers.EnemySpeedFactor)
//    SpawnIntervalFactor 2^(-0.32 x level)  time between packs (WaveSpawner)
//                        (-0.45 above 0)
//    ExtraZombieFraction 0.35 x level       fast typists only: this much more
//                                           of the wave comes at the end of it
//    PackBonus           +1 from level 0.6  fast typists only: one more zombie
//                                           in every pack
//    ShortWordShift      -0.22 x level      chance of a short word for a
//                                           normal zombie (WordBank.PickWord)
//    WordLengthBias      0.6 x level        "best of two": slower typists
//                                           tend to get the shorter of two
//                                           words, faster ones the longer
//    LongWordChance      fast typists only  some normal zombies carry long words
//    For players at or below the reference speed the number of zombies never
//    changes: the level is as long as designed, it just comes slower.
//
// Static (like TypingSettings) so the measured speed carries over from one
// level to the next and to a Restart in the same session.
// ---------------------------------------------------------------------------
using UnityEngine;

public static class DynamicDifficulty
{
    // ---- The setting (PlayerPrefs, on by default) ----

    private const string PrefKey = "DynamicDifficulty";
    private static bool loaded;
    private static bool enabled;

    public static bool Enabled
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                enabled = PlayerPrefs.GetInt(PrefKey, 1) == 1;
            }
            return enabled;
        }
        set
        {
            loaded = true;
            if (enabled == value)
            {
                return;
            }
            enabled = value;
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();

            AnalyticsEvent e = Analytics.NewEvent("dda_toggle");
            e.Add("on", value);
            e.Add("dda_wpm", EstimatedWpm);
            Analytics.Send(e);
        }
    }

    // ---- Tuning ----

    public const float ReferenceWpm = 50f;          // the speed the levels are tuned for (the average typist is about 52 WPM)
    private const float MinWpm = 20f;               // speeds outside this range are treated as these
    private const float MaxWpm = 120f;
    private const float PriorWpm = 40f;             // the first guess: a little gentle (losing a slow player in 10 s is worse than boring a fast one for 10 s)
    private const float PriorCharacters = 6f;       // how much typing the first guess is worth (until calibrated)
    public const float CalibrationSeconds = 10f;    // active typing before the speed counts as measured...
    private const int CalibrationMinCharacters = 15; // ...and at least this many measured key gaps
    private const float MaxGapSeconds = 1.5f;       // active time: a longer gap between two keys counts as this long
    private const float MaxWordGapSeconds = 1f;     // speed: a longer gap inside a word counts as this long (12 WPM)
    private const float TrackingHalfLifeChars = 150f; // after calibration: the last ~150 characters count most

    private const float AccuracyHalfLifeKeys = 40f;
    private const float AccuracyGood = 0.92f;       // no penalty at or above this
    private const float AccuracyPenaltyRange = 0.17f; // full penalty 17 points below (75%)
    private const float MaxAccuracyPenalty = 0.35f;

    private const float DamagePerHealth = 0.008f;   // a 10 damage bite adds 0.08
    private const float MaxDamagePenalty = 0.3f;
    private const float DamageHalfLifeSeconds = 20f;

    private const float MinLevel = -1.4f;
    private const float MaxLevel = 1.3f;
    private const float CatchUpRate = 0.25f;        // level units per second until the level first reaches its target after calibration
    private const float TrackingRate = 0.08f;       // ...and after
    private const float DeadBand = 0.04f;

    private const float PaceExponent = 0.42f;       // below level 0 (slower typists)...
    private const float PaceExponentUp = 0.55f;     // ...and above it (faster typists)
    private const float SpawnExponent = -0.32f;
    private const float SpawnExponentUp = -0.45f;
    private const float ExtraZombieSlope = 0.35f;   // fast typists: extra zombies per wave, as a fraction of the wave
    private const float PackBonusLevel = 0.6f;      // fast typists: one more zombie per pack from this level
    private const float ShortWordSlope = -0.22f;
    private const float LengthBiasSlope = 0.6f;

    // ---- State ----

    private static float weightedChars;   // the measured characters and seconds (the speed is their ratio)
    private static float weightedSeconds;
    private static float priorWpm = PriorWpm; // this run's first guess (only used until calibrated)
    private static float activeSeconds;   // measured typing time in this run
    private static int measuredChars;     // correct characters measured in this run
    private static float accuracy = 0.95f;
    private static float damagePenalty;
    private static float level;           // the level in use (smoothed)
    private static float lastKeyTime;     // Time.time of the last key (or of a target appearing)
    private static bool hadTargets;
    private static bool converged;        // the level has reached its target once since calibration
    private static float sessionWpm = -1f; // the last measured speed this session (-1 = none yet)

    // True once the first CalibrationSeconds of typing have been measured in this run.
    public static bool Calibrated { get; private set; }

    // The player's speed as measured now (the first guess before any typing).
    public static float EstimatedWpm
    {
        get
        {
            if (Calibrated && weightedSeconds > 0f)
            {
                return 12f * weightedChars / weightedSeconds;
            }
            float priorSeconds = PriorCharacters * 12f / priorWpm;
            return 12f * (weightedChars + PriorCharacters) / (weightedSeconds + priorSeconds);
        }
    }

    // The last speed measured this session, or -1 (shown in the settings panel).
    public static float SessionWpm
    {
        get { return sessionWpm; }
    }

    // Recent accuracy (0 to 1), weighted toward the last ~40 keys.
    public static float Accuracy
    {
        get { return accuracy; }
    }

    // The level in use: 0 = the level as designed, negative = gentler, positive = harder.
    public static float Level
    {
        get { return level; }
    }

    // ---- What the game reads (all exactly 1 or 0 at level 0, i.e. when switched off) ----

    // Enemies' speed and attack clocks (multiplied into Powers.EnemySpeedFactor).
    public static float PaceFactor
    {
        get { return Mathf.Pow(2f, (level > 0f ? PaceExponentUp : PaceExponent) * level); }
    }

    // Time between packs of zombies (WaveSpawner).
    public static float SpawnIntervalFactor
    {
        get { return Mathf.Pow(2f, (level > 0f ? SpawnExponentUp : SpawnExponent) * level); }
    }

    // Added to a normal zombie's chance of a short word (WordBank.PickWord).
    public static float ShortWordShift
    {
        get { return ShortWordSlope * level; }
    }

    // -1 to 1: below 0 the shorter of two words is picked more often, above 0 the longer (WordBank).
    public static float WordLengthBias
    {
        get { return Mathf.Clamp(LengthBiasSlope * level, -1f, 1f); }
    }

    // Fast typists only: extra zombies for a wave of 'waveSize' (0 at or below the reference speed).
    public static int ExtraZombies(int waveSize)
    {
        return Mathf.RoundToInt(waveSize * ExtraZombieSlope * Mathf.Max(0f, level));
    }

    // Fast typists only: added to the size of every pack of zombies.
    public static int PackBonus
    {
        get { return level >= PackBonusLevel ? 1 : 0; }
    }

    // Chance that a normal zombie gets a LONG word (only for fast typists, up to 20%).
    public static float LongWordChance
    {
        get { return Mathf.Clamp01((level - 0.5f) * 0.25f); }
    }

    // ---- Called by the game ----

    // GameManager.StartGame: a new run. The speed is measured again from a
    // first guess (this session's last speed, or PriorWpm).
    public static void BeginRun()
    {
        priorWpm = sessionWpm > 0f ? sessionWpm : PriorWpm;
        weightedChars = 0f;
        weightedSeconds = 0f;
        activeSeconds = 0f;
        measuredChars = 0;
        accuracy = 0.95f;
        damagePenalty = 0f;
        Calibrated = false;
        converged = false;
        hadTargets = false;
        lastKeyTime = Time.time;
        level = Enabled ? TargetLevel() : 0f; // starts there: no ramp at the start of a level
    }

    // Every frame of play (TypingController). anyTargets: something can be typed now.
    public static void Tick(float deltaTime, bool anyTargets)
    {
        // Nothing to type: the player is not slow, they are waiting. The clock
        // for the next key starts when a target appears.
        if (!anyTargets || !hadTargets)
        {
            lastKeyTime = Time.time;
        }
        hadTargets = anyTargets;

        damagePenalty *= Mathf.Pow(0.5f, deltaTime / DamageHalfLifeSeconds);

        float target = Enabled ? TargetLevel() : 0f;
        float difference = target - level;
        if (Calibrated && Mathf.Abs(difference) <= DeadBand)
        {
            converged = true;
        }
        if (Mathf.Abs(difference) > DeadBand || !Enabled)
        {
            float rate = converged && Enabled ? TrackingRate : CatchUpRate;
            level = Mathf.MoveTowards(level, target, rate * deltaTime);
        }
    }

    // A correct key (TypingController, after it matched a target).
    // startsWord: the first key of a word (its gap is finding the word, not typing it).
    public static void OnCorrectKey(bool startsWord)
    {
        AddAccuracy(1f);
        float gap = Time.time - lastKeyTime;
        lastKeyTime = Time.time;
        if (Powers.IsFrenzy)
        {
            return; // tiny frenzy words are not normal typing
        }
        activeSeconds += Mathf.Min(gap, MaxGapSeconds);
        if (startsWord)
        {
            return; // its gap was finding the word, not typing it
        }
        // (Two keys in one frame give a gap of 0 here and the whole gap to the
        // first of them: the total time is still right, so both count.)

        // After calibration older typing fades, so the estimate follows the player.
        if (Calibrated)
        {
            float keep = Mathf.Pow(0.5f, 1f / TrackingHalfLifeChars);
            weightedChars *= keep;
            weightedSeconds *= keep;
        }
        weightedChars += 1f;
        weightedSeconds += Mathf.Min(gap, MaxWordGapSeconds);
        measuredChars += 1;

        if (!Calibrated && activeSeconds >= CalibrationSeconds && measuredChars >= CalibrationMinCharacters)
        {
            FinishCalibration();
        }
        if (Calibrated)
        {
            sessionWpm = EstimatedWpm;
        }
    }

    // A wrong key: only counts against accuracy (its time is in the next correct key's gap).
    public static void OnWrongKey()
    {
        AddAccuracy(0f);
    }

    // GameManager.TakeDamage (not when the shield blocked it).
    public static void OnDamage(int amount)
    {
        damagePenalty = Mathf.Min(MaxDamagePenalty, damagePenalty + amount * DamagePerHealth);
    }

    // ---- Inside ----

    private static float TargetLevel()
    {
        float wpm = Mathf.Clamp(EstimatedWpm, MinWpm, MaxWpm);
        float speedLevel = Mathf.Log(wpm / ReferenceWpm, 2f);
        float accuracyPenalty = Mathf.Clamp01((AccuracyGood - accuracy) / AccuracyPenaltyRange) * MaxAccuracyPenalty;
        return Mathf.Clamp(speedLevel - accuracyPenalty - damagePenalty, MinLevel, MaxLevel);
    }

    private static void AddAccuracy(float correct)
    {
        float keep = Mathf.Pow(0.5f, 1f / AccuracyHalfLifeKeys);
        accuracy = accuracy * keep + correct * (1f - keep);
    }

    private static void FinishCalibration()
    {
        Calibrated = true; // from now on EstimatedWpm is the measured keys only (the prior is dropped)
        sessionWpm = EstimatedWpm;

        AnalyticsEvent e = Analytics.NewEvent("dda_calibrated");
        e.Add("dda_wpm", EstimatedWpm);
        e.Add("dda_accuracy", accuracy);
        e.Add("dda_target_level", TargetLevel());
        e.Add("dda_level", level);
        e.Add("active_seconds", activeSeconds);
        e.Add("characters", measuredChars);
        e.Add("on", Enabled);
        Analytics.Send(e);

        if (Enabled)
        {
            Tutorial.Once("dda", "AUTO DIFFICULTY: tuned to your speed (about "
                + Mathf.RoundToInt(EstimatedWpm) + " WPM). Change it in SETTINGS (pause with Esc).", 5f);
        }
    }

    // One line for the editor's debug readout (HUD): what the adaptive difficulty sees and does.
    public static string DebugText()
    {
        if (!Enabled)
        {
            return "ADAPTIVE OFF";
        }
        return "ADAPTIVE  " + Mathf.RoundToInt(EstimatedWpm) + " WPM" + (Calibrated ? "" : " (measuring " + Mathf.FloorToInt(activeSeconds) + "s)")
            + "   acc " + Mathf.RoundToInt(accuracy * 100f) + "%"
            + "   level " + level.ToString("+0.00;-0.00") + " -> " + TargetLevel().ToString("+0.00;-0.00")
            + "\nzombie speed x" + PaceFactor.ToString("0.00")
            + "   spawn gap x" + SpawnIntervalFactor.ToString("0.00")
            + "\nextra zombies +" + Mathf.RoundToInt(ExtraZombieSlope * Mathf.Max(0f, level) * 100f) + "%"
            + "   pack +" + PackBonus
            + "   short words " + Mathf.RoundToInt(100f * Mathf.Clamp(0.60f + ShortWordShift, 0.3f, 0.92f)) + "%";
    }

    // For analytics events ("run_end").
    public static void AddTo(AnalyticsEvent e)
    {
        e.Add("dda_on", Enabled);
        e.Add("dda_calibrated", Calibrated);
        e.Add("dda_wpm", EstimatedWpm);
        e.Add("dda_level", level);
    }
}
