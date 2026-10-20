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
// COMBO WEAPONS: the combo (kills in a row without a mistake) buys them.
// One key per weapon, two actions:
//   HOLD the key (BuyHoldSeconds, a bar fills under its slots) to BUY one:
//     the combo is spent and the weapon is KEPT in a slot (bottom-left, a lit
//     square) until the player wants it;
//   TAP the key (or click its slot) to USE a kept one.
// So holding a FRENZY and reaching 10 combo is never a clash: tap uses the
// one you have, hold buys another.
//   [3] FRENZY - costs FrenzyCost combo: for frenzySeconds every enemy word
//       becomes a TINY word (1 to 4 letters, random) in a vivid colour, drawn bigger;
//       the real words come back when it ends.
//   [4] RPG    - costs RocketCost combo: the next word the player finishes is
//       shot with a rocket that explodes around its target (Bullet, Explosion).
//   Two of each can be kept (MaxKept); one weapon can be BOUGHT per wave
//   (oneWeaponPerWave). The line under the big COMBO on the HUD shows what the
//   combo can buy.
//
// Lives on the GameManager object. TypingController calls BeginLureAim (1 down),
// ReleaseLureAim (1 up), TryUseFreeze, WeaponKeyDown and WeaponKeyUp (3 / 4); the HUD calls
// UseWeapon when a weapon slot is clicked; GameManager calls OnComboChanged
// every time the combo changes, WaveSpawner OnWaveStart.
// ---------------------------------------------------------------------------
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

    // Combo weapon costs (also shown in the COMBO WEAPONS tip).
    public const int FrenzyCost = 10;                     // combo spent on a frenzy [3]
    public const int RocketCost = 15;                     // combo spent on one RPG shot [4]

    [Header("Combo weapons")]
    [SerializeField] private float frenzySeconds = 5f;    // enemy words are short this long
    [SerializeField] private bool oneWeaponPerWave = true;

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

    private float shieldWordTimeLeft; // > 0 while the shield word is waiting to be typed

    // Combo weapons. Static like the freeze (reset in Awake).
    private static float frenzyTimeLeft;

    // True during a FRENZY: every enemy word is short.
    public static bool IsFrenzy
    {
        get { return frenzyTimeLeft > 0f; }
    }

    // True once a kept RPG is used (loaded) and not fired yet.
    public bool RocketLoaded { get; private set; }

    // Combo weapons bought and kept for later (MaxKept of each at most), shown
    // bottom-left as lit squares; the key (or a click) uses them.
    public const int MaxKept = 2;         // slots per weapon
    public int FrenzyKept { get; private set; }
    public int RocketKept { get; private set; }

    private int combo = 1;               // the combo now (GameManager tells us)
    private bool weaponBoughtThisWave;
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
        UpdateWeaponHolds();

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
        if (kind == PowerKind.Lure)
        {
            if (LureCharges >= maxCharges)
            {
                return false;
            }
            LureCharges += 1;
        }
        else
        {
            if (FreezeCharges >= maxCharges)
            {
                return false;
            }
            FreezeCharges += 1;
        }

        RefreshHud();
        hud.PulsePower(kind);
        hud.ShowPowerGain(kind);
        GameManager.Instance.RequestPowerTip(kind, TipTitle(kind), TipText(kind));
        return true;
    }

    // ---- The first-time tip box (shown by GameManager, drawn by HUD) ----

    private static string TipTitle(PowerKind kind)
    {
        return kind == PowerKind.Lure ? "YOU GOT A LURE BOMB!" : "YOU GOT A FREEZE!";
    }

    private string TipText(PowerKind kind)
    {
        if (kind == PowerKind.Lure)
        {
            return "Zombies near it walk to it and crowd around it, then it explodes.\n\n"
                + "HOLD 1 to aim: a dashed arc shows where it will land.\n"
                + "ARROW KEYS steer it: LEFT / RIGHT = direction, UP / DOWN = distance.\n"
                + "RELEASE 1 to throw. (A quick tap throws it straight ahead.)";
        }
        return "Press 2 to freeze every zombie, energy orb and the boss for "
            + freezeSeconds.ToString("0.#") + " seconds.\n\n"
            + "Keep typing while they cannot move!";
    }

    // Called by GameManager whenever the combo changes (up by one per kill, back
    // to 1 on a mistake, down when spent on a weapon).
    public void OnComboChanged(int newCombo)
    {
        combo = newCombo;
        RefreshWeaponStatus();
    }

    // Called by WaveSpawner when a fight starts: a new weapon can be bought.
    public void OnWaveStart()
    {
        weaponBoughtThisWave = false;
        RefreshWeaponStatus();
    }

    // Could a weapon be bought right now (a full hold of its key would work)?
    // Shows nothing; used by Analytics. Keep it in step with BuyWeapon's rules.
    public bool CanBuyNow(bool rocket)
    {
        int cost = rocket ? RocketCost : FrenzyCost;
        return !(oneWeaponPerWave && weaponBoughtThisWave) && combo >= cost && Kept(rocket) < MaxKept;
    }

    // ---- Analytics: one "power" event per thing done with a power or weapon ----
    //   action: throw / use / empty (key pressed with nothing to use) / buy /
    //           buy_refused / tap_empty / hold_released (let go before a buy) /
    //           use_refused / aim_cancelled / frenzy_end
    //   item:   lure / freeze / frenzy / rpg
    // Lure and freeze GAINS are in the "crate" event (power_gained); weapon
    // gains are "buy". The player's state (charges, combo...) is added as it
    // is AFTER the action.

    private float aimStartTime;      // game time the 1 key went down (aiming)
    private int killsAtFrenzyStart;  // kills before the current frenzy began

    private static AnalyticsEvent PowerEvent(string action, string item)
    {
        AnalyticsEvent e = Analytics.NewEvent("power");
        e.Add("action", action);
        e.Add("item", item);
        return e;
    }

    private static void SendPowerEvent(AnalyticsEvent e)
    {
        Analytics.AddPlayerState(e);
        Analytics.Send(e);
    }

    // ---- Combo weapons (called by TypingController) ----

    // Key 3 (rocket = false) or key 4 (rocket = true), one key for two actions:
    //   TAP  (released within TapMaxSeconds)  -> USE a kept weapon
    //   HOLD (kept down for BuyHoldSeconds)   -> BUY one with the combo
    // While it is held, a bar under the weapon's slots fills up (HUD); let go
    // before it is full (and after a tap) and nothing happens. A click on a
    // slot always uses.
    public const float BuyHoldSeconds = 0.6f;
    private const float TapMaxSeconds = 0.3f;
    private const float HoldBarDelay = 0.1f;    // the bar only shows after this (a tap never flashes it)

    private readonly float[] weaponHold = { -1f, -1f }; // seconds held: [0] = 3 FRENZY, [1] = 4 RPG; -1 = not held

    public void WeaponKeyDown(bool rocket)
    {
        weaponHold[rocket ? 1 : 0] = 0f;
    }

    public void WeaponKeyUp(bool rocket)
    {
        int index = rocket ? 1 : 0;
        float held = weaponHold[index];
        weaponHold[index] = -1f;
        hud.SetWeaponHold(rocket, 0f);
        if (held >= 0f && held <= TapMaxSeconds)
        {
            TapWeapon(rocket);
        }
        else if (held > TapMaxSeconds)
        {
            // Held too long for a tap, let go before the buy: nothing happens
            // in the game, but it was probably an attempt to buy.
            AnalyticsEvent e = PowerEvent("hold_released", rocket ? "rpg" : "frenzy");
            e.Add("held_seconds", held);
            e.Add("could_buy", CanBuyNow(rocket));
            SendPowerEvent(e);
        }
    }

    // Every frame: count the keys held down; a full hold buys.
    private void UpdateWeaponHolds()
    {
        for (int index = 0; index < 2; index++)
        {
            if (weaponHold[index] < 0f)
            {
                continue;
            }
            bool rocket = index == 1;
            if (GameManager.Instance.State != GameState.Playing)
            {
                weaponHold[index] = -1f; // paused or game over: the hold is cancelled
                hud.SetWeaponHold(rocket, 0f);
                continue;
            }

            weaponHold[index] += Time.deltaTime;
            float shown = weaponHold[index] < HoldBarDelay ? 0f : weaponHold[index] / BuyHoldSeconds;
            hud.SetWeaponHold(rocket, Mathf.Clamp01(shown));
            if (weaponHold[index] >= BuyHoldSeconds)
            {
                weaponHold[index] = -1f; // done: letting go of the key now does nothing
                hud.SetWeaponHold(rocket, 0f);
                BuyWeapon(rocket);
            }
        }
    }

    // A tap: use a kept weapon, or say how to get one.
    private void TapWeapon(bool rocket)
    {
        if (Kept(rocket) > 0)
        {
            UseWeapon(rocket, "key");
            return;
        }

        AnalyticsEvent e = PowerEvent("tap_empty", rocket ? "rpg" : "frenzy");
        e.Add("could_buy", CanBuyNow(rocket));
        SendPowerEvent(e);

        int cost = rocket ? RocketCost : FrenzyCost;
        string weaponName = rocket ? "RPG" : "FRENZY";
        if (CanBuyWeapon(cost, weaponName)) // (if not, it shows why)
        {
            hud.ShowHint("HOLD " + (rocket ? "4" : "3") + " to get " + (rocket ? "an RPG" : "a FRENZY")
                + " (tap = use one you have).", 2.5f);
        }
    }

    private int Kept(bool rocket)
    {
        return rocket ? RocketKept : FrenzyKept;
    }

    // Spends the combo on a weapon and keeps it in a free slot (bottom-left).
    private void BuyWeapon(bool rocket)
    {
        int cost = rocket ? RocketCost : FrenzyCost;
        string weaponName = rocket ? "RPG" : "FRENZY";
        string item = rocket ? "rpg" : "frenzy";
        if (Kept(rocket) >= MaxKept)
        {
            hud.ShowHint("Your " + weaponName + " slots are full: use one first.", 2f);
            AnalyticsEvent full = PowerEvent("buy_refused", item);
            full.Add("reason", "slots_full");
            SendPowerEvent(full);
            return;
        }
        if (!CanBuyWeapon(cost, weaponName))
        {
            AnalyticsEvent refused = PowerEvent("buy_refused", item);
            refused.Add("reason", oneWeaponPerWave && weaponBoughtThisWave ? "one_per_wave" : "not_enough_combo");
            SendPowerEvent(refused);
            return;
        }
        int comboBefore = combo;
        GameManager.Instance.SpendCombo(cost);
        weaponBoughtThisWave = true;
        if (rocket)
        {
            RocketKept += 1;
        }
        else
        {
            FrenzyKept += 1;
        }
        RefreshWeaponSlots();
        hud.PulseWeapon(rocket);
        hud.ShowWeaponGain(rocket);
        hud.ShowBigMessage("GOT " + weaponName + "!",
            "kept bottom-left - TAP " + (rocket ? "4" : "3") + " (or click it) when you want to use it");
        RefreshWeaponStatus();

        AnalyticsEvent e = PowerEvent("buy", item);
        e.Add("combo_before", comboBefore);
        SendPowerEvent(e);
    }

    // Uses a kept weapon: a click on its slot (HUD).
    public void UseWeapon(bool rocket)
    {
        UseWeapon(rocket, "click");
    }

    // Uses a kept weapon. via (for analytics): "key" (tap 3 / 4) or "click".
    private void UseWeapon(bool rocket, string via)
    {
        string item = rocket ? "rpg" : "frenzy";
        if (Kept(rocket) <= 0)
        {
            hud.ShowHint("No " + (rocket ? "RPG" : "FRENZY") + " kept. HOLD " + (rocket ? "4" : "3")
                + " to buy one with your combo.", 2.5f);
            LogUseRefused(item, "none_kept", via);
            return;
        }
        if (rocket)
        {
            if (RocketLoaded)
            {
                hud.ShowHint("The RPG is already loaded: finish a word to fire it.", 2f);
                LogUseRefused(item, "already_loaded", via);
                return;
            }
            RocketKept -= 1;
            RocketLoaded = true;
            hud.ShowBigMessage("RPG LOADED!", "finish a word to fire the rocket");
        }
        else
        {
            if (IsFrenzy)
            {
                hud.ShowHint("A frenzy is already on.", 2f);
                LogUseRefused(item, "frenzy_on", via);
                return;
            }
            FrenzyKept -= 1;
            StartFrenzy();
        }
        RefreshWeaponSlots();
        hud.PulseWeapon(rocket);
        RefreshWeaponStatus();

        AnalyticsEvent e = PowerEvent("use", item);
        e.Add("via", via);
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
        frenzyTimeLeft = frenzySeconds;
        frenzyDuration = frenzySeconds;
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
        return true;
    }

    // Says why a weapon cannot be bought right now (hint), or returns true.
    private bool CanBuyWeapon(int cost, string weaponName)
    {
        if (oneWeaponPerWave && weaponBoughtThisWave)
        {
            hud.ShowHint("Only one weapon per wave.", 2f);
            return false;
        }
        if (combo < cost)
        {
            hud.ShowHint(weaponName + " needs " + cost + " combo (you have " + combo + ").", 2.5f);
            return false;
        }
        return true;
    }

    // The line under the big COMBO: the weapon in use, or what the combo can buy.
    private void RefreshWeaponStatus()
    {
        string text;
        float progress;
        Color color;
        Color grey = new Color(0.6f, 0.62f, 0.66f);
        bool canBuyFrenzy = FrenzyKept < MaxKept;   // MaxKept of each can be kept
        bool canBuyRocket = RocketKept < MaxKept;
        if (IsFrenzy)
        {
            text = "FRENZY!  " + frenzyTimeLeft.ToString("0.0") + "s";
            progress = frenzyTimeLeft / Mathf.Max(0.01f, frenzySeconds);
            color = Palette.WeaponFrenzy;
        }
        else if (RocketLoaded)
        {
            text = "RPG LOADED - finish a word";
            progress = 1f;
            color = Palette.BlastOrange;
        }
        else if (oneWeaponPerWave && weaponBoughtThisWave)
        {
            text = "Next weapon: next wave";
            progress = 0f;
            color = grey;
        }
        else if (!canBuyFrenzy && !canBuyRocket)
        {
            text = "Weapon slots full - use them!";
            progress = 1f;
            color = grey;
        }
        else if (canBuyRocket && combo >= RocketCost)
        {
            text = canBuyFrenzy ? "HOLD [3] FRENZY or [4] RPG to get one!" : "HOLD [4] to get an RPG!";
            progress = 1f;
            color = Palette.BlastOrange;
        }
        else if (canBuyFrenzy && combo >= FrenzyCost)
        {
            text = "HOLD [3] to get a FRENZY!" + (canBuyRocket ? "   RPG in " + (RocketCost - combo) + " combo" : "");
            progress = canBuyRocket ? (float)combo / RocketCost : 1f;
            color = Palette.WeaponFrenzy;
        }
        else if (canBuyFrenzy)
        {
            text = "FRENZY in " + (FrenzyCost - combo) + " combo";
            progress = (float)combo / FrenzyCost;
            color = new Color(0.8f, 0.82f, 0.86f);
        }
        else
        {
            text = "RPG in " + (RocketCost - combo) + " combo";
            progress = (float)combo / RocketCost;
            color = new Color(0.8f, 0.82f, 0.86f);
        }

        if (text != lastWeaponStatus || IsFrenzy)
        {
            lastWeaponStatus = text;
            hud.SetWeaponStatus(text, progress, color);
        }
    }

    // ---- Using charges (called by TypingController) ----

    // The 1 key went down: start aiming (the dashed arc shows at once).
    public void BeginLureAim()
    {
        if (LureCharges <= 0)
        {
            hud.ShowHint("No lure bombs. Find them in supply crates (orange).", 3f);
            SendPowerEvent(PowerEvent("empty", "lure"));
            return;
        }

        aimStartTime = Time.time;
        aiming = true;
        aimTurn = 0f;                 // every aim starts straight ahead...
        aimDistance = throwDistance;  // ...at the usual distance
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

        LureCharges -= 1;
        RefreshHud();
        hud.PulsePower(PowerKind.Lure);
        LureBomb.Throw(Hand(), landing);

        AnalyticsEvent e = PowerEvent("throw", "lure");
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
        if (FreezeCharges <= 0)
        {
            hud.ShowHint("No freeze left. Find them in supply crates (blue).", 3f);
            SendPowerEvent(PowerEvent("empty", "freeze"));
            return;
        }

        bool alreadyFrozen = IsFrozen;
        FreezeCharges -= 1;
        RefreshHud();
        hud.PulsePower(PowerKind.Freeze);
        freezeTimeLeft = freezeSeconds;

        AnalyticsEvent e = PowerEvent("use", "freeze");
        e.Add("while_frozen", alreadyFrozen); // used again before the last one ran out
        SendPowerEvent(e);
    }

    // ---- Shield (Tab) ----

    // Called by TypingController when Tab is pressed: brings up the SHIELD
    // WORD to type (or closes it again if it is already up).
    public void TryUseShield()
    {
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
        started.Add("time_limit", shieldWordSeconds);
        SendPowerEvent(started);
        shieldWordTimeLeft = shieldWordSeconds;
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
                hud.SetShieldPromptTime(shieldWordSeconds > 0f ? shieldWordTimeLeft / shieldWordSeconds : 0f);
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
