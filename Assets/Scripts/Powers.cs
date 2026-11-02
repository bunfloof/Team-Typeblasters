// Powers.cs
// ---------------------------------------------------------------------------
// The player's special powers, used with the number keys (and Tab):
//
//   [1] LURE BOMB - HOLD 1: a dashed arc (ThrowArc) shows where it will land,
//       throwDistance metres ahead, where you look, and a hint (bottom-right)
//       says to use the ARROW keys: Left / Right turn the throw, Up / Down
//       move the landing point farther / closer. RELEASE 1 to throw it there
//       (a quick tap throws straight ahead).
//       It blinks; every zombie near it walks to it and crowds around it
//       (they forget about you), then it explodes. See LureBomb.
//   [2] FREEZE    - every zombie, energy orb and the boss stop for
//       freezeSeconds. You keep typing. While frozen, IsFrozen is true.
//
// Charges (max maxCharges of each) come ONLY from supply crates in the level.
//
//   [Tab] SHIELD  - NOT from crates: shieldChargesPerLevel charges for the
//       whole level (1 by default, given at the start). Tab only brings up a
//       SHIELD WORD (ShieldWord, e.g. "protect", styled for the typing
//       difficulty): the player must TYPE it within shieldWordSeconds, while
//       the enemies keep coming. Too slow, or Tab again: no shield, but the
//       charge is kept. Once the word is typed, for shieldSeconds the
//       player takes NO damage and every enemy (zombies, energy orbs, the
//       boss's attack timer) moves at shieldEnemySpeed: time to think and
//       pick targets. A zombie that reaches the player bounces off the shield
//       and stays alive, so it can still be shot. While it is up, IsShielded
//       is true and EnemySpeedFactor is below 1.
//
// Skills use independent energy: tap 3 (10) or 4 (15). Combo is only a score multiplier.
using System.Collections.Generic;
using UnityEngine;

public enum PowerKind
{
    Lure,    // key 1: lure bomb
    Freeze   // key 2: freeze every zombie
}

public class Powers : MonoBehaviour
{
    [Header("Charges")]
    [SerializeField] private int maxCharges = 3;

    // Independent skill energy costs, displayed beside each action.
    public const int FrenzyCost = 10;
    public const int RocketCost = 15;
    public const int LureCost = 4;
    public const int FreezeCost = 5;

    [Header("Energy skills")]
    [SerializeField] private float frenzySeconds = 5f;    // enemy words are short this long


    [Header("Lure bomb")]
    [SerializeField] private float throwDistance = 9f;    // metres ahead of the player (a quick tap)
    [SerializeField] private float minThrowDistance = 3f; // never closer than this (e.g. facing a wall)
    [SerializeField] private float maxThrowDistance = 22f; // the farthest the Up arrow can push it
    [SerializeField] private float aimTurnSpeed = 90f;    // degrees per second the Left / Right arrows turn the throw
    [SerializeField] private float maxAimTurn = 60f;      // degrees left or right of where the camera looks, at most
    [SerializeField] private float aimDistanceSpeed = 10f; // metres per second the Up / Down arrows move the landing point

    [Header("Freeze")]
    [SerializeField] private float freezeSeconds = 4f;

    [Header("Shield")]
    [SerializeField] private int shieldChargesPerLevel = 1;  // given at the start of the level, never refilled
    [SerializeField] private float shieldSeconds = 8f;
    [SerializeField, Range(0.1f, 1f)] private float shieldEnemySpeed = 0.35f; // enemies move at this fraction of their speed
    [SerializeField] private float shieldWordSeconds = 6f;   // time to type the shield word after pressing Tab

    [Header("References (wired in the scene)")]
    [SerializeField] private HUD hud;
    [SerializeField] private Transform player;

    // Static so that zombies, orbs and the boss can simply check Powers.IsFrozen.
    // Reset in Awake, because static values survive a scene reload.
    private static float freezeTimeLeft;

    // True while the freeze power is on: zombies, orbs and the boss stand still.
    public static bool IsFrozen
    {
        get { return freezeTimeLeft > 0f; }
    }

    // The shield. Static like the freeze (reset in Awake).
    private static float shieldTimeLeft;
    private static float shieldSlow = 1f;

    // True while the shield is up: the player takes no damage.
    public static bool IsShielded
    {
        get { return shieldTimeLeft > 0f; }
    }

    // Enemies multiply their speed (and their attack clocks) by this: below 1
    // while the shield is up, and scaled to the player's typing speed by the
    // ADAPTIVE DIFFICULTY (DynamicDifficulty.PaceFactor, 1 when it is off).
    public static float EnemySpeedFactor
    {
        get { return (IsShielded ? shieldSlow : 1f) * DynamicDifficulty.PaceFactor; }
    }

    public int ShieldCharges { get; private set; }

    // Successful actions (and refusals) are observable without coupling powers to a level.
    public static event System.Action<string, string> ActionPerformed;

    public void RefillTrainingSupplies()
    {
        ShieldCharges = Mathf.Max(ShieldCharges, 1);

        RefreshHud();
        RefreshShieldHud();
        RefreshWeaponStatus();
    }

    private float shieldWordTimeLeft; // > 0 while the shield word is waiting to be typed

    public void ResetTrainingState()
    {
        if (EasyTutorial.Active == null) return;
        StopAiming();
        if (IsFrenzy) RestoreRealWords();
        freezeTimeLeft = frenzyTimeLeft = shieldTimeLeft = shieldWordTimeLeft = 0f;
        shieldSlow = 1f;
        ShieldWord.Close();
        RocketLoaded = false;
        FrenzyKept = RocketKept = 0;

        RefreshWeaponSlots();
        RefillTrainingSupplies();
    }



    // Combo weapons. Static like the freeze (reset in Awake).
    private static float frenzyTimeLeft;

    // True during a FRENZY: every enemy word is short.
    public static bool IsFrenzy
    {
        get { return frenzyTimeLeft > 0f; }
    }

    // True once a kept RPG is used (loaded) and not fired yet.
    public bool RocketLoaded { get; private set; }

    // Legacy telemetry/scene compatibility. Inventory is no longer awarded or displayed.
    public const int MaxKept = 2;         // slots per weapon
    public int FrenzyKept { get; private set; }
    public int RocketKept { get; private set; }

    // Skill energy is owned by GameManager.

    private string lastWeaponStatus;     // what the HUD shows now, so it is only rebuilt when it changes

    public int LureCharges { get; private set; }
    public int FreezeCharges { get; private set; }

    // Lure bomb aiming (while the 1 key is held).
    private bool aiming;

    // True while the player holds 1 to aim a lure bomb (the camera widens its view).
    public bool IsAiming
    {
        get { return aiming; }
    }
    private ThrowArc arc;        // the dashed preview, built on first use
    private float aimTurn;       // degrees the throw is turned right (< 0 = left) by the arrow keys
    private float aimDistance;   // metres: starts at throwDistance, Up / Down change it
    private Vector2 arrowInput;  // this frame's arrow keys: x = Right - Left, y = Up - Down (set by TypingController)

    private void Awake()
    {
        freezeTimeLeft = 0f;
        frenzyTimeLeft = 0f;
        shieldTimeLeft = 0f;
        shieldSlow = 1f;
        ShieldWord.Reset();
    }

    private void Start()
    {
        ShieldCharges = shieldChargesPerLevel;
        RefreshShieldHud();
        RefreshHud();
        RefreshWeaponSlots();
        OnComboChanged(1);
    }

    private void Update()
    {
        UpdateAim();


        if (GameManager.Instance.State == GameState.Playing && frenzyTimeLeft > 0f)
        {
            frenzyTimeLeft = Mathf.Max(0f, frenzyTimeLeft - Time.deltaTime);
            if (frenzyTimeLeft <= 0f)
            {
                RestoreRealWords();
                hud.ShowHint("Frenzy over.", 1.5f);

                AnalyticsEvent e = PowerEvent("frenzy_end", "frenzy");
                e.Add("kills_during", GameManager.Instance.Stats.Kills - killsAtFrenzyStart);
                SendPowerEvent(e);
            }
        }
        RefreshWeaponStatus();
        UpdateShield();

        if (freezeTimeLeft <= 0f || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        freezeTimeLeft -= Time.deltaTime;
        if (freezeTimeLeft <= 0f)
        {
            freezeTimeLeft = 0f;
        }

    }

    // ---- Earning charges ----

    // Gives the player one more charge. Returns false (and shows nothing) if
    // that power is already full (maxCharges). On a gain, a "+1" rises next to
    // the power on the HUD, and the very first charge of each power asks for a
    // tip box explaining it (GameManager.RequestPowerTip: the box waits until the
    // player is not typing or aiming, and never opens while tips are turned off).
    public bool AddCharge(PowerKind kind)
    {
        int gained = kind == PowerKind.Lure ? LureCost : FreezeCost;
        if (!GameManager.Instance.AddSkillEnergy(gained)) return false;
        RefreshHud();
        hud.PulsePower(kind);
        hud.ShowHint("+" + gained + " ENERGY",2f);
        return true;
    }

    // ---- The first-time tip box (shown by GameManager, drawn by HUD) ----

    private static string TipTitle(PowerKind kind)
    {
        return kind == PowerKind.Lure ? "YOU GOT A LURE BOMB!" : "YOU GOT A FREEZE!";
    }

    // Called by GameManager whenever the combo changes (up by one per kill, back
    // to 1 on a mistake, down when spent on a weapon).
    public void OnComboChanged(int newCombo)
    {

        RefreshWeaponStatus();
    }

    // Refresh availability at the start of a fight; energy carries between waves.
    public void OnWaveStart()
    {

        RefreshWeaponStatus();
    }

    // Availability uses independent energy and refuses to charge an active skill.
    public bool CanUseNow(bool rocket)
    {
        return GameManager.Instance.State == GameState.Playing
            && !(rocket ? RocketLoaded : IsFrenzy)
            && GameManager.Instance.SkillEnergy >= (rocket ? RocketCost : FrenzyCost);
    }

    // ---- Analytics: one "power" event per thing done with a power or weapon ----
    //   action: throw / use / empty / use_refused / aim_cancelled / frenzy_end
    //   item:   lure / freeze / frenzy / rpg
    // Skill uses include energy before, cost and energy after. Other player
    // state is added after the action; no purchase events are emitted.

    private float aimStartTime;      // game time the 1 key went down (aiming)
    private int killsAtFrenzyStart;  // kills before the current frenzy began

    private static AnalyticsEvent PowerEvent(string action, string item)
    {
        ActionPerformed?.Invoke(action, item);
        AnalyticsEvent e = Analytics.NewEvent("power");
        e.Add("action", action);
        e.Add("item", item);
        if (EasyTutorial.Active != null) e.Add("tutorial_demo", EasyTutorial.Active.IsDemonstrating);
        return e;
    }

    private static void SendPowerEvent(AnalyticsEvent e)
    {
        Analytics.AddPlayerState(e);
        Analytics.Send(e);
    }

    // ---- Combo weapons (called by TypingController) ----

    // One press spends energy and activates. Release/holding never repeats a use.
    public void WeaponKeyDown(bool rocket) { UseWeapon(rocket,"key"); }
    public void WeaponKeyUp(bool rocket) { }
    public void UseWeapon(bool rocket) { UseWeapon(rocket,"click"); }
    private void UseWeapon(bool rocket, string via)
    {
        GameManager game = GameManager.Instance;
        if (game.State != GameState.Playing) return;
        if (EasyTutorial.Active != null && !EasyTutorial.Active.AllowsPower(rocket ? "rpg" : "frenzy")) return;
        string item = rocket ? "rpg" : "frenzy";
        if (rocket ? RocketLoaded : IsFrenzy)
        {
            hud.ShowHint(rocket ? "RPG LOADED" : "FRENZY ACTIVE",1.5f);
            LogUseRefused(item,"already_active",via);
            return;
        }
        int cost = rocket ? RocketCost : FrenzyCost;
        int before = game.SkillEnergy;
        if (!game.TrySpendSkillEnergy(cost))
        {
            hud.ShowHint(cost + " ENERGY NEEDED  |  ENERGY " + before,2f);
            LogUseRefused(item,"not_enough_energy",via);
            return;
        }
        if (rocket) { RocketLoaded = true; hud.ShowBigMessage("RPG LOADED!","TYPE A WORD"); }
        else StartFrenzy();
        RefreshWeaponSlots(); hud.PulseWeapon(rocket); RefreshWeaponStatus();
        AnalyticsEvent e = PowerEvent("use",item);
        e.Add("via",via); e.Add("energy_before",before); e.Add("energy_spent",cost); e.Add("energy_after",game.SkillEnergy);
        SendPowerEvent(e);
    }
    private static void LogUseRefused(string item, string reason, string via)
    {
        AnalyticsEvent e = PowerEvent("use_refused", item);
        e.Add("reason", reason);
        e.Add("via", via);
        SendPowerEvent(e);
    }

    // FRENZY: every enemy word is short for frenzySeconds.
    private void StartFrenzy()
    {
        killsAtFrenzyStart = GameManager.Instance.Stats.Kills;
        frenzyTimeLeft = EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave && !EasyTutorial.Active.IsDemonstrating ? 15f : frenzySeconds;
        frenzyDuration = frenzyTimeLeft;
        GiveShortWords();
        hud.ShowBigMessage("FRENZY!", "every enemy word is SHORT for " + frenzySeconds.ToString("0.#") + "s");
        CameraDirector.Shake(0.25f);
    }

    private void RefreshWeaponSlots()
    {
        hud.SetWeapons(FrenzyKept, RocketKept);
    }

    // ---- FRENZY: short words ----

    private const float FrenzyLabelScale = 1.3f; // enemy words are drawn this much bigger during a frenzy
    private const float FrenzyLabelPop = 0.6f;   // ...and pop even bigger for a moment when it starts
    private static float frenzyDuration = 1f;

    // How much bigger enemy words are drawn right now (WaveSpawner.LayoutLabels):
    // 1 normally; during a frenzy FrenzyLabelScale, after a quick pop at its start.
    public static float LabelBoost
    {
        get
        {
            if (!IsFrenzy)
            {
                return 1f;
            }
            float since = frenzyDuration - frenzyTimeLeft;
            return FrenzyLabelScale * (1f + FrenzyLabelPop * Mathf.Exp(-since * 6f));
        }
    }

    // Every enemy (zombies and the boss's words) gets a short word (4 letters
    // at most); barrels, crates, orbs and quiz answers keep theirs. The word
    // being typed is dropped first (it is about to change).
    private void GiveShortWords()
    {
        WaveSpawner spawner = Object.FindFirstObjectByType<WaveSpawner>();
        DropTypedWord();
        if (spawner == null)
        {
            return;
        }

        // First letters the new words must avoid: everything that keeps its word.
        List<char> used = new List<char>();
        foreach (ITypingTarget target in spawner.GetTypingTargets())
        {
            if (!(target is Zombie) && !(target is BossPart))
            {
                used.Add(char.ToUpperInvariant(target.Word[0]));
            }
        }

        foreach (Zombie zombie in spawner.AliveZombies)
        {
            if (zombie != null && zombie.IsAlive)
            {
                string shortWord = WordBank.PickFrenzyWord(used);
                used.Add(shortWord[0]);
                zombie.EnterFrenzy(shortWord);
            }
        }
        if (spawner.CurrentBoss != null && spawner.CurrentBoss.IsAlive)
        {
            spawner.CurrentBoss.EnterFrenzy(used);
        }
    }

    // The frenzy is over: every enemy gets its real word back.
    private void RestoreRealWords()
    {
        WaveSpawner spawner = Object.FindFirstObjectByType<WaveSpawner>();
        DropTypedWord();
        if (spawner == null)
        {
            return;
        }
        foreach (Zombie zombie in spawner.AliveZombies)
        {
            if (zombie != null)
            {
                zombie.ExitFrenzy();
            }
        }
        if (spawner.CurrentBoss != null)
        {
            spawner.CurrentBoss.ExitFrenzy();
        }
    }

    // Drops the word being typed (its letters go back to white), so no half-typed
    // word is left pointing at letters that are about to change.
    private static void DropTypedWord()
    {
        TypingController typing = Object.FindFirstObjectByType<TypingController>();
        if (typing != null)
        {
            typing.DropWord();
        }
    }

    // Called by TypingController when a word is finished: true (once) if the
    // RPG is loaded, and that shot becomes the rocket.
    public bool ConsumeRocket()
    {
        if (!RocketLoaded)
        {
            return false;
        }
        RocketLoaded = false;
        RefreshWeaponStatus();
        SendPowerEvent(PowerEvent("fire", "rpg"));
        return true;
    }

    private void RefreshWeaponStatus()
    {
        int energy = GameManager.Instance.SkillEnergy;
        string text = "<size=65%>ENERGY</size> " + energy;
        if (text == lastWeaponStatus) return;
        lastWeaponStatus = text;
        hud.SetWeaponStatus(text,Mathf.Clamp01((float)energy / RocketCost),new Color(.3f,.9f,.88f));
        RefreshHud();
        RefreshWeaponSlots();
    }
    // ---- Using charges (called by TypingController) ----

    // The 1 key went down: start aiming (the dashed arc shows at once).
    public void BeginLureAim()
    {
        if (GameManager.Instance.State != GameState.Playing || aiming) return;
        if (EasyTutorial.Active != null && !EasyTutorial.Active.AllowsPower("lure")) return;
        if (GameManager.Instance.SkillEnergy < LureCost)
        {
            hud.ShowHint(LureCost + " ENERGY NEEDED",2f);
            LogUseRefused("lure","not_enough_energy","key");
            return;
        }

        aimStartTime = Time.time;
        aiming = true;
        aimTurn = 0f;                 // every aim starts straight ahead...
        aimDistance = EasyTutorial.Active != null ? 12f : throwDistance;
        if (arc == null)
        {
            arc = ThrowArc.Create();
        }
        hud.SetAimHint(true);
        UpdateAim();
    }

    // The 1 key came up: throw where the arc shows now (a quick tap, with no
    // arrow key pressed, throws straight ahead at throwDistance).
    public void ReleaseLureAim()
    {
        if (!aiming)
        {
            return;
        }

        Vector3 landing = LandingPoint(aimDistance);
        StopAiming();
        int before = GameManager.Instance.SkillEnergy;
        if (!GameManager.Instance.TrySpendSkillEnergy(LureCost))
        {
            LogUseRefused("lure","not_enough_energy_or_not_playing","key");
            return;
        }
        RefreshHud();
        hud.PulsePower(PowerKind.Lure);
        LureBomb.Throw(Hand(), landing);

        AnalyticsEvent e = PowerEvent("throw", "lure");
        e.Add("energy_before",before); e.Add("energy_spent",LureCost); e.Add("energy_after",GameManager.Instance.SkillEnergy);
        e.Add("aim_seconds", Time.time - aimStartTime); // under 0.3 = a quick tap
        e.Add("turned_degrees", aimTurn);               // steered with Left / Right
        e.Add("distance", aimDistance);                 // metres wanted (Up / Down)
        SendPowerEvent(e);
    }

    // Stops aiming without throwing (e.g. the game was paused); no charge is used.
    private void StopAiming()
    {
        aiming = false;
        if (arc != null)
        {
            arc.Hide();
        }
        hud.SetAimHint(false);
    }

    // Every frame while aiming: steer with the arrow keys and redraw the arc.
    private void UpdateAim()
    {
        if (!aiming)
        {
            return;
        }
        if (GameManager.Instance.State != GameState.Playing)
        {
            StopAiming(); // paused or game over: cancel, press 1 again to aim
            SendPowerEvent(PowerEvent("aim_cancelled", "lure"));
            return;
        }

        // Left / Right: turn the throw, within maxAimTurn of where the camera looks.
        aimTurn = Mathf.Clamp(aimTurn + arrowInput.x * aimTurnSpeed * Time.deltaTime, -maxAimTurn, maxAimTurn);

        // Up / Down: move the landing point farther / closer.
        aimDistance = Mathf.Clamp(aimDistance + arrowInput.y * aimDistanceSpeed * Time.deltaTime,
            minThrowDistance, maxThrowDistance);

        arc.Show(Hand(), LandingPoint(aimDistance));
    }

    // Called by TypingController every frame with the arrow keys held:
    // x = Right minus Left, y = Up minus Down (each -1, 0 or 1). Used only while aiming.
    public void SetAimInput(Vector2 arrows)
    {
        arrowInput = arrows;
    }

    // Where the bomb leaves the hand: just below and right of the camera.
    private static Vector3 Hand()
    {
        return CameraDirector.ShotOrigin(new Vector3(0.2f, -0.4f, 0.5f)); // the chest in the map view
    }

    public void TryUseFreeze()
    {
        if (GameManager.Instance.State != GameState.Playing) return;
        if (EasyTutorial.Active != null && !EasyTutorial.Active.AllowsPower("freeze")) return;
        if (IsFrozen)
        {
            hud.ShowHint("FREEZE ACTIVE",1.5f);
            return;
        }
        int before = GameManager.Instance.SkillEnergy;
        if (!GameManager.Instance.TrySpendSkillEnergy(FreezeCost))
        {
            hud.ShowHint(FreezeCost + " ENERGY NEEDED",2f);
            LogUseRefused("freeze","not_enough_energy","key");
            return;
        }

        bool alreadyFrozen = IsFrozen;
        RefreshHud();
        hud.PulsePower(PowerKind.Freeze);
        freezeTimeLeft = EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave && !EasyTutorial.Active.IsDemonstrating ? 15f : freezeSeconds;

        AnalyticsEvent e = PowerEvent("use", "freeze");
        e.Add("energy_before",before); e.Add("energy_spent",FreezeCost); e.Add("energy_after",GameManager.Instance.SkillEnergy);
        e.Add("while_frozen", alreadyFrozen); // used again before the last one ran out
        SendPowerEvent(e);
    }

    // ---- Shield (Tab) ----

    // Called by TypingController when Tab is pressed: brings up the SHIELD
    // WORD to type (or closes it again if it is already up).
    public void TryUseShield()
    {
        if (EasyTutorial.Active != null && !EasyTutorial.Active.AllowsPower("shield")) return;
        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }
        if (ShieldWord.Current != null)
        {
            SendPowerEvent(PowerEvent("word_cancelled", "shield"));
            CloseShieldWord("Shield cancelled. Press Tab to try again.");
            return;
        }
        if (IsShielded)
        {
            LogUseRefused("shield", "already_active", "key");
            hud.ShowHint("Your shield is already up.", 1.5f);
            return;
        }
        if (ShieldCharges <= 0)
        {
            SendPowerEvent(PowerEvent("empty", "shield"));
            hud.ShowHint("No shield left: you get " + shieldChargesPerLevel + " per level.", 2.5f);
            return;
        }

        // A word whose first character nothing on screen uses, styled for the
        // typing difficulty ("protect", "Protect,", "#Shield_07"...).
        WaveSpawner spawner = Object.FindFirstObjectByType<WaveSpawner>();
        List<char> used = spawner != null ? spawner.UsedFirstLetters() : new List<char>();
        string word = TypingSettings.Style(WordBank.PickShieldWord(used), used);
        ShieldWord.Open(word, this, hud);
        AnalyticsEvent started = PowerEvent("word_started", "shield");
        started.Add("word", word);
        started.Add("time_limit", EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave ? 30f : shieldWordSeconds);
        SendPowerEvent(started);
        shieldWordTimeLeft = EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave ? 30f : shieldWordSeconds;
        hud.SetShieldPromptTime(1f);
    }

    // Closes the shield word without raising the shield (the charge is kept).
    private void CloseShieldWord(string hint)
    {
        ShieldWord.Close();
        shieldWordTimeLeft = 0f;
        hud.ShowHint(hint, 2.5f);
    }

    // Called by ShieldWord when its word has been typed: the shield goes up.
    public void RaiseShield()
    {
        shieldWordTimeLeft = 0f;
        if (ShieldCharges <= 0 || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        ShieldCharges -= 1;
        shieldSlow = shieldEnemySpeed;
        shieldTimeLeft = shieldSeconds;
        AnalyticsEvent used = PowerEvent("use", "shield");
        used.Add("duration", shieldSeconds);
        used.Add("enemy_speed_factor", shieldEnemySpeed);
        SendPowerEvent(used);
        RefreshShieldHud();
        hud.PulseShield();
        hud.ShowHint("SHIELD UP! No damage for " + shieldSeconds.ToString("0.#")
            + " seconds and enemies are slowed. Take your time.", 3f);
    }

    // Called when the shield blocks a hit (a zombie bounced off it, an orb or
    // the boss would have hurt the player).
    public void OnShieldHit()
    {
        SendPowerEvent(PowerEvent("block", "shield"));
        hud.PulseShield();
        CameraDirector.Shake(0.15f);
    }

    // Game time: the shield (and the shield word's timer) waits while the
    // game is paused or a tip is open.
    private void UpdateShield()
    {
        if (ShieldWord.Current != null)
        {
            GameState state = GameManager.Instance.State;
            if (state == GameState.Won || state == GameState.Lost)
            {
                ShieldWord.Close();
                shieldWordTimeLeft = 0f;
            }
            else if (state == GameState.Playing)
            {
                shieldWordTimeLeft -= Time.deltaTime;
                hud.SetShieldPromptTime(EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave ? shieldWordTimeLeft / 30f
                    : shieldWordSeconds > 0f ? shieldWordTimeLeft / shieldWordSeconds : 0f);
                if (shieldWordTimeLeft <= 0f)
                {
                    SendPowerEvent(PowerEvent("word_timeout", "shield"));
                    CloseShieldWord("Too slow: no shield. Press Tab to try again.");
                }
            }
        }

        if (shieldTimeLeft <= 0f || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        shieldTimeLeft = Mathf.Max(0f, shieldTimeLeft - Time.deltaTime);
        RefreshShieldHud();
        if (shieldTimeLeft <= 0f)
        {
            SendPowerEvent(PowerEvent("end", "shield"));
            shieldSlow = 1f;
            hud.ShowHint("Shield down!", 1.5f);
        }
    }

    private void RefreshShieldHud()
    {
        float fraction = shieldSeconds > 0f ? shieldTimeLeft / shieldSeconds : 0f;
        hud.SetShield(ShieldCharges, shieldChargesPerLevel, fraction);
    }

    // Where the lure bomb lands: 'wanted' metres ahead along the ground,
    // in the direction the camera looks, but short of any wall in the way.
    private Vector3 LandingPoint(float wanted)
    {
        Transform view = Camera.main.transform;
        Vector3 ahead = view.forward;
        ahead.y = 0f;
        if (ahead.sqrMagnitude < 0.001f)
        {
            ahead = player.forward; // looking straight down or up: use the body's direction
        }
        ahead.Normalize();
        ahead = Quaternion.AngleAxis(aimTurn, Vector3.up) * ahead; // turned with Left / Right

        float distance = wanted;
        Vector3 from = player.position + Vector3.up * 1f;
        RaycastHit hit;
        if (Physics.Raycast(from, ahead, out hit, wanted))
        {
            distance = Mathf.Max(minThrowDistance, hit.distance - 0.8f);
        }

        Vector3 landing = player.position + ahead * distance;
        landing.y = 0f;
        return landing;
    }

    private void RefreshHud()
    {
        hud.SetPowers(LureCharges, FreezeCharges, maxCharges);
    }
}
