// WaveSpawner.cs
// ---------------------------------------------------------------------------
// Runs the level, Typing of the Dead style. For every Encounter of the Level
// (the 5 fights listed at the top of Level.cs), in order:
//   1. RIDE: the player rides the encounter's route (on a long run a few
//      AMBUSH zombies jump into the road ahead, see RideAmbush), then stops and turns to
//      face the fight.
//   2. FIGHT: a "Wave N" banner, then the area's barrels and supply crates get
//      words (they can be typed now) and a wave of zombies comes out of the
//      area's spawn points (doors burst open), a few at a time. Or, in the
//      last area, the BOSS.
//   3. CLEARED: when every zombie is dead, the exit gate opens and the next
//      ride begins.
// After the last encounter the player wins.
//
// Wave n (encounter n) has zombiesBase + zombiesPerWave x n zombies (4 + 2n by
// default). Later waves are faster, spawn more often, and mix in special
// zombies: EXPLOSIVE ones from explosiveFirstWave, RUNNERS from
// runnerFirstWave, ARMORED ones from armoredFirstWave (see Zombie). From
// chainFirstWave on, WORD CHAIN pairs come on top of that (one more pair each
// wave): two zombies at once out of two different doors, "hunt" and "hunter", linked in
// purple (see AddChainPairs and SpawnChainPair). The first pair explains itself.
//
// It also keeps the lists of everything that can be typed (GetTypingTargets),
// which TypingController (targeting), Explosion (blasts) and the HUD use, and
// every frame it places the enemies' words on screen (LayoutLabels).
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [Header("Waves")]
    [SerializeField] private int zombiesBase = 4;               // wave n spawns zombiesBase + zombiesPerWave x n
    [SerializeField] private int zombiesPerWave = 2;
    [SerializeField] private float spawnInterval = 2.0f;        // seconds between spawns in wave 1
    [SerializeField] private float spawnIntervalPerWave = 0.2f; // seconds SUBTRACTED for each later wave

    [Header("Zombies")]
    [SerializeField] private float zombieSpeed = 1.5f;          // metres per second in wave 1
    [SerializeField] private float zombieSpeedPerWave = 0.12f;  // speed ADDED for each later wave

    [Header("Explosive zombies")]
    [SerializeField] private int explosiveFirstWave = 2;        // no explosive zombies before this wave
    [SerializeField] private int explosiveStartCount = 2;       // explosive zombies in explosiveFirstWave
    [SerializeField] private int explosivePerWave = 1;          // one more explosive zombie in every later wave

    [Header("Runners")]
    [SerializeField] private int runnerFirstWave = 3;
    [SerializeField] private int runnerStartCount = 2;
    [SerializeField] private int runnersPerWave = 1;

    [Header("Armored zombies")]
    [SerializeField] private int armoredFirstWave = 4;
    [SerializeField] private int armoredStartCount = 2;
    [SerializeField] private int armoredPerWave = 1;

    [Header("Map view")]
    [SerializeField] private int[] mapViewWaves = { 2, 4 };     // in these waves the camera rises to a view over the whole area (CameraDirector)

    [Header("Word chains")]
    [SerializeField] private int chainFirstWave = 2;            // no WORD CHAIN pairs (hunt + hunter) before this wave
    [SerializeField] private int chainStartCount = 1;           // pairs in chainFirstWave
    [SerializeField] private int chainPerWave = 1;              // one more pair in every later wave

    [Header("Boss")]
    [SerializeField] private string bossName = "SUBJECT ZERO";

    [Header("References (wired in the scene)")]
    [SerializeField] private Zombie zombiePrefab;
    [SerializeField] private Transform player;
    [SerializeField] private HUD hud;
    [SerializeField] private Level level;

    private const float MinSpawnInterval = 0.8f; // the spawn interval never goes below this
    private const float PackStagger = 0.6f;      // seconds between the zombies of one pack
    private const float PackLaneWidth = 1.4f;    // metres between the lanes of a pack's zombies (side by side)
    private const float BannerSeconds = 2f;      // how long the "Wave N" banner stays up
    private const float GateSeconds = 1.5f;      // time for a gate to open before the ride goes on

    private readonly List<Zombie> aliveZombies = new List<Zombie>();
    private readonly List<ITypingTarget> typingTargets = new List<ITypingTarget>();
    private readonly List<SupplyCrate> activeCrates = new List<SupplyCrate>();
    private readonly List<Vector3> threatPositions = new List<Vector3>();

    private RailMover rail;          // on the Player object
    private Boss boss;               // the current boss, or null

    // Every zombie that is currently alive.
    public List<Zombie> AliveZombies
    {
        get { return aliveZombies; }
    }

    // The barrels of the area being fought (Explosion sets them off).
    public List<Barrel> ActiveBarrels { get; } = new List<Barrel>();

    // The boss being fought, or null.
    public Boss CurrentBoss
    {
        get { return boss; }
    }

    // The encounter being ridden to or fought (null before the level starts),
    // and its wave number (1 = the first).
    public Encounter CurrentEncounter { get; private set; }
    public int CurrentWave { get; private set; }

    // True in the waves listed in mapViewWaves: once the player stops there,
    // the camera rises to show the whole area (see CameraDirector).
    public bool IsMapViewWave
    {
        get { return CurrentEncounter != null && System.Array.IndexOf(mapViewWaves, CurrentWave) >= 0; }
    }

    // Used by zombies, barrels... to create their word.
    public HUD Hud
    {
        get { return hud; }
    }

    // Where the player is and which way they face (for analytics positions).
    public Vector3 PlayerPosition
    {
        get { return player.position; }
    }

    public float PlayerYaw
    {
        get { return player.eulerAngles.y; }
    }

    // What the player is doing now, for analytics: "ride" (on the rails),
    // "fight" (a zombie wave, its banner and gate included), "boss", or
    // "none" (before the first ride).
    public string PhaseName
    {
        get
        {
            if (CurrentEncounter == null)
            {
                return "none";
            }
            if (rail != null && rail.IsRiding)
            {
                return "ride";
            }
            return CurrentEncounter.IsBossFight ? "boss" : "fight";
        }
    }

    // Everything the player can type right now: alive zombies, the active
    // barrels and supply crates, and the boss's parts, orbs and quiz answers.
    // The list is reused, so read it right away.
    public List<ITypingTarget> GetTypingTargets()
    {
        typingTargets.Clear();
        typingTargets.AddRange(aliveZombies);
        foreach (Barrel barrel in ActiveBarrels)
        {
            if (barrel != null && barrel.IsAlive)
            {
                typingTargets.Add(barrel);
            }
        }
        foreach (SupplyCrate crate in activeCrates)
        {
            if (crate != null && crate.IsAlive)
            {
                typingTargets.Add(crate);
            }
        }
        if (boss != null && boss.IsAlive)
        {
            boss.AddTypeableParts(typingTargets);
        }
        return typingTargets;
    }

    // UPPERCASE first letters of every word that can be typed right now. New
    // words avoid them, so the first key usually points at exactly one target
    // (a WORD CHAIN pair shares its first letter on purpose, see SpawnChainPair).
    public List<char> UsedFirstLetters()
    {
        List<char> used = new List<char>();
        foreach (ITypingTarget target in GetTypingTargets())
        {
            used.Add(char.ToUpperInvariant(target.Word[0]));
        }
        return used;
    }

    // Awake (not Start): everything must be ready before GameManager can begin the waves,
    // which may happen before this object's Start. Level lists its fights in its
    // own Awake, which runs before this one (see Level's DefaultExecutionOrder).
    private void Awake()
    {
        rail = player.GetComponent<RailMover>();

        if (level == null || level.Encounters.Count == 0)
        {
            Debug.LogError("WaveSpawner: no Level with encounters is wired in the scene.");
            return;
        }

        hud.SetWave(1, level.Encounters.Count);
        rail.PlaceAt(level.StartPosition, level.StartFacing);
    }

    private void LateUpdate()
    {
        LayoutLabels();
        UpdateThreatArrows();
    }

    // Arrows at the screen edges toward zombies that are off screen.
    private void UpdateThreatArrows()
    {
        bool playing = GameManager.Instance.State == GameState.Playing;
        threatPositions.Clear();
        if (playing)
        {
            foreach (Zombie zombie in aliveZombies)
            {
                threatPositions.Add(zombie.HitPoint);
            }
        }
        hud.UpdateThreatArrows(threatPositions, Camera.main);
    }

    // ---- Enemy words on screen ----
    // Every enemy word is a HUD text (HUD.CreateWordLabel), drawn on top of the
    // 3D scene, so no body can hide it. Every frame each word is pinned to its
    // enemy's LabelAnchor:
    //   - it is hidden while its enemy is behind the camera;
    //   - if its enemy is off to a side (or above), the word is kept
    //     labelEdgeMargin inside that screen edge, so it can still be read and
    //     typed (KeepOnScreen). For a zombie that is off screen, this puts the
    //     word next to its red threat arrow (only zombies get arrows);
    //   - it shrinks with distance, like the enemy does (labelReferenceDistance),
    //     but is never drawn smaller than minWordFontSize, so it stays readable
    //     even in the map view, where the camera is far above everything;
    //   - it never goes under the HUD along the top of the screen (health,
    //     combo, score): it stays labelTopMargin below the top edge, and
    //     during the boss fight bossLabelTopMargin (below the boss's health bar);
    //   - words are placed one by one in priority order (the word being typed
    //     first, then the nearest enemy first). A word that would overlap one
    //     already placed is pushed up until it is clear, and slides there
    //     smoothly, but never past the top edge: when there is no room left
    //     above, it moves the shortest way to a free spot instead (below or
    //     beside the words in the way).

    [Header("Enemy words")]
    [SerializeField] private float labelReferenceDistance = 12f; // at this distance a word is drawn at full size
    [SerializeField] private float labelMinScale = 0.55f;        // far words never get smaller than this
    [SerializeField] private float labelMaxScale = 1.2f;         // near words never get bigger than this
    [SerializeField] private float labelGap = 4f;                // empty space (HUD units) between stacked words
    [SerializeField] private float labelSlideSharpness = 14f;    // higher = pushed words slide into place faster
    [SerializeField] private float labelEdgeMargin = 110f;       // HUD units: words always stay this far inside the side and bottom edges
    [SerializeField] private float labelTopMargin = 150f;        // HUD units: ...and this far below the top edge (health, combo and score are up there)
    [SerializeField] private float bossLabelTopMargin = 235f;    // HUD units: during the boss fight, below its health bar too
    [SerializeField] private float minWordFontSize = 34f;        // HUD units: no word is ever drawn smaller (34 is about 19 pixels in a 960x600 browser window)
    private const float MapLabelScale = 0.7f;                    // in the map view (waves 2 and 4) words are drawn this much smaller (but never below minWordFontSize)
    private const float LabelSideGap = 30f;                      // HUD units between two words placed side by side (with less, they read as one word)
    private const float LabelSwitchBand = 15f;                   // HUD units: dead band for StackUp (more than the camera's breathing plus one letter's kick)

    private readonly List<ITypingTarget> labelOrder = new List<ITypingTarget>();
    private readonly List<Rect> placedLabels = new List<Rect>();

    // How far each word is currently pushed up. A word not in here yet (just
    // spawned) jumps straight to its spot instead of sliding in from elsewhere.
    private Dictionary<TMP_Text, Vector2> labelLifts = new Dictionary<TMP_Text, Vector2>();
    private Dictionary<TMP_Text, Vector2> nextLabelLifts = new Dictionary<TMP_Text, Vector2>();


    // How far below the top edge words must stay right now (lower during the
    // boss fight, when the boss's health bar is up there too).
    private float TopMargin
    {
        get { return boss != null ? Mathf.Max(labelTopMargin, bossLabelTopMargin) : labelTopMargin; }
    }

    private void LayoutLabels()
    {
        RectTransform layer = hud.WordLayer;
        Camera cam = Camera.main;
        Vector3 cameraPosition = cam.transform.position;

        labelOrder.Clear();
        labelOrder.AddRange(GetTypingTargets());
        labelOrder.Sort((a, b) =>
        {
            if (a.IsTargeted != b.IsTargeted)
            {
                return a.IsTargeted ? -1 : 1;
            }
            float distanceA = (a.LabelAnchor - cameraPosition).sqrMagnitude;
            float distanceB = (b.LabelAnchor - cameraPosition).sqrMagnitude;
            return distanceA.CompareTo(distanceB);
        });

        placedLabels.Clear();
        nextLabelLifts.Clear();
        float slide = 1f - Mathf.Exp(-labelSlideSharpness * Time.unscaledDeltaTime);

        foreach (ITypingTarget target in labelOrder)
        {
            TMP_Text label = target.Label;
            RectTransform labelRect = label.rectTransform;

            // Behind the camera: hide the word.
            Vector3 screenPoint = cam.WorldToScreenPoint(target.LabelAnchor);
            if (screenPoint.z <= 0f)
            {
                label.enabled = false;
                continue;
            }

            // Screen pixels -> position inside the word layer (Canvas units).
            Vector2 anchor;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPoint, null, out anchor);

            // Size: fit the box to the text, then scale with distance.
            float scale = Mathf.Clamp(labelReferenceDistance / screenPoint.z, labelMinScale, labelMaxScale);
            if (target is Zombie || target is BossPart)
            {
                scale *= Powers.LabelBoost; // enemy words are bigger during a FRENZY
            }
            float mapBlend = CameraDirector.MapBlend;
            scale *= Mathf.Lerp(1f, MapLabelScale, mapBlend); // seen from high up, words are smaller
            scale = Mathf.Max(scale, minWordFontSize / label.fontSize); // ...but never too small to read
            Vector2 textSize = new Vector2(label.preferredWidth, label.preferredHeight);
            labelRect.sizeDelta = textSize;
            labelRect.localScale = new Vector3(scale, scale, 1f);

            // The word's box on screen with no push, then moved off the words
            // already placed.
            Vector2 size = textSize * scale;
            anchor = KeepOnScreen(anchor, size, labelRect.pivot, layer.rect);
            Rect baseRect = new Rect(anchor - Vector2.Scale(size, labelRect.pivot), size);
            // First person: push the word UP past every word already placed (no
            // room left at the top: to the nearest free spot instead).
            // Map view (seen from above, words crowd together): move it the
            // shortest way, in any direction, to a free spot.
            Rect rect = mapBlend > 0.5f ? SpreadOut(baseRect, layer.rect) : StackUp(baseRect, layer.rect, WasMovedAside(label));
            placedLabels.Add(rect);

            // Slide toward the new spot (new words jump straight there).
            Vector2 targetLift = rect.position - baseRect.position;
            Vector2 lift;
            if (labelLifts.TryGetValue(label, out lift))
            {
                lift = Vector2.Lerp(lift, targetLift, slide);
            }
            else
            {
                lift = targetLift;
            }
            nextLabelLifts[label] = lift;

            labelRect.anchoredPosition = anchor + lift;
            label.enabled = true;

            // The word being typed is drawn on top of all the others.
            if (target.IsTargeted && labelRect.GetSiblingIndex() != layer.childCount - 1)
            {
                labelRect.SetAsLastSibling();
            }
        }

        // Keep only the words that still exist.
        Dictionary<TMP_Text, Vector2> swap = labelLifts;
        labelLifts = nextLabelLifts;
        nextLabelLifts = swap;
    }

    // Moves a word that would stick out of the screen back inside it, keeping
    // labelEdgeMargin free at the edges. The word slides straight toward the
    // middle of the screen, so it ends up on the same side as its enemy (for
    // a zombie that is off screen: next to its red threat arrow). A word that
    // already fits does not move.
    //   anchor = where the word's pivot would be (HUD units, 0,0 = screen middle)
    //   size   = the word's size on screen, pivot = the word's pivot, screen = the word layer
    // First person: pushes the word up past every word already placed (it only
    // ever moves up, so this always finishes), but never off the top of the
    // screen. If the pile is too high for that (words piled up high on the
    // screen, like the boss's head word and its energy orbs), it moves the
    // shortest way to a free spot instead (FindFreeSpot). Only when no spot is
    // free does it stop at the top edge, drawn over another word.
    // The top limit stays still while the camera kicks (every letter) and
    // breathes, so a dead band (LabelSwitchBand) keeps the choice steady: a
    // stacked word moves aside only when the pile is clearly too high, and a
    // word moved aside (wasMovedAside) comes back only when there is clearly room.
    private Rect StackUp(Rect rect, Rect screen, bool wasMovedAside)
    {
        Rect pushed = rect;
        bool moved = true;
        while (moved)
        {
            moved = false;
            foreach (Rect placed in placedLabels)
            {
                if (pushed.Overlaps(placed))
                {
                    pushed.y = placed.yMax + labelGap;
                    moved = true;
                }
            }
        }

        float highest = screen.yMax - TopMargin - pushed.height; // the word's bottom edge when it touches the top limit
        float switchAt = wasMovedAside ? highest - LabelSwitchBand : highest + LabelSwitchBand;
        if (pushed.y > rect.y && pushed.y > switchAt) // it had to be pushed, and the pile is too high
        {
            Rect free;
            if (FindFreeSpot(rect, screen, out free))
            {
                return free;
            }
        }
        pushed.y = Mathf.Min(pushed.y, highest);
        return pushed;
    }

    // True if last frame the word sat below or beside its own spot, so StackUp
    // had moved it to a free spot (a stacked word only ever moves up).
    private bool WasMovedAside(TMP_Text label)
    {
        Vector2 lastLift;
        return labelLifts.TryGetValue(label, out lastLift) && (lastLift.x != 0f || lastLift.y < -1f);
    }

    // Map view: the word already fits -> it stays. Otherwise it moves the
    // shortest way to a free spot (FindFreeSpot). If no single move frees it,
    // fall back to stacking it up.
    private Rect SpreadOut(Rect rect, Rect screen)
    {
        if (!OverlapsPlaced(rect))
        {
            return rect;
        }

        Rect free;
        if (FindFreeSpot(rect, screen, out free))
        {
            return free;
        }
        return StackUp(rect, screen, false);
    }

    // Tries every spot just above, below, left or right of each word already
    // placed, and gives back the free one (no overlap, on screen) closest to
    // where the word wants to be. Sideways moves count a little more, so words
    // prefer to move up / down. Returns false if no single move frees it.
    private bool FindFreeSpot(Rect rect, Rect screen, out Rect best)
    {
        Rect inside = new Rect(screen.xMin + 10f, screen.yMin + 10f, screen.width - 20f, screen.height - 10f - TopMargin);
        best = rect;
        float bestCost = float.MaxValue;
        foreach (Rect placed in placedLabels)
        {
            TrySpot(new Rect(rect.x, placed.yMax + labelGap, rect.width, rect.height), rect, inside, ref best, ref bestCost);
            TrySpot(new Rect(rect.x, placed.yMin - rect.height - labelGap, rect.width, rect.height), rect, inside, ref best, ref bestCost);
            TrySpot(new Rect(placed.xMin - rect.width - LabelSideGap, rect.y, rect.width, rect.height), rect, inside, ref best, ref bestCost);
            TrySpot(new Rect(placed.xMax + LabelSideGap, rect.y, rect.width, rect.height), rect, inside, ref best, ref bestCost);
        }
        return bestCost < float.MaxValue;
    }

    private void TrySpot(Rect spot, Rect wanted, Rect inside, ref Rect best, ref float bestCost)
    {
        if (spot.xMin < inside.xMin || spot.xMax > inside.xMax || spot.yMin < inside.yMin || spot.yMax > inside.yMax)
        {
            return; // off screen
        }
        if (OverlapsPlaced(spot))
        {
            return;
        }
        Vector2 move = spot.position - wanted.position;
        float cost = Mathf.Abs(move.x) * 1.2f + Mathf.Abs(move.y);
        if (cost < bestCost)
        {
            bestCost = cost;
            best = spot;
        }
    }

    private bool OverlapsPlaced(Rect rect)
    {
        foreach (Rect placed in placedLabels)
        {
            if (rect.Overlaps(placed))
            {
                return true;
            }
        }
        return false;
    }

    private Vector2 KeepOnScreen(Vector2 anchor, Vector2 size, Vector2 pivot, Rect screen)
    {
        // The middle of the word's box.
        Vector2 middle = anchor + Vector2.Scale(new Vector2(0.5f, 0.5f) - pivot, size);

        // How far from the screen middle the box's middle may be.
        float roomX = Mathf.Max(0f, screen.width * 0.5f - labelEdgeMargin - size.x * 0.5f);
        float roomUp = Mathf.Max(0f, screen.height * 0.5f - TopMargin - size.y * 0.5f);          // above the middle
        float roomDown = Mathf.Max(0f, screen.height * 0.5f - labelEdgeMargin - size.y * 0.5f); // below the middle

        // fit = 1 means it already fits; smaller = pull it that much closer to the middle.
        float fit = 1f;
        if (Mathf.Abs(middle.x) > roomX)
        {
            fit = Mathf.Min(fit, roomX / Mathf.Abs(middle.x));
        }
        if (middle.y > roomUp)
        {
            fit = Mathf.Min(fit, roomUp / middle.y);
        }
        if (middle.y < -roomDown)
        {
            fit = Mathf.Min(fit, roomDown / -middle.y);
        }

        // Pull the box's middle that much closer to the screen middle...
        Vector2 newMiddle = middle * fit;
        // ...and move the word's pivot by the same amount, so the whole box moves together.
        return anchor + (newMiddle - middle);
    }

    // Called by GameManager when the game starts.
    public void BeginWaves()
    {
        if (level == null || level.Encounters.Count == 0)
        {
            return;
        }
        StartCoroutine(RunLevel());
    }

    // Called by GameManager when the player dies.
    public void StopWaves()
    {
        StopAllCoroutines();
    }

    // Called by a zombie when it dies or reaches the player.
    public void RemoveZombie(Zombie zombie)
    {
        aliveZombies.Remove(zombie);
    }

    // A coroutine: it runs over many frames. Every "yield return" pauses it here
    // and Unity continues it later (after the wait is over, or on the next frame).
    private IEnumerator RunLevel()
    {
        int count = level.Encounters.Count;

        for (int index = 0; index < count; index++)
        {
            Encounter encounter = level.Encounters[index];
            int wave = index + 1;
            CurrentEncounter = encounter;
            CurrentWave = wave;
            hud.SetWave(wave, count);

            // 1. Ride to the fight. On a long run, a few zombies jump into the
            //    road (not before the first fight: the player has not been
            //    shown how to type yet).
            rail.RideAlong(encounter.Route, encounter.Facing);
            RunStats stats = GameManager.Instance.Stats;
            float rideStart = stats.Seconds;
            int healthAtRideStart = GameManager.Instance.Health;
            ambushersPlaced = 0;
            ambushersWoken = 0;
            AnalyticsEvent rideStartEvent = Analytics.NewEvent("ride_start");
            rideStartEvent.Add("boss_next", encounter.IsBossFight);
            Analytics.Send(rideStartEvent);

            Coroutine ambush = index > 0 ? StartCoroutine(RideAmbush(wave)) : null;
            while (!rail.IsStopped)
            {
                yield return null;
            }
            if (ambush != null)
            {
                StopCoroutine(ambush);
            }
            ClearAmbush();

            AnalyticsEvent rideEnd = Analytics.NewEvent("ride_end");
            rideEnd.Add("seconds", stats.Seconds - rideStart);
            rideEnd.Add("ambushers_placed", ambushersPlaced);
            rideEnd.Add("ambushers_woken", ambushersWoken);  // the ones the player got to see
            rideEnd.Add("health_at_start", healthAtRideStart);
            Analytics.AddPlayerState(rideEnd);
            Analytics.Send(rideEnd);

            // 2. Fight.
            hud.ShowBanner(encounter.IsBossFight ? "BOSS FIGHT" : "Wave " + wave);
            yield return new WaitForSeconds(BannerSeconds);
            hud.HideBanner();
            GameManager.Instance.Powers.OnWaveStart(); // a new combo weapon can be bought
            ActivateProps(encounter);
            StartCoroutine(ShowPropHints());

            float fightStart = stats.Seconds;
            int killsAtStart = stats.Kills;
            int correctKeysAtStart = stats.CorrectKeys;
            int wrongKeysAtStart = stats.WrongKeys;
            int healthAtStart = GameManager.Instance.Health;

            if (encounter.IsBossFight)
            {
                yield return RunBossFight(encounter);
            }
            else
            {
                yield return RunZombieWave(wave, encounter);
            }

            // Analytics: the fight is won (a death ends the run instead, see "run_end").
            AnalyticsEvent waveEnd = Analytics.NewEvent("wave_end");
            waveEnd.Add("boss_fight", encounter.IsBossFight);
            waveEnd.Add("seconds", stats.Seconds - fightStart);
            waveEnd.Add("kills", stats.Kills - killsAtStart);
            waveEnd.Add("correct_keys", stats.CorrectKeys - correctKeysAtStart);
            waveEnd.Add("wrong_keys", stats.WrongKeys - wrongKeysAtStart);
            waveEnd.Add("health_at_start", healthAtStart);
            Analytics.AddPlayerState(waveEnd);
            Analytics.Send(waveEnd);
            Analytics.FlushSoon();

            // 3. Cleared: the next gate opens.
            DeactivateProps();
            if (encounter.ExitGate != null)
            {
                encounter.ExitGate.Open();
                yield return new WaitForSeconds(GateSeconds);
            }
        }

        GameManager.Instance.WinGame();
    }

    // ---- Ambush on the run ----
    // A long run between two fights is not just a ride: a few zombies are
    // waiting along the road. They NEVER pop up in view:
    //   - they are all placed when the run starts, each on the route (so never
    //     inside a wall) at a spot the camera cannot see at that moment (off
    //     screen, or hidden behind a building; if the planned spot can be seen,
    //     the next hidden spot further along the road is used);
    //   - a placed zombie LURKS (Zombie.IsLurking): it stands still, has no word
    //     and cannot be typed. The moment the player sees it, it wakes up: it
    //     gets a short word and walks at the player, so a zombie waiting round
    //     a corner steps out from behind the building.
    // While a woken one is in the road ahead (within AmbushSlowDistance), the
    // run slows to AmbushRideSpeed so there is time to type its word. Shoot it
    // and the run goes on at full speed; miss it and it bites once. One the
    // player rides past disappears quietly (no score, no bite).

    private const float AmbushRideSpeed = 3.8f;   // metres per second while a woken ambush zombie is in the road ahead
    private const float AmbushSlowDistance = 35f; // ...within this many metres
    private const float AmbushStartGap = 30f;     // the first one waits at least this far along the run
    private const float AmbushEndGap = 12f;       // none closer than this to the stop (the fight is about to start)
    private const float AmbushSpacing = 30f;      // metres of run per ambush zombie (a 179 m run: about 3)
    private const float AmbushMinApart = 18f;     // metres between two of them along the road
    private const float AmbushSearchStep = 2f;    // looking for a hidden spot: try every 2 m further along
    private const int AmbushMax = 3;

    private readonly List<Zombie> ambushers = new List<Zombie>();
    private int ambushersPlaced;  // this run between two fights (for analytics)
    private int ambushersWoken;
    private readonly Dictionary<Zombie, float> ambushAlong = new Dictionary<Zombie, float>(); // where each one waits: metres along the run (RailMover.Travelled)
    private const float AmbushPassedBy = 1.5f;   // ridden this far past where it waited: it is behind the player, it goes

    private IEnumerator RideAmbush(int wave)
    {
        yield return null; // the run has just been set up: let it start
        float runLength = rail.RemainingDistance;
        float speed = zombieSpeed + zombieSpeedPerWave * (wave - 1);

        // 1. Place them now, before the camera can see them: evenly along the
        //    run, each moved further along until the camera cannot see it.
        int count = Mathf.Clamp(Mathf.FloorToInt((runLength - AmbushStartGap - AmbushEndGap) / AmbushSpacing) + 1, 0, AmbushMax);
        float lastPlaced = -AmbushMinApart;
        for (int i = 0; i < count; i++)
        {
            float planned = Mathf.Lerp(AmbushStartGap, runLength - AmbushEndGap, count == 1 ? 0.5f : (float)i / (count - 1));
            float along = Mathf.Max(planned, lastPlaced + AmbushMinApart);
            while (along <= runLength - AmbushEndGap && CanBeSeen(AmbushSpot(along, i)))
            {
                along += AmbushSearchStep;
            }
            if (along > runLength - AmbushEndGap)
            {
                continue; // no hidden spot left on this run
            }
            PlaceAmbusher(AmbushSpot(along, i), speed, rail.Travelled + along);
            lastPlaced = along;
        }

        // 2. While riding: wake the ones the player can see, drop the ones
        //    ridden past, and slow down while a woken one is in the road ahead.
        while (rail.IsRiding)
        {
            bool slow = false;
            for (int i = ambushers.Count - 1; i >= 0; i--)
            {
                Zombie zombie = ambushers[i];
                if (zombie == null || !zombie.IsAlive)
                {
                    ambushers.RemoveAt(i);
                    continue;
                }
                Vector3 fromPlayer = zombie.Position - player.position;
                fromPlayer.y = 0f;
                // Ridden past: measured along the road (not by the way the player
                // faces, which swings round in a corner).
                if (rail.Travelled > ambushAlong[zombie] + AmbushPassedBy)
                {
                    zombie.Despawn(); // ridden past
                    ambushers.RemoveAt(i);
                    continue;
                }
                if (zombie.IsLurking && CanBeSeen(zombie.Position))
                {
                    WakeAmbusher(zombie);
                }
                if (!zombie.IsLurking && fromPlayer.magnitude < AmbushSlowDistance && Vector3.Dot(fromPlayer, rail.Heading) > 0f)
                {
                    slow = true;
                }
            }
            rail.SpeedCap = slow ? AmbushRideSpeed : float.MaxValue;
            yield return null;
        }
    }

    // A spot 'along' metres along the run from where it started (the run has
    // only just begun), a little to the left or right of the middle of the road.
    private Vector3 AmbushSpot(float along, int number)
    {
        Vector3 at = rail.PointAhead(along);
        Vector3 ahead = rail.PointAhead(along + 0.5f) - at;
        ahead.y = 0f;
        if (ahead.sqrMagnitude < 0.0001f)
        {
            ahead = rail.Heading;
        }
        ahead.Normalize();
        Vector3 side = new Vector3(ahead.z, 0f, -ahead.x) * (number % 2 == 0 ? 0.8f : -0.8f);
        return at + side;
    }

    // Can the camera see a zombie standing here? On screen (with a small margin)
    // and nothing solid (a building) between the camera and its head or chest.
    private static bool CanBeSeen(Vector3 groundPoint)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return true; // cannot tell: treat as seen (never spawn blindly)
        }
        return CanSee(cam, groundPoint + Vector3.up * 1.9f) || CanSee(cam, groundPoint + Vector3.up * 1.0f);
    }

    private static bool CanSee(Camera cam, Vector3 point)
    {
        Vector3 viewport = cam.WorldToViewportPoint(point);
        if (viewport.z <= 0f || viewport.x < -0.05f || viewport.x > 1.05f || viewport.y < -0.05f || viewport.y > 1.05f)
        {
            return false; // behind the camera or off screen
        }
        Vector3 from = cam.transform.position;
        Vector3 toPoint = point - from;
        float distance = toPoint.magnitude;
        if (distance < 0.8f)
        {
            return true;
        }
        // Stop a little short, so the zombie's own body does not count as "in the way".
        return !Physics.Raycast(from, toPoint / distance, distance - 0.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    // A lurking zombie: in the scene, but not yet one of the zombies that can be
    // typed, hit or seen on the HUD (it is out of the alive list until it wakes).
    private void PlaceAmbusher(Vector3 at, float speed, float along)
    {
        Zombie zombie = SpawnZombieAt(at, at, 0.2f, ZombieKind.Normal, speed, "AMBUSH");
        if (zombie == null)
        {
            return;
        }
        aliveZombies.Remove(zombie);
        zombie.StartLurking();
        ambushers.Add(zombie);
        ambushAlong[zombie] = along;
        ambushersPlaced += 1;
    }

    // The player sees it: it gets a short word (quick to type on the move) and
    // joins the alive zombies (word on screen, typeable, red arrow, blasts...).
    private void WakeAmbusher(Zombie zombie)
    {
        zombie.Wake(WordBank.PickWord(UsedFirstLetters(), ZombieKind.Runner));
        aliveZombies.Add(zombie);
        ambushersWoken += 1;
        if (Powers.IsFrenzy)
        {
            zombie.EnterFrenzy(WordBank.PickFrenzyWord(UsedFirstLetters())); // nothing happens if it already has a frenzy word
        }
        Tutorial.Once("ambush", "AMBUSH! Shoot the zombie in the road before you run into it.", 4f);
    }

    // Called by GameManager when the run ends during a ride: the ambush
    // zombies still lurking (they are not in AliveZombies) get their
    // "zombie" analytics event too (woken ones are in AliveZombies already).
    public void LogLurkersRunOver()
    {
        foreach (Zombie zombie in ambushers)
        {
            if (zombie != null && zombie.IsAlive && zombie.IsLurking)
            {
                zombie.LogRunOver();
            }
        }
    }

    // The run is over and the speed limit is lifted. A woken ambush zombie
    // still coming stays and joins the fight (taking it away in plain view
    // would look like it vanished); one still lurking was never seen, so it
    // quietly goes.
    private void ClearAmbush()
    {
        foreach (Zombie zombie in ambushers)
        {
            if (zombie != null && zombie.IsLurking)
            {
                zombie.Despawn();
            }
        }
        ambushers.Clear();
        ambushAlong.Clear();
        rail.SpeedCap = float.MaxValue;
    }

    // ---- A zombie wave ----

    private IEnumerator RunZombieWave(int wave, Encounter encounter)
    {
        // 1. This wave's numbers. Wave 1 uses the base values.
        int zombiesThisWave = zombiesBase + zombiesPerWave * wave;
        float speed = zombieSpeed + zombieSpeedPerWave * (wave - 1);
        float interval = spawnInterval - spawnIntervalPerWave * (wave - 1);
        if (interval < MinSpawnInterval)
        {
            interval = MinSpawnInterval;
        }

        // The WORD CHAIN pairs come on top of the wave's zombies (2 more each).
        HashSet<int> pairStarts;
        ZombieKind[] plan = AddChainPairs(PlanKinds(zombiesThisWave, wave), wave, out pairStarts);
        zombiesThisWave = plan.Length;
        Tutorial.Once("type", "Type the word above a zombie to shoot it. Every letter is a bullet!", 6f);
        LogWaveStart(plan, pairStarts.Count, speed, interval, encounter);

        // 2. Zombies come out in packs of GroupSize, from the spawn points in turn
        //    (a shuffled order, so it is not the same door every time).
        List<SpawnPoint> order = new List<SpawnPoint>(encounter.SpawnPoints);
        if (order.Count == 0)
        {
            Debug.LogError("WaveSpawner: the fight \"" + encounter.name + "\" has no spawn points under it.");
            yield break;
        }
        Shuffle(order);
        int spawned = 0;
        int next = 0;
        while (spawned < zombiesThisWave)
        {
            SpawnPoint point = order[next % order.Count];
            next += 1;

            int pack = Mathf.Min(Mathf.Max(1, encounter.GroupSize), zombiesThisWave - spawned);
            if (point.Door != null)
            {
                point.Door.BurstOpen();
            }

            // A pack of 3 or more does not all come out of one door: its last
            // zombie comes from a door on the OTHER side of the player's view
            // (so a pack from the right sends one from the left, and back).
            SpawnPoint otherSide = pack >= 3 ? OtherSidePoint(point, order, encounter) : null;

            int packLeft = pack;
            int member = 0;
            while (packLeft > 0 && spawned < zombiesThisWave)
            {
                // A WORD CHAIN pair takes two places of the plan and comes out together.
                if (pairStarts.Contains(spawned) && SpawnChainPair(point, order, speed))
                {
                    spawned += 2;
                    packLeft -= 2;
                }
                else
                {
                    bool last = packLeft == 1;
                    if (last && otherSide != null)
                    {
                        if (otherSide.Door != null)
                        {
                            otherSide.Door.BurstOpen();
                        }
                        SpawnZombie(otherSide, plan[spawned], speed);
                    }
                    else
                    {
                        // Side by side, not in one spot: each member of the pack
                        // gets its own lane across the doorway.
                        int atThisDoor = otherSide != null ? pack - 1 : pack;
                        float lane = (member - (atThisDoor - 1) * 0.5f) * PackLaneWidth;
                        SpawnZombieInLane(point, plan[spawned], speed, lane);
                    }
                    spawned += 1;
                    packLeft -= 1;
                }
                member += 1;
                if (packLeft > 0)
                {
                    yield return new WaitForSeconds(PackStagger);
                }
            }
            yield return new WaitForSeconds(interval * pack);
        }

        // 3. Wait until every zombie of this wave is dead or removed.
        while (aliveZombies.Count > 0)
        {
            yield return null;
        }
    }

    // The "wave_start" analytics event: what this wave PLANS to send. (On the
    // Hard level some normal zombies turn into shifters as they spawn; the
    // "zombie" events say what really came.)
    private void LogWaveStart(ZombieKind[] plan, int chainPairs, float speed, float interval, Encounter encounter)
    {
        int[] planned = new int[System.Enum.GetValues(typeof(ZombieKind)).Length];
        foreach (ZombieKind kind in plan)
        {
            planned[(int)kind] += 1;
        }

        AnalyticsEvent e = Analytics.NewEvent("wave_start");
        e.Add("boss_fight", false);
        e.Add("zombies_planned", plan.Length);
        for (int kind = 0; kind < planned.Length; kind++)
        {
            e.Add("planned_" + ((ZombieKind)kind).ToString().ToLowerInvariant(), planned[kind]);
        }
        e.Add("chain_pairs", chainPairs);
        e.Add("zombie_speed", speed);
        e.Add("spawn_interval", interval);
        e.Add("group_size", encounter.GroupSize);
        e.Add("map_view", IsMapViewWave);
        e.Add("barrels", ActiveBarrels.Count);
        e.Add("crates", activeCrates.Count);
        Analytics.AddPlayerState(e);
        Analytics.Send(e);
    }

    // One entry per zombie of the wave (in spawn order): which kind it is.
    private ZombieKind[] PlanKinds(int zombieCount, int wave)
    {
        ZombieKind[] plan = new ZombieKind[zombieCount];
        int index = 0;
        index = Fill(plan, index, ZombieKind.Explosive, CountForWave(wave, explosiveFirstWave, explosiveStartCount, explosivePerWave));
        index = Fill(plan, index, ZombieKind.Runner, CountForWave(wave, runnerFirstWave, runnerStartCount, runnersPerWave));
        Fill(plan, index, ZombieKind.Armored, CountForWave(wave, armoredFirstWave, armoredStartCount, armoredPerWave));
        // The rest stay ZombieKind.Normal (the default value).

        // Fisher-Yates shuffle: the special ones come at random moments.
        for (int i = plan.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            ZombieKind swap = plan[i];
            plan[i] = plan[j];
            plan[j] = swap;
        }
        return plan;
    }

    // Writes kind into plan from index on, count times (never past the end). Returns the next free index.
    private static int Fill(ZombieKind[] plan, int index, ZombieKind kind, int count)
    {
        for (int i = 0; i < count && index < plan.Length; i++)
        {
            plan[index] = kind;
            index += 1;
        }
        return index;
    }

    // How many special zombies of one kind a wave has: none before firstWave,
    // then startCount, plus perWave for every later wave.
    private static int CountForWave(int wave, int firstWave, int startCount, int perWave)
    {
        if (wave < firstWave)
        {
            return 0;
        }
        return startCount + perWave * (wave - firstWave);
    }

    // ---- WORD CHAIN pairs ----

    // Adds this wave's WORD CHAIN pairs to the plan: none before chainFirstWave,
    // then chainStartCount, plus chainPerWave for every later wave. Each pair is
    // two extra NORMAL zombies, next to each other in the plan (they come out at
    // the same moment, from two doors), put in at a random moment of the wave. pairStarts gets the
    // place of the first zombie of each pair.
    private ZombieKind[] AddChainPairs(ZombieKind[] plan, int wave, out HashSet<int> pairStarts)
    {
        pairStarts = new HashSet<int>();
        int pairs = CountForWave(wave, chainFirstWave, chainStartCount, chainPerWave);
        if (pairs <= 0)
        {
            return plan;
        }

        // Each entry of 'units' is one zombie of the plan, or -1 for a whole pair.
        List<int> units = new List<int>();
        foreach (ZombieKind kind in plan)
        {
            units.Add((int)kind);
        }
        for (int p = 0; p < pairs; p++)
        {
            units.Insert(Random.Range(0, units.Count + 1), -1);
        }

        List<ZombieKind> result = new List<ZombieKind>();
        foreach (int unit in units)
        {
            if (unit < 0)
            {
                pairStarts.Add(result.Count);
                result.Add(ZombieKind.Normal);
                result.Add(ZombieKind.Normal);
            }
            else
            {
                result.Add((ZombieKind)unit);
            }
        }
        return result.ToArray();
    }

    // Two normal zombies at the same moment, but out of two DIFFERENT doors
    // (point, and another spawn point of the area), carrying a short word and a
    // longer word that starts with it (hunt / hunter). Both words are purple and
    // a purple line links them across the area (ChainLink): typing the long word
    // kills both. Returns false if no pair of words fits right now.
    private bool SpawnChainPair(SpawnPoint point, List<SpawnPoint> areaPoints, float speed)
    {
        if (HardLevel.Active != null) return false;
        string[] words = WordBank.PickChainPair(UsedFirstLetters());
        if (words == null)
        {
            return false;
        }

        // The other door: any spawn point of the area but this one (an area
        // with a single spawn point uses it for both).
        SpawnPoint otherPoint = point;
        List<SpawnPoint> others = new List<SpawnPoint>();
        foreach (SpawnPoint candidate in areaPoints)
        {
            if (candidate != point)
            {
                others.Add(candidate);
            }
        }
        if (others.Count > 0)
        {
            otherPoint = others[Random.Range(0, others.Count)];
            if (otherPoint.Door != null)
            {
                otherPoint.Door.BurstOpen();
            }
        }

        // Which word comes out of which door is random too.
        bool shortHere = Random.value < 0.5f;
        Zombie shortOne = SpawnZombie(shortHere ? point : otherPoint, ZombieKind.Normal, speed, words[0]);
        Zombie longOne = SpawnZombie(shortHere ? otherPoint : point, ZombieKind.Normal, speed, words[1]);
        shortOne.MarkChainLinked();
        longOne.MarkChainLinked();
        ChainLink.Create(shortOne, longOne);

        GameManager.Instance.RequestTip("word chain", "WORD CHAIN!",
            "Two zombies are linked in PURPLE: \"" + words[0].ToLowerInvariant() + "\" and \""
            + words[1].ToLowerInvariant() + "\".\n\n"
            + "The long word starts with the short one. Type the LONG word:\n"
            + "the short zombie dies on the way, then the long one.\n\n"
            + "One word, two kills - and a WORD CHAIN bonus!",
            Palette.WordChain);
        return true;
    }

    // Called by the Boss's STOMP: a zombie crawls out of the ground at 'at' and
    // walks at the player. A red one blown up next to the boss hurts the boss.
    public Zombie SpawnBossMinion(Vector3 at, ZombieKind kind)
    {
        Vector3 towardPlayer = player.position - at;
        towardPlayer.y = 0f;
        Vector3 exit = at + towardPlayer.normalized * 1.5f;
        return SpawnZombieAt(at, exit, 0.3f, kind, zombieSpeed + zombieSpeedPerWave * 2f);
    }

    // Spawns one zombie at the spawn point. forcedWord: its word (a WORD CHAIN
    // pair's); null = pick a word whose first letter is not on screen yet.
    private Zombie SpawnZombie(SpawnPoint point, ZombieKind kind, float speed, string forcedWord = null)
    {
        return SpawnZombieAt(point.Position, point.Exit, point.Spread, kind, speed, forcedWord);
    }

    // Like SpawnZombie, but 'lane' metres to the side of the door (across the
    // way the zombie walks out), so the zombies of a pack walk side by side.
    private Zombie SpawnZombieInLane(SpawnPoint point, ZombieKind kind, float speed, float lane)
    {
        Vector3 walk = point.Exit - point.Position;
        walk.y = 0f;
        Vector3 across = walk.sqrMagnitude > 0.001f ? new Vector3(walk.z, 0f, -walk.x).normalized : Vector3.right;
        Vector3 shift = across * lane;
        return SpawnZombieAt(point.Position + shift * 0.5f, point.Exit + shift, point.Spread * 0.5f, kind, speed);
    }

    // A spawn point on the other side of the player's view (left / right of the
    // direction faced at the stop) from 'point', or null if there is none.
    private static SpawnPoint OtherSidePoint(SpawnPoint point, List<SpawnPoint> points, Encounter encounter)
    {
        Vector3 facing = encounter.Facing;
        Vector3 right = new Vector3(facing.z, 0f, -facing.x);
        float side = Vector3.Dot(point.Exit - encounter.StopPosition, right);

        List<SpawnPoint> others = new List<SpawnPoint>();
        foreach (SpawnPoint candidate in points)
        {
            float candidateSide = Vector3.Dot(candidate.Exit - encounter.StopPosition, right);
            if (candidate != point && candidateSide * side < 0f)
            {
                others.Add(candidate);
            }
        }
        return others.Count > 0 ? others[Random.Range(0, others.Count)] : null;
    }

    // The same, from any spot: the zombie appears at spawnAt (plus a random
    // offset up to spread metres), walks to exitAt, then at the player.
    private Zombie SpawnZombieAt(Vector3 spawnAt, Vector3 exitAt, float spread, ZombieKind kind, float speed, string forcedWord = null)
    {
        if (HardLevel.Active != null && kind == ZombieKind.Normal && HardLevel.Active.NextIsShifter())
            kind = ZombieKind.Shifter;
        // A small random offset so a pack does not stand in one spot.
        Vector2 offset = Random.insideUnitCircle * spread;
        Vector3 side = new Vector3(offset.x, 0f, offset.y);
        Vector3 position = spawnAt + side;
        Vector3 exit = exitAt + side * 0.5f;

        string word = forcedWord;
        if (word == null)
        {
            List<char> used = UsedFirstLetters();
            word = kind == ZombieKind.Armored ? WordBank.PickArmorWord(used)
                : WordBank.PickWord(used, HardLevel.Active != null && kind != ZombieKind.Runner ? ZombieKind.Explosive : kind);
        }

        Quaternion rotation = Quaternion.identity;
        Vector3 walk = exit - position;
        walk.y = 0f;
        if (walk.sqrMagnitude > 0.001f)
        {
            rotation = Quaternion.LookRotation(walk);
        }

        Zombie zombie = Instantiate(zombiePrefab, position, rotation);
        zombie.Setup(word, speed, player, this, kind, exit);
        aliveZombies.Add(zombie);

        // Born during a FRENZY: a short word right away (its real word comes back with the others).
        if (Powers.IsFrenzy)
        {
            zombie.EnterFrenzy(WordBank.PickFrenzyWord(UsedFirstLetters()));
        }

        // The first time each special kind shows up, say what it does.
        if (kind == ZombieKind.Shifter)
        {
            Tutorial.Once("shifter", "PURPLE SHIFTER: its word changes while idle. Type exact case and punctuation!", 6f);
        }
        else if (kind == ZombieKind.Explosive)
        {
            Tutorial.Once("explosive", "RED ZOMBIE: finish its word to blow up EVERYTHING in its ring!", 6f);
        }
        else if (kind == ZombieKind.Runner)
        {
            Tutorial.Once("runner", "RUNNER! Fast and yellow - shoot it before the slow ones.", 6f);
        }
        else if (kind == ZombieKind.Armored)
        {
            Tutorial.Once("armored", "ARMORED: the first word breaks its armor. Explosions kill it at once!", 6f);
        }
        return zombie;
    }

    private static void Shuffle(List<SpawnPoint> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            SpawnPoint swap = list[i];
            list[i] = list[j];
            list[j] = swap;
        }
    }

    // ---- Barrels and supply crates of the area ----

    // Gives every barrel and crate of the encounter a word: they can be typed now.
    private void ActivateProps(Encounter encounter)
    {
        foreach (Barrel barrel in encounter.Barrels)
        {
            if (barrel != null && !barrel.IsExploded)
            {
                barrel.Activate(WordBank.PickBarrelWord(UsedFirstLetters()), hud);
                ActiveBarrels.Add(barrel);
                LogProp("barrel", barrel.Word, barrel.Position, Barrel.BlastRadius);
            }
        }
        foreach (SupplyCrate crate in encounter.Crates)
        {
            if (crate != null && !crate.IsOpened)
            {
                crate.Activate(WordBank.PickCrateWord(crate.Kind, UsedFirstLetters()), hud);
                activeCrates.Add(crate);
                LogProp("crate_" + crate.Kind.ToString().ToLowerInvariant(), crate.Word, crate.Position, 0f);
            }
        }
    }

    // The "prop" analytics event: a barrel or crate got its word (it can be typed now).
    private static void LogProp(string kind, string word, Vector3 position, float blastRadius)
    {
        AnalyticsEvent e = Analytics.NewEvent("prop");
        e.Add("kind", kind);
        e.Add("word", word);
        if (blastRadius > 0f)
        {
            e.Add("blast_radius", blastRadius);
        }
        e.Add("x", position.x);
        e.Add("z", position.z);
        Analytics.Send(e);
    }

    // The fight is over: barrels and crates left behind cannot be typed any more.
    private void DeactivateProps()
    {
        foreach (Barrel barrel in ActiveBarrels)
        {
            if (barrel != null && !barrel.IsExploded)
            {
                barrel.Deactivate();
            }
        }
        ActiveBarrels.Clear();

        foreach (SupplyCrate crate in activeCrates)
        {
            if (crate != null && !crate.IsOpened)
            {
                crate.Deactivate();
            }
        }
        activeCrates.Clear();
    }

    // Explains barrels and crates the first time they appear (a few seconds
    // into the fight, so it does not hide the first hint).
    private IEnumerator ShowPropHints()
    {
        yield return new WaitForSeconds(6f);
        foreach (Barrel barrel in ActiveBarrels)
        {
            if (barrel != null && barrel.IsAlive)
            {
                Tutorial.Once("barrel", "BARREL: type its orange word to blow up everything in its ring. Wait for zombies to walk in!", 7f);
                break;
            }
        }

        yield return new WaitForSeconds(7f);
        foreach (SupplyCrate crate in activeCrates)
        {
            if (crate != null && crate.IsAlive)
            {
                Tutorial.Once("crate", "SUPPLY CRATE: type its green word to grab what is inside.", 6f);
                break;
            }
        }
    }

    // ---- The boss ----

    private IEnumerator RunBossFight(Encounter encounter)
    {
        // 1. The boss appears where it fights.
        CameraDirector.Shake(0.4f);
        boss = Boss.Create(encounter.BossStand, player.position, this, hud);
        hud.ShowBossBar(bossName);

        AnalyticsEvent e = Analytics.NewEvent("wave_start");
        e.Add("boss_fight", true);
        e.Add("boss_health", boss.Health);
        e.Add("barrels", ActiveBarrels.Count);
        e.Add("crates", activeCrates.Count);
        Analytics.AddPlayerState(e);
        Analytics.Send(e);

        // 2. Wait until it is dead.
        while (boss.IsAlive)
        {
            yield return null;
        }

        // Zombies it summoned fall down with it.
        foreach (Zombie minion in new List<Zombie>(aliveZombies))
        {
            minion.KillByBlast(boss.BodyCenter, "boss_died");
        }

        // 3. Let it sink into the ground.
        yield return new WaitForSeconds(1.5f);
        boss = null;
        hud.HideBossBar();
    }
}
