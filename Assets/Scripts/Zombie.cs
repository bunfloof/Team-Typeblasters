// Zombie.cs
// ---------------------------------------------------------------------------
// One zombie: a capsule that walks at the player and carries a word.
// WaveSpawner creates it and calls Setup().
//
// There are four kinds, colour coded (see ZombieKind):
//   - NORMAL    (light grey): typing its word kills it.
//   - RUNNER    (yellow, thin): about twice as fast, short word. Kill it first.
//   - ARMORED   (steel blue): its FIRST word breaks the armour (it turns grey)
//               and it gets a second word. Any explosion kills it at once.
//   - EXPLOSIVE (big, red, ring on the ground): always a long word.
//               blastRadius = word length x radiusPerLetter. Finishing its word
//               blows it up, and EVERYTHING inside the ring dies too (see
//               Explosion). What you see (the ring) is what explodes.
//   - SHIFTER   (purple, Hard and Extreme typing difficulty only): its word
//               CHANGES every few seconds while nobody types it. Typing its
//               first letter locks the word (see TypingSettings.ShifterSeconds).
//   - SPLITTER  (teal, chunky): a word that breaks in two ("SUNSET" = "SUN" +
//               "SET", see WordBank.PickSplitterWord). Shot dead, it SPLITS
//               into two SPLITLINGS (small, quick, light teal, half a bite),
//               each carrying a new word that HIDES one half, underlined
//               ("SUNSHINE", "UPSET"). An explosion kills it whole, no split.
//   - GHOST     (pale lavender, floats, VERY slow, half a bite): a LONG word
//               shown only for a moment, then hidden as "_ _ _". It stays
//               hidden while it is typed: the player types it from memory.
//               If nobody is typing it, it flashes back now and then.
//   - BRUTE     (huge, dark, slow): carries a PHRASE of 2-3 words, typed with
//               the SPACES in between, like a typing test. Its bite hurts double.
//   - SPITTER   (acid green): stops at a distance and SPITS acid globs at the
//               player, each with its own short word (an EnergyOrb: type it to
//               pop it, or it hurts). It keeps spitting until it is shot.
//   - SNIPER    (khaki, ENDLESS only): appears on a perch in the STANDS and
//               never moves. Every few seconds it AIMS (a red laser at the
//               player, blinking faster just before the shot) and FIRES one
//               bullet with a short word (an EnergyOrb: type it to stop it, or
//               it hurts). Its own word kills it. See WaveSpawner's snipers.
//
// Every kind's word is a light shade of its body colour (Palette.WordFor).
// The word itself is styled for the TYPING DIFFICULTY (TypingSettings.Style):
// lowercase on Easy, then mixed case, punctuation and symbols.
//
// Where it walks: out of its doorway first (to the spawn point's exit), then
// straight at the player. If a lure bomb is on the ground nearby, it walks to
// the bomb instead and crowds around it. While the freeze power is on, it
// stands still and turns icy blue. While the SHIELD is up it moves slower and,
// if it reaches the player, bounces off the shield instead of biting (it stays
// alive, so it can still be shot). Every bullet that hits it pushes it back a
// little. When it dies, the capsule is knocked over (see Corpse).
//
// A zombie that REACHES the player does not go away: it stays right there and
// bites again every few seconds (BaseBiteInterval, adaptive) from a few metres
// away (its stand-off) until it is shot, blown up (barrel,
// red zombie, RPG, lure bomb) or lured away by a lure bomb. Freeze stops it.
//
// THE WORD is not part of the 3D scene: it is a HUD text (HUD.CreateWordLabel)
// pinned above the zombie by WaveSpawner.LayoutLabels, so no body can hide it.
//
// This script lives on the root of the Zombie prefab (Assets/Prefabs/Zombie).
// The root sits at ground level (y = 0); the "Body" capsule and the "Ring" are
// children. Keep the root's scale at (1, 1, 1): the ring must match the blast
// radius exactly. To resize a zombie, scale the Body (see Setup).
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum ZombieKind
{
    Normal,
    Runner,
    Armored,
    Explosive,
    Shifter,
    Splitter,
    Splitling,  // one of the two halves of a Splitter
    Ghost,
    Brute,
    Spitter,
    Sniper      // Endless: shoots from the stands (keep it last: kinds are saved as numbers)
}

public class Zombie : MonoBehaviour, ITypingTarget
{
    [Header("Tuning")]
    [SerializeField] private float attackDistance = 1.5f;  // how close it must get to hit the player
    [SerializeField] private int damagePerHit = 10;
    [SerializeField] private float radiusPerLetter = 0.75f; // blast radius in metres per letter of the word (explosive zombies only)
    [SerializeField] private Color explosiveBodyColor = new Color(0.9f, 0.25f, 0.2f); // body tint of an explosive zombie
    [SerializeField] private float explosiveBodyScale = 1.5f; // an explosive zombie's body is this many times bigger

    [Header("Kinds")]
    [SerializeField] private float runnerSpeedMultiplier = 2f;      // runners are this many times faster
    [SerializeField] private float armoredSpeedMultiplier = 0.8f;   // armored zombies are slower
    [SerializeField] private float explosiveSpeedMultiplier = 0.85f;

    [Header("Hits")]
    [SerializeField] private float flinchSeconds = 0.15f;  // each bullet slows it down for this long
    [SerializeField] private float flinchSlowdown = 0.25f; // speed multiplier while hit

    [Header("Spacing")]
    [SerializeField] private float separationDistance = 1.2f; // wanted gap (metres) between two zombies' bodies
    [SerializeField] private float separationStrength = 1.5f; // how hard they step away from each other

    [Header("References (wired in the Zombie prefab)")]
    [SerializeField] private TMP_Text label;  // the prefab's old world-space word; switched off (see header)
    [SerializeField] private BlastRing ring;  // ring on the ground (hidden unless explosive)

    // The word sits this many metres above the top of the head.
    private const float LabelAboveHead = 0.3f;

    // A red zombie that blows up next to the boss (the boss summons them) hurts it this much.
    private const float BlastBossDamage = 100f;

    // A zombie this far from the player can never matter again: it is removed.
    private const float LostDistance = 120f;

    // If it has not reached its doorway exit after this long, it gives up on it.
    private const float ExitTimeout = 8f;
    // Inside its doorway / tunnel a zombie moves this much faster: it is out
    // of sight there (no word yet), so the player should not wait for it.
    private const float EmergeSpeedMultiplier = 2.5f;

    // ---- No walking through walls ----
    // Zombies walk straight (no path finding), but every move goes through
    // MoveBy: a sphere at waist height is swept along the move, and if it
    // would hit the scene (a wall, a building, the stands) the zombie slides
    // along that surface instead of going in. Things that are not walls never
    // stop it: other zombies, barrels, crates, bodies, orbs, lure bombs, doors
    // (they swing open), gates, and anything low enough to step over (below
    // the sphere: curbs, low walls, the ground). A zombie that starts inside a
    // building (a spawn point behind a door) is not stopped by that building:
    // a sweep ignores what it starts in, so it can still walk out.
    private const float WallProbeHeight = 1.0f;   // metres above the feet
    private const float WallProbeRadius = 0.35f;
    private const float WallSkin = 0.05f;         // stop this far short of a wall
    private static readonly RaycastHit[] wallHits = new RaycastHit[8];

    private void MoveBy(Vector3 move)
    {
        move.y = 0f;
        for (int attempt = 0; attempt < 2 && move.sqrMagnitude > 0.000001f; attempt++)
        {
            RaycastHit wall;
            if (!FindWall(move, out wall))
            {
                transform.position += move;
                return;
            }
            // Go up to the wall, then slide along it with what is left of the move.
            float distance = move.magnitude;
            Vector3 direction = move / distance;
            float allowed = Mathf.Max(0f, wall.distance - WallSkin);
            transform.position += direction * allowed;
            Vector3 normal = wall.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f)
            {
                return;
            }
            normal.Normalize();
            Vector3 left = direction * (distance - allowed);
            move = left - normal * Vector3.Dot(left, normal); // the part along the wall
        }
    }

    private bool FindWall(Vector3 move, out RaycastHit wall)
    {
        wall = default(RaycastHit);
        float distance = move.magnitude;
        Vector3 origin = transform.position + Vector3.up * WallProbeHeight;
        int count = Physics.SphereCastNonAlloc(origin, WallProbeRadius, move / distance, wallHits, distance + WallSkin,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = wallHits[i];
            if (hit.collider == null || hit.distance <= 0f || !IsWall(hit.collider) || (player != null && hit.collider.transform.IsChildOf(player)))
            {
                continue; // distance 0: it started inside it (e.g. its own spawn building)
            }
            if (!found || hit.distance < wall.distance)
            {
                wall = hit;
                found = true;
            }
        }
        return found;
    }

    private static bool IsWall(Collider collider)
    {
        if (collider.attachedRigidbody != null)
        {
            return false; // anything that moves on its own (bodies, debris)
        }
        return collider.GetComponentInParent<Zombie>() == null && collider.GetComponentInParent<Barrel>() == null
            && collider.GetComponentInParent<SupplyCrate>() == null && collider.GetComponentInParent<Corpse>() == null
            && collider.GetComponentInParent<EnergyOrb>() == null && collider.GetComponentInParent<LureBomb>() == null
            && collider.GetComponentInParent<Door>() == null && collider.GetComponentInParent<Gate>() == null
            && collider.GetComponentInParent<Boss>() == null;
    }

    // TextMeshPro rich-text colour for the letters typed so far (yellow).
    // Letters already typed turn dim grey, so what is left stands out (yellow
    // used to clash with the yellow runner and acid spitter words).
    public const string TypedColorTag = "<color=#7E848E>";

    public string Word { get; private set; }
    public int TypedCount { get; private set; }       // how many letters have been typed so far
    public float BlastRadius { get; private set; }    // 0 unless explosive
    public bool IsExplosive { get; private set; }
    public ZombieKind Kind { get; private set; }
    public bool HasArmor { get; private set; }        // armored zombie that still wears its armour
    public bool IsAlive { get; private set; }
    public string ColoredWord { get; private set; }   // the word as rich text: typed part yellow, rest white
    public float BodyRadius { get; private set; }     // metres, used for spacing
    public TMP_Text Label { get; private set; }       // the word on the HUD

    private float speed;
    private float baseSpeed;        // the wave's speed (a splitter's splitlings start from it)
    private bool caseSensitive;     // false on Easy: the word is stored UPPERCASE and shown in lowercase
    private float wordChangeTimer;  // shifters only: seconds since the word last changed
    private Transform player;
    private WaveSpawner spawner;
    private Transform body;         // the capsule
    private Renderer bodyRenderer;
    private Material normalMaterial; // the body's material when not frozen
    private float headHeight;       // metres from the feet to the top of the head

    // Where it walks first (out of its doorway), and whether it got there.
    private Vector3 exitPoint;

    // Where it walks out of its doorway (CameraDirector waits for zombies about to appear there).
    public Vector3 ExitPoint
    {
        get { return exitPoint; }
    }
    private bool reachedExit;

    // True once it has walked out of its doorway / tunnel (reached its Exit).
    // Before that it is still out of sight inside, so its word is not shown
    // and it cannot be typed (WaveSpawner.GetTypingTargets): no killing a
    // zombie the player has not seen yet.
    public bool HasEmerged
    {
        get { return reachedExit; }
    }
    private float exitTimer;

    // How close it stands to a lure bomb (each zombie a bit different, so they crowd around it).
    private float crowdDistance;

    private float flinchTimer;
    private bool lookingFrozen;

    // ---- Analytics (see LogSpawn, LogEnd and the red zombie's "blast" report) ----
    public int Id { get; private set; }       // zombie_id: its number within the run
    private bool spawnLogged;                 // "zombie_spawn" sent (on its first active frame)
    private float spawnPlayTime;              // play seconds of the run when it appeared
    private float wokePlayTime = -1f;         // ...when an ambusher woke up (-1 = never)
    private float firstTypedPlayTime = -1f;   // ...when its first letter was first typed (-1 = never)
    private float lastStartPlayTime = -1f;    // ...when its first letter was typed the LAST time
    private int typingStarts;                 // how many times the player started a word of it (Backspace or a FRENZY make them start again; an armored zombie's second word counts too)
    private int wordShifts;                   // purple shifters: how often the word changed
    // RED zombies: how many OTHER zombies stood inside the ring
    private int inRingAtFirstLetter = -1;     // ...when the player first typed its first letter
    private int inRingAtLastStart = -1;       // ...when the player typed its first letter the last time
    private int inRingMaxWhileTyped;          // ...at most while its word was being typed (any attempt)
    private int inRingMax;                    // ...at most during its whole life

    // Called by WaveSpawner right after it creates this zombie.
    //   exit: the point just outside its doorway; it walks there first.
    public void Setup(string word, float moveSpeed, Transform playerTransform, WaveSpawner owner,
        ZombieKind kind, Vector3 exit)
    {
        player = playerTransform;
        spawner = owner;
        caseSensitive = TypingSettings.IsCaseSensitive;
        Word = Styled(word);
        Kind = kind;
        exitPoint = exit;

        IsAlive = true;
        TypedCount = 0;
        Id = Analytics.NextZombieId();
        spawnPlayTime = GameManager.Instance.Stats.Seconds;
        IsExplosive = kind == ZombieKind.Explosive;
        HasArmor = kind == ZombieKind.Armored;
        crowdDistance = Random.Range(0.8f, 1.8f);

        // The body: the prefab's capsule (2 m tall, 1 m wide), sized and coloured by kind.
        body = transform.Find("Body");
        bodyRenderer = body.GetComponent<Renderer>();
        Vector3 size = Vector3.one;
        baseSpeed = moveSpeed;
        speed = moveSpeed * (HardLevel.Active != null ? HardLevel.Active.enemySpeedMultiplier : 1f);
        if (kind == ZombieKind.Splitter)
        {
            size = new Vector3(1.3f, 1.05f, 1.3f);
            bodyRenderer.sharedMaterial = Palette.Lit(Palette.SplitterTeal);
            speed *= 0.9f;
            splitRaw = word;
            splitAt = WordBank.SplitterSplit(word);
        }
        else if (kind == ZombieKind.Splitling)
        {
            size = new Vector3(0.6f, 0.65f, 0.6f);
            bodyRenderer.sharedMaterial = Palette.Lit(Palette.SplitlingTeal);
            speed *= 1.5f;
        }
        else if (kind == ZombieKind.Ghost)
        {
            size = new Vector3(0.85f, 1.05f, 0.85f);
            bodyRenderer.sharedMaterial = Palette.Lit(Palette.GhostPale);
            speed *= GhostSpeedMultiplier;
            ghostFirstLook = GhostLookBase + GhostLookPerLetter * word.Length;
            ghostRevealed = true;
        }
        else if (kind == ZombieKind.Brute)
        {
            size = new Vector3(1.6f, 1.35f, 1.6f);
            bodyRenderer.sharedMaterial = Palette.Lit(Palette.BruteDark);
            speed *= 0.6f;
        }
        else if (kind == ZombieKind.Spitter)
        {
            size = new Vector3(1f, 0.95f, 1f);
            bodyRenderer.sharedMaterial = Palette.Lit(Palette.SpitterAcid);
        }
        else if (kind == ZombieKind.Sniper)
        {
            size = new Vector3(0.8f, 0.95f, 0.8f);
            bodyRenderer.sharedMaterial = Palette.Lit(Palette.SniperKhaki);
        }
        if (kind == ZombieKind.Shifter)
        {
            bodyRenderer.sharedMaterial = Palette.Lit(Palette.ShifterPurple);
            size = new Vector3(0.85f, 1.15f, 0.85f);
        }
        if (kind == ZombieKind.Runner)
        {
            size = new Vector3(0.7f, 0.9f, 0.7f);
            bodyRenderer.sharedMaterial = Palette.Lit(Palette.RunnerYellow);
            speed *= runnerSpeedMultiplier;
        }
        else if (kind == ZombieKind.Armored)
        {
            size = new Vector3(1.15f, 1f, 1.15f);
            bodyRenderer.sharedMaterial = Palette.Lit(Palette.ArmorSteel);
            speed *= armoredSpeedMultiplier;
        }
        else if (kind == ZombieKind.Explosive)
        {
            size = Vector3.one * explosiveBodyScale;
            bodyRenderer.sharedMaterial = Palette.Lit(explosiveBodyColor);
            speed *= explosiveSpeedMultiplier;
        }
        // Scale the Body, NOT the root (that would stretch the ring). The capsule's
        // centre is at half its height, so it moves up too and its feet stay on the ground.
        body.localScale = size;
        body.localPosition = new Vector3(0f, size.y, 0f);
        normalMaterial = bodyRenderer.sharedMaterial;
        headHeight = 2f * size.y;
        BodyRadius = 0.5f * size.x;

        if (IsExplosive)
        {
            BlastRadius = word.Length * radiusPerLetter;
            ring.Show(BlastRadius, true); // true = the orange ring
        }
        else
        {
            BlastRadius = 0f;
            ring.gameObject.SetActive(false);
        }

        // The word lives on the HUD; the prefab's old world-space label is not used.
        label.gameObject.SetActive(false);
        Label = spawner.Hud.CreateWordLabel(new Vector2(0.5f, 0f)); // centred, just above the head
        Label.color = Palette.WordFor(kind); // its body colour, bright enough to read
        RefreshLabel();
    }

    // True for the two zombies of a WORD CHAIN pair (hunt / hunter).
    public bool IsChainLinked { get; private set; }

    // Called by WaveSpawner.SpawnChainPair: the word turns purple, like the
    // ChainLink line between the pair, so the player sees they belong together.
    public void MarkChainLinked()
    {
        IsChainLinked = true;
        if (realWord != null)
        {
            realColor = Palette.WordChain; // purple again once the frenzy is over
        }
        else
        {
            Label.color = Palette.WordChain;
        }
    }

    // ---- FRENZY (a combo weapon, see Powers) ----
    // While it lasts the zombie carries a SHORT word (4 letters at most) in a
    // vivid colour; afterwards it gets its real word (and colour) back.

    private string realWord;   // the real word under the frenzy word (null = no frenzy word)
    private Color realColor;

    public void EnterFrenzy(string shortWord)
    {
        if (!IsAlive || realWord != null)
        {
            return;
        }
        realWord = Word;
        realColor = Label.color;
        Word = TypingSettings.StyleShort(shortWord);
        TypedCount = 0;
        Label.color = Palette.WordFrenzy;
        RefreshLabel();
    }

    public void ExitFrenzy()
    {
        if (realWord == null)
        {
            return;
        }
        // Already started (being typed, or waiting with its letters): it keeps
        // its short word until it is finished - or dropped (ResetProgress).
        if (TypedCount > 0)
        {
            frenzyExitPending = true;
            return;
        }
        frenzyExitPending = false;
        Word = realWord;
        realWord = null;
        TypedCount = 0;
        if (IsAlive)
        {
            Label.color = realColor;
            RefreshLabel();
        }
    }

    // ---- Lurking (an ambush on the run, see WaveSpawner.RideAmbush) ----
    // A lurking zombie waits out of sight (behind a building or off screen):
    // it stands still, has no word on screen and cannot be typed. When the
    // player first sees it, WaveSpawner wakes it: it gets a word and walks out.

    public bool IsLurking { get; private set; }

    // A zombie met on the run between two fights (it started out lurking). Its
    // bite only costs one step of combo, not the whole combo.
    public bool IsAmbusher { get; private set; }

    public void StartLurking()
    {
        IsLurking = true;
        IsAmbusher = true;
        Label.gameObject.SetActive(false);
    }

    // Wakes it with a new word (chosen now, so its first letter does not clash
    // with the words that came on screen while it waited).
    public void Wake(string word)
    {
        word = Styled(word);
        IsLurking = false;
        wokePlayTime = GameManager.Instance.Stats.Seconds;
        if (realWord != null && !Powers.IsFrenzy)
        {
            ExitFrenzy(); // a frenzy started and ended while it waited
        }
        if (realWord != null)
        {
            realWord = word; // still in a frenzy: this is the real word for later
        }
        else
        {
            Word = word;
            TypedCount = 0;
        }
        Label.gameObject.SetActive(true);
        RefreshLabel();
    }

    private void Update()
    {
        if (!IsAlive || IsLurking || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        // Analytics: the first frame it is active (an ambusher: once the player saw it).
        if (!spawnLogged)
        {
            spawnLogged = true;
            LogSpawn();
        }

        // Analytics: how full the red ring is.
        if (IsExplosive)
        {
            int inRing = CountInRing();
            inRingMax = Mathf.Max(inRingMax, inRing);
            if (TypedCount > 0)
            {
                inRingMaxWhileTyped = Mathf.Max(inRingMaxWhileTyped, inRing);
            }
        }

        // GHOST: its word fades in and out (also while frozen).
        if (Kind == ZombieKind.Ghost)
        {
            UpdateGhost();
        }

        // Freeze power: stand still, icy blue.
        SetFrozenLook(Powers.IsFrozen);
        if (Powers.IsFrozen)
        {
            ShowLaser(false); // a frozen sniper stops aiming (no-op for the others)
            return;
        }

        // The guided first wave holds the world while it explains something:
        // zombies wait (no walking, biting or spitting), everything else runs.
        // One still in its doorway first walks out (its word only shows outside,
        // and the lesson may be waiting for the player to type it).
        if (GuidedIntro.Holding && reachedExit)
        {
            return;
        }

        // SHIFTER: a new word every few seconds. Never replaces a partially typed
        // word or one with a killing shot in flight (typing it locks it).
        if (Kind == ZombieKind.Shifter && realWord == null && !IsWordComplete)
        {
            wordChangeTimer += Time.deltaTime;
            if (wordChangeTimer >= TypingSettings.ShifterSeconds && TypedCount == 0)
            {
                List<char> used = spawner.UsedFirstLetters();
                used.Remove(char.ToUpperInvariant(Word[0]));
                Word = TypingSettings.Style(WordBank.PickWord(used, ZombieKind.Armored), used);
                wordChangeTimer = 0f;
                wordShifts += 1;
                RefreshLabel();
                spawner.Hud.ShowFloatingText(LabelAnchor, "WORD SHIFT!", ShifterColor, true);
            }
        }

        // SNIPER: stands on its perch, turns to the player, aims and fires.
        if (Kind == ZombieKind.Sniper)
        {
            reachedExit = true; // it is in plain sight on the stands from the start
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(toPlayer);
            }
            UpdateSnipe();
            return;
        }

        // 1. Where to go: out of the doorway first, then to a lure bomb or the player.
        Vector3 goal;
        float stopDistance = 0f;
        bool chasingPlayer = false;
        if (!reachedExit)
        {
            goal = exitPoint;
            exitTimer += Time.deltaTime;
            if (FlatDistance(transform.position, exitPoint) < 0.4f || exitTimer > ExitTimeout)
            {
                reachedExit = true;
            }
        }
        else
        {
            LureBomb lure = LureBomb.FindLure(transform.position);
            if (lure != null)
            {
                goal = lure.Position;
                stopDistance = crowdDistance;
            }
            else
            {
                goal = player.position;
                chasingPlayer = true;
                // It stops a few metres away and attacks from there (a spitter
                // from much farther): right in front of the camera its word
                // would be pushed off the top of the screen and jump around.
                stopDistance = Kind == ZombieKind.Spitter ? SpitRange : StandOffDistance;
                if (HasApproach && Kind != ZombieKind.Spitter && CameraDirector.MapBlend <= 0.5f)
                {
                    goal = ApproachGoal(out stopDistance); // its own curve to its own spot in the fan
                }
            }
        }

        // 2. Step toward the goal, keeping a little distance from other zombies.
        Vector3 toGoal = goal - transform.position;
        toGoal.y = 0f;
        Vector3 direction = Vector3.zero;
        if (toGoal.magnitude > stopDistance)
        {
            direction = toGoal.normalized;
        }
        direction += SeparationPush() * separationStrength;
        direction.y = 0f;
        direction = Vector3.ClampMagnitude(direction, 1f);

        float currentSpeed = speed * Powers.EnemySpeedFactor; // slower while the shield is up
        if (!reachedExit)
        {
            currentSpeed *= EmergeSpeedMultiplier; // bursts out of its doorway / tunnel, then walks
        }
        if (flinchTimer > 0f)
        {
            flinchTimer -= Time.deltaTime;
            currentSpeed *= flinchSlowdown;
        }
        MoveBy(direction * currentSpeed * Time.deltaTime); // slides along walls instead of going through

        // SPITTER: in range of the player, it spits acid globs.
        if (Kind == ZombieKind.Spitter && chasingPlayer
            && FlatDistance(transform.position, player.position) <= SpitRange + 1f)
        {
            UpdateSpit();
        }

        // 3. Close enough to bite (or to bounce off the shield).
        if (chasingPlayer && FlatDistance(transform.position, player.position) <= BiteRange)
        {
            if (Powers.IsShielded)
            {
                BounceOffShield();
                return;
            }
            Attack();
            return;
        }

        // Safety net: wandered off somewhere it can never matter again.
        if (FlatDistance(transform.position, player.position) > LostDistance)
        {
            Remove("lost");
        }
    }

    public void SetTrainingSpeed(float value)
    {
        if (EasyTutorial.Active != null) speed = Mathf.Max(0f, value);
    }

    // Sum of "step away" directions from every zombie that is too close. Each
    // push is stronger the closer the other zombie is (0 at the wanted distance).
    private Vector3 SeparationPush()
    {
        // Near the player (first person) they stand further apart, side by
        // side in a fan, so their words never pile up on top of each other.
        float gap = separationDistance;
        if (CameraDirector.MapBlend <= 0.5f && FlatDistance(transform.position, player.position) < StandOffDistance + 3f)
        {
            gap = Mathf.Max(gap, NearPlayerGap);
        }
        Vector3 push = Vector3.zero;
        foreach (Zombie other in spawner.AliveZombies)
        {
            if (other == this)
            {
                continue;
            }

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float distance = away.magnitude;
            float wanted = BodyRadius + other.BodyRadius + gap;
            if (distance >= wanted)
            {
                continue;
            }

            if (distance < 0.001f)
            {
                away = Random.insideUnitSphere; // exactly on top of each other: pick any direction
                away.y = 0f;
                distance = 0.001f;
            }
            push += away.normalized * (1f - distance / wanted);
        }
        return push;
    }

    // Switches the body to the icy material (frozen) or back.
    private void SetFrozenLook(bool frozen)
    {
        if (frozen == lookingFrozen)
        {
            return;
        }
        lookingFrozen = frozen;
        bodyRenderer.sharedMaterial = frozen ? Palette.Lit(Palette.FrozenIce) : normalMaterial;
    }

    // ---- Hits (called by Bullet for every letter except the last) ----

    // A bullet hit it: it is pushed back a little and slowed for a moment.
    public void Flinch(Vector3 bulletDirection)
    {
        if (!IsAlive)
        {
            return;
        }
        flinchTimer = flinchSeconds;
        Vector3 push = bulletDirection;
        push.y = 0f;
        if (push.sqrMagnitude > 0.0001f)
        {
            MoveBy(push.normalized * 0.06f);
        }
    }

    // ---- Word on screen (used by WaveSpawner.LayoutLabels) ----

    public Vector3 LabelAnchor
    {
        get { return transform.position + Vector3.up * (headHeight + LabelAboveHead); }
    }

    // True while this zombie is the player's current target (at least one letter typed).
    public bool IsTargeted
    {
        get { return TypedCount > 0; }
    }

    public bool IsCaseSensitive
    {
        get { return caseSensitive; }
    }

    // ---- Typing progress (used by TypingController) ----

    // The letter the player has to type next.
    public char NextLetter
    {
        get { return Word[TypedCount]; }
    }

    public bool IsWordComplete
    {
        get { return TypedCount >= Word.Length; }
    }

    public void AdvanceProgress()
    {
        TypedCount += 1;
        RefreshLabel();

        // Its first letter was just typed: the player started its word (again).
        if (TypedCount == 1)
        {
            float now = GameManager.Instance.Stats.Seconds;
            typingStarts += 1;
            lastStartPlayTime = now;
            if (IsExplosive)
            {
                inRingAtLastStart = CountInRing();
            }
            if (firstTypedPlayTime < 0f)
            {
                firstTypedPlayTime = now;
                inRingAtFirstLetter = inRingAtLastStart;
            }
        }
    }

    // Called when the player drops this zombie as their target.
    public void ResetProgress()
    {
        TypedCount = 0;
        if (frenzyExitPending && !Powers.IsFrenzy)
        {
            ExitFrenzy(); // the frenzy ended while it was being typed: its real word comes back now
            return;
        }
        RefreshLabel();
    }

    private bool frenzyExitPending; // the frenzy is over, but its short word stays until it is finished or dropped

    // ---- Dying ----

    public Vector3 Position
    {
        get { return transform.position; }
    }

    // Bullets aim at the middle of the body.
    public Vector3 HitPoint
    {
        get { return transform.position + Vector3.up * headHeight * 0.6f; }
    }

    // Called by the Bullet of the LAST letter when it hits.
    public void CompleteWord()
    {
        if (!IsAlive)
        {
            return;
        }

        if (HasArmor)
        {
            BreakArmor();
            return;
        }

        if (IsExplosive)
        {
            // The explosion kills this zombie too (it stands in its own blast) and
            // scores everything it kills together. The "blast" analytics event
            // is sent when the whole chain reaction is over (ExplosionChain).
            AnalyticsEvent report = Analytics.NewEvent("blast");
            report.Add("source", "red");
            report.Add("zombie_id", Id);
            report.Add("word_length", Word.Length);
            report.Add("frenzy_word", realWord != null);
            report.Add("blast_radius", BlastRadius);
            report.Add("in_ring_now", CountInRing());
            report.Add("in_ring_at_last_start", inRingAtLastStart);   // when the attempt that blew it up began
            report.Add("in_ring_at_first_letter", inRingAtFirstLetter); // when its word was first started
            report.Add("in_ring_max_while_typed", inRingMaxWhileTyped);
            report.Add("in_ring_max", inRingMax);
            report.Add("typing_starts", typingStarts);                // more than 1: started, dropped, started again
            report.Add("typing_seconds", GameManager.Instance.Stats.Seconds - lastStartPlayTime); // last start to blast
            report.Add("distance_to_player", FlatDistance(transform.position, player.position));
            Explosion.Detonate(transform.position, BlastRadius, null, BlastBossDamage, report, true); // its word was typed: the kills earn energy
            return;
        }

        KillByBullet();
    }

    // Killed by the player's bullet: counts for score, the body is knocked back.
    private void KillByBullet()
    {
        int points = GameManager.Instance.AddKill();
        spawner.Hud.ShowFloatingText(LabelAnchor, "+" + points, Color.white);

        // A SPLITTER splits in two (before it is removed, so the wave never looks empty):
        // its word breaks into two halves, each hidden in a splitling's new word.
        if (Kind == ZombieKind.Splitter && splitAt > 0)
        {
            string first = splitRaw.Substring(0, splitAt);
            string second = splitRaw.Substring(splitAt);
            spawner.SpawnSplitlings(transform.position, baseSpeed, splitRaw, first, second);
            spawner.Hud.ShowFloatingText(LabelAnchor + Vector3.up * 0.5f,
                "SPLIT!  " + first.ToLowerInvariant() + " + " + second.ToLowerInvariant(), Palette.WordSplitter, true);
        }

        Vector3 away = transform.position - player.position;
        away.y = 0f;
        KnockOver(away.normalized * 4f + Vector3.up * 2f);
        Remove("shot");
    }

    // Killed by an explosion centred at center. No score here: the explosion's
    // chain scores all its kills together. The body is thrown away from center.
    // cause (for analytics): "blast", or "boss_died" for the boss's minions.
    public void KillByBlast(Vector3 center, string cause)
    {
        if (!IsAlive)
        {
            return;
        }

        if (IsExplosive)
        {
            ring.PlayBlastEffect(); // its own ring grows and disappears
        }

        Vector3 away = transform.position - center;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = Random.insideUnitSphere;
            away.y = 0f;
        }
        KnockOver(away.normalized * 8f + Vector3.up * 6f);
        Remove(cause);
    }

    // The first word of an armored zombie: the armour breaks (it turns grey) and
    // it gets a second word.
    private void BreakArmor()
    {
        HasArmor = false;
        bodyRenderer.sharedMaterial = Palette.Lit(Palette.ZombieGrey);
        normalMaterial = bodyRenderer.sharedMaterial;
        spawner.Hud.ShowFloatingText(LabelAnchor, "ARMOR BROKEN", Palette.WordArmor, true);
        flinchTimer = flinchSeconds * 3f;

        // A new word whose first letter is not in use (ignoring our own old word).
        List<char> used = spawner.UsedFirstLetters();
        used.Remove(char.ToUpperInvariant(Word[0]));
        Word = TypingSettings.Style(WordBank.PickWord(used, ZombieKind.Armored), used);
        TypedCount = 0;
        Label.color = Color.white;

        // Broken during a FRENZY: the new word is its real word for later, and
        // it carries another short word for now.
        if (realWord != null)
        {
            realWord = Word;
            realColor = Color.white;
            Word = TypingSettings.StyleShort(WordBank.PickFrenzyWord(used));
            Label.color = Palette.WordFrenzy;
        }
        RefreshLabel();
    }

    // Reached the player while the SHIELD is up: no bite. It is thrown back a
    // few metres and staggers, and stays alive (it can still be shot for points).
    private void BounceOffShield()
    {
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = -player.forward;
        }
        MoveBy(away.normalized * ShieldBounceDistance);
        flinchTimer = ShieldStaggerSeconds;
        spawner.Hud.ShowFloatingText(LabelAnchor, "BLOCKED", Palette.Shield, true);
        GameManager.Instance.TakeDamage(BiteDamage, IsAmbusher, BiteEvent(), this);
    }

    private const float ShieldBounceDistance = 3.5f;  // metres it is thrown back by the shield
    private const float ShieldStaggerSeconds = 0.8f;  // then it moves slowly for this long

    // ---- GHOST ----
    // A long word, shown for ghostFirstLook seconds (longer for longer words),
    // then hidden: every letter not typed yet is drawn as a dim "_", on the
    // enemy AND in the bottom word box. It stays hidden while the player types
    // it (the typed letters show): that is the challenge, typing from memory.
    // Only while NOBODY is typing it does it flash back, for GhostFlash
    // seconds at the end of every GhostCycle. To make up for it, a ghost is
    // very slow and bites for half. It floats up and down a little.

    private const float GhostSpeedMultiplier = 0.45f;
    private const float GhostLookBase = 1f;          // seconds the word is shown at first...
    private const float GhostLookPerLetter = 0.18f;  // ...plus this per letter
    private const float GhostCycle = 7f;
    private const float GhostFlash = 1f;
    private const string GhostBlankColor = "<color=#FFFFFF66>"; // the "_" of a hidden letter
    private float ghostTimer;
    private float ghostFirstLook;
    private bool ghostRevealed;  // true while the whole word is shown

    // True while its word (the part not typed yet) is hidden as "_".
    private bool GhostHidesWord
    {
        get { return Kind == ZombieKind.Ghost && !ghostRevealed && realWord == null; }
    }

    private void UpdateGhost()
    {
        ghostTimer += Time.deltaTime;
        body.localPosition = new Vector3(0f, body.localScale.y + 0.15f + 0.12f * Mathf.Sin(ghostTimer * 2.5f), 0f);

        bool revealed;
        if (TypedCount > 0)
        {
            revealed = false; // being typed: from memory
        }
        else if (ghostTimer < ghostFirstLook)
        {
            revealed = true;  // the first look
        }
        else
        {
            revealed = (ghostTimer - ghostFirstLook) % GhostCycle >= GhostCycle - GhostFlash; // a flash at the end of each cycle
        }
        if (revealed != ghostRevealed)
        {
            ghostRevealed = revealed;
            RefreshLabel();
        }
    }

    // ---- SPLITTER / SPLITLING ----

    private string splitRaw;      // a splitter's word (UPPERCASE, unstyled)...
    private int splitAt;          // ...and where it breaks in two (0 = it does not)
    private string fragment;      // a splitling: the half of the splitter's word hidden in its word (UPPERCASE)

    // Called by WaveSpawner.SpawnSplitlings: this splitling's word hides
    // 'half' (one half of the splitter's word), drawn underlined and bold.
    public void MarkFragment(string half)
    {
        fragment = half;
        RefreshLabel();
    }

    // ---- SPITTER ----
    // Stops SpitRange metres from the player. Every SpitInterval seconds (the
    // first one sooner) it spits an acid glob: an EnergyOrb in acid green with
    // a short word, flying for SpitFlightSeconds. One glob in the air at a time.

    private const float SpitRange = 11f;
    private const float SpitInterval = 4.5f;
    private const float SpitFirstDelay = 1.2f;
    private const float SpitFlightSeconds = 6f;
    private const int SpitDamage = 5;
    private float spitTimer = SpitInterval - SpitFirstDelay;
    private EnergyOrb lastGlob;

    private void UpdateSpit()
    {
        if (lastGlob != null && lastGlob.IsAlive)
        {
            return; // wait until the last glob is popped or has hit
        }
        spitTimer += Time.deltaTime * Powers.EnemySpeedFactor;
        if (spitTimer < SpitInterval)
        {
            return;
        }
        spitTimer = 0f;

        string word = WordBank.PickOrbWord(spawner.UsedFirstLetters());
        Vector3 mouth = transform.position + Vector3.up * (headHeight * 0.85f);
        lastGlob = EnergyOrb.Launch(mouth, word, SpitFlightSeconds, SpitDamage, spawner.Hud, Palette.SpitterAcid, "spitter");
        spawner.AddStrayOrb(lastGlob);
        Flinch(transform.position - player.position); // a little recoil
    }

    // ---- SNIPER ----
    // Never moves (WaveSpawner puts it on a perch in the stands). It waits
    // for its interval, AIMS for SnipeAimSeconds (a red laser from its rifle to
    // the player, blinking faster and faster), then FIRES: an EnergyOrb with a
    // short word that reaches the player in flightSeconds, unless it is typed.
    // One bullet in the air at a time. WaveSpawner sets the pace per wave
    // (SetSniperPace): later waves shoot more often and faster.

    private const float SnipeAimSeconds = 1.4f;
    private const float SnipeFirstDelay = 2.5f;   // after it appears, the first aim starts this much sooner than a full interval
    private const int SnipeDamage = 8;
    private static readonly Color SnipeLaser = new Color(1f, 0.12f, 0.12f);
    private float snipeInterval = 7f;              // seconds from one shot (or its end) to the next aim
    private float snipeFlightSeconds = 4f;
    private float snipeTimer;
    private float snipeAim = -1f;                  // seconds into the aim; below 0 = not aiming
    private EnergyOrb lastBullet;
    private LineRenderer laser;

    // Called by WaveSpawner right after Setup.
    public void SetSniperPace(float interval, float flightSeconds)
    {
        snipeInterval = Mathf.Max(1f, interval);
        snipeFlightSeconds = Mathf.Max(1f, flightSeconds);
        snipeTimer = Mathf.Max(0f, snipeInterval - SnipeFirstDelay);
    }

    private Vector3 Muzzle
    {
        get { return transform.position + Vector3.up * (headHeight * 0.8f) + transform.forward * 0.45f; }
    }

    private void UpdateSnipe()
    {
        if (lastBullet != null && lastBullet.IsAlive)
        {
            ShowLaser(false);
            return; // wait until the last bullet is stopped or has hit
        }
        float step = Time.deltaTime * Powers.EnemySpeedFactor; // slower while the shield is up
        if (snipeAim < 0f)
        {
            snipeTimer += step;
            if (snipeTimer < snipeInterval)
            {
                ShowLaser(false);
                return;
            }
            snipeTimer = 0f;
            snipeAim = 0f; // start aiming
        }

        snipeAim += step;
        if (snipeAim < SnipeAimSeconds)
        {
            // Blinks faster as the shot gets closer (4 -> 16 blinks a second).
            float rate = Mathf.Lerp(4f, 16f, snipeAim / SnipeAimSeconds);
            ShowLaser(Mathf.Repeat(snipeAim * rate, 1f) < 0.6f);
            return;
        }

        snipeAim = -1f;
        ShowLaser(false);
        string word = WordBank.PickOrbWord(spawner.UsedFirstLetters());
        lastBullet = EnergyOrb.Launch(Muzzle, word, snipeFlightSeconds, SnipeDamage, spawner.Hud, Palette.SniperKhaki, "sniper");
        spawner.AddStrayOrb(lastBullet);
    }

    private void ShowLaser(bool on)
    {
        if (!on)
        {
            if (laser != null)
            {
                laser.enabled = false;
            }
            return;
        }
        if (laser == null)
        {
            GameObject beam = new GameObject("Sniper Laser");
            beam.transform.SetParent(transform, false);
            laser = beam.AddComponent<LineRenderer>();
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.startWidth = 0.06f;
            laser.endWidth = 0.03f;
            laser.sharedMaterial = Palette.Unlit(SnipeLaser);
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.receiveShadows = false;
        }
        laser.enabled = true;
        laser.SetPosition(0, Muzzle);
        laser.SetPosition(1, CameraDirector.PlayerEye + Vector3.down * 0.4f); // where its bullet flies (EnergyOrb.AimPoint)
    }

    // Text colour of the "WORD SHIFT!" popup (the shifter's purple, lighter).
    private static readonly Color ShifterColor = new Color(0.8f, 0.4f, 1f);

    // STAND-OFF: in first person a zombie chasing the player stops this far away
    // (big ones farther: their words sit higher) and attacks from there. In
    // the map view (seen from above) it may walk right up: attackDistance.
    private const float NearPlayerGap = 2.2f; // metres between two zombies' bodies when they stand at the player
    private const float StandOff = 3f;
    private const float BigStandOff = 4f;
    private const float BiteReach = 0.6f;    // it bites from up to this far beyond its stand-off

    private float StandOffDistance
    {
        get
        {
            // Map view and third person (Endless): the player's body is in the
            // picture, so a zombie must walk right up before it attacks.
            if (CameraDirector.MapBlend > 0.5f || CameraDirector.IsThirdPerson)
            {
                return attackDistance;
            }
            return (Kind == ZombieKind.Brute || Kind == ZombieKind.Explosive ? BigStandOff : StandOff) + standOffExtra;
        }
    }

    // ---- Its own way in (set by WaveSpawner.AssignApproach) ----
    // A wave's zombies do not all walk the straight line from their door: each
    // gets a BEARING (degrees left / right of the way the player faces) and
    // walks a curve toward it, so they arrive spread out in a fan in front of
    // the player, some a row further back (standOffExtra). Their words then
    // sit apart and stay still instead of piling up and shuffling.
    private float approachBearing = float.NaN; // NaN: straight at the player
    private float standOffExtra;

    public bool HasApproach
    {
        get { return !float.IsNaN(approachBearing); }
    }

    public float ApproachBearing
    {
        get { return approachBearing; }
    }

    public void SetApproach(float bearing, float extraStandOff)
    {
        approachBearing = bearing;
        standOffExtra = Mathf.Max(0f, extraStandOff);
    }

    private const float ApproachLookAhead = 3f;   // metres: it steers toward a point this much closer on its curve
    private const float ApproachMaxTurn = 22f;    // degrees of bearing it gains per look-ahead step

    // Where to walk now on the way to its spot: a point a little closer to the
    // player and turned a little toward its bearing (a gentle curve), then
    // the spot itself. 'stop' is how close to that point counts as there.
    private Vector3 ApproachGoal(out float stop)
    {
        Vector3 facing = spawner.CurrentEncounter != null ? spawner.CurrentEncounter.Facing : player.forward;
        facing.y = 0f;
        facing.Normalize();
        Vector3 fromPlayer = transform.position - player.position;
        fromPlayer.y = 0f;
        float distance = fromPlayer.magnitude;
        float standOff = StandOffDistance;
        Vector3 spot = player.position + Quaternion.Euler(0f, approachBearing, 0f) * facing * standOff;
        if (distance < 0.5f || FlatDistance(transform.position, spot) < ApproachLookAhead)
        {
            stop = 0.15f;
            return spot;
        }
        float current = Vector3.SignedAngle(facing, fromPlayer, Vector3.up);
        float turn = Mathf.Clamp(Mathf.DeltaAngle(current, approachBearing), -ApproachMaxTurn, ApproachMaxTurn);
        float radius = Mathf.Max(standOff, distance - ApproachLookAhead);
        stop = 0f;
        return player.position + Quaternion.Euler(0f, current + turn, 0f) * facing * radius;
    }

    private float BiteRange
    {
        get { return CameraDirector.MapBlend > 0.5f ? attackDistance : (CameraDirector.IsThirdPerson ? attackDistance + BiteReach : StandOffDistance + BiteReach); }
    }

    // At the player: it STAYS and keeps biting until it is killed or lured away,
    // with a breather between bites: the first bite comes FirstBiteDelay after
    // it arrives, then one every BaseBiteInterval seconds, faster or slower
    // with the player's typing speed (DynamicDifficulty.AttackRateFactor). The
    // timer only runs while it is free to bite (not frozen).
    private const float BaseBiteInterval = 2.5f;
    private const float FirstBiteDelay = 1f;
    private const float BiteRecoil = 0.25f;  // metres it rocks back after each bite (it then steps in again)
    private float biteTimer = -1f;           // below 0: it has not reached the player yet
    private int bites;

    private void Attack()
    {
        if (biteTimer < 0f)
        {
            biteTimer = FirstBiteDelay; // just arrived: a moment before the first bite
        }
        biteTimer -= Time.deltaTime * DynamicDifficulty.AttackRateFactor;
        if (biteTimer > 0f)
        {
            return;
        }
        biteTimer = BaseBiteInterval;
        bites += 1;
        GameManager.Instance.TakeDamage(BiteDamage, IsAmbusher, BiteEvent(), this);

        // A small rock back, so each bite reads as a hit.
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.0001f)
        {
            MoveBy(away.normalized * BiteRecoil);
        }

        // The bite that ended the run: this zombie logs its own "zombie" event
        // (GameManager leaves it out of the "run_over" ones).
        if (GameManager.Instance.State == GameState.Lost)
        {
            LogEnd("bite");
        }
    }

    private int BiteDamage
    {
        get
        {
            if (Kind == ZombieKind.Brute) return damagePerHit * 2;
            if (Kind == ZombieKind.Splitling || Kind == ZombieKind.Ghost) return Mathf.Max(1, damagePerHit / 2);
            return damagePerHit;
        }
    }

    private AnalyticsEvent BiteEvent()
    {
        AnalyticsEvent hit = Analytics.NewEvent("damage");
        hit.Add("source", "zombie_" + Kind.ToString().ToLowerInvariant());
        hit.Add("ambusher", IsAmbusher);
        hit.Add("attacker_id", Id);
        hit.Add("attacker_word", Word);
        hit.Add("attacker_word_length", Word.Length);
        hit.Add("attacker_letters_typed", TypedCount); // more than 0: the player was typing this very zombie
        hit.Add("attacker_seconds_alive", GameManager.Instance.Stats.Seconds - spawnPlayTime);
        if (IsAmbusher)
        {
            hit.Add("attacker_seconds_since_wake", GameManager.Instance.Stats.Seconds - wokePlayTime); // how long the player saw it
        }
        return hit;
    }

    // Lets go of the capsule and throws it: it tumbles, then sinks into the floor.
    private void KnockOver(Vector3 velocity)
    {
        Corpse.Launch(body.gameObject, velocity, Random.insideUnitSphere * 6f, 2f);
    }

    // Quietly takes it out of the game, no score and no bite: an ambush zombie
    // the player rode past (WaveSpawner).
    public void Despawn()
    {
        if (IsAlive)
        {
            Remove(IsLurking ? "never_seen" : "rode_past");
        }
    }

#if UNITY_EDITOR
    // Removes a zombie for developer testing without awarding score, kills, or energy.
    public void DespawnForDebug()
    {
        if (IsAlive)
        {
            Remove("debug_complete");
        }
    }
#endif

    // Takes this zombie out of the game. No score here: also used when it reaches the player.
    // outcome (for analytics): how it left: "shot", "blast", "bite", "rode_past",
    // "never_seen", "lost" or "boss_died" ("run_over" comes from LogRunOver).
    private void Remove(string outcome)
    {
        // Destroy() only really happens at the end of the frame, so other scripts
        // check IsAlive to know right away that this zombie is gone.
        IsAlive = false;
        spawner.RemoveZombie(this);
        Destroy(Label.gameObject); // the word is on the HUD, not a child of this zombie
        Destroy(gameObject);
        LogEnd(outcome);
    }

    // Called by GameManager when the run ends (death, win, Restart) while this
    // zombie is still around: its "zombie" event, outcome "run_over".
    public void LogRunOver()
    {
        LogEnd("run_over");
    }

    // The "zombie_spawn" analytics event: it appeared (its word is on screen).
    private void LogSpawn()
    {
        AnalyticsEvent e = Analytics.NewEvent("zombie_spawn");
        e.Add("zombie_id", Id);
        e.Add("kind", Kind.ToString().ToLowerInvariant());
        e.Add("word", Word);
        e.Add("word_length", Word.Length);
        e.Add("ambusher", IsAmbusher);
        e.Add("chain_linked", IsChainLinked);
        e.Add("frenzy_word", realWord != null);
        if (IsExplosive)
        {
            e.Add("blast_radius", BlastRadius);
        }
        AddPosition(e);
        Analytics.Send(e);
    }

    // Map position and distance to the player (analytics).
    private void AddPosition(AnalyticsEvent e)
    {
        e.Add("x", transform.position.x);
        e.Add("z", transform.position.z);
        e.Add("distance_to_player", FlatDistance(transform.position, player.position));
    }

    // The "zombie" analytics event: one per zombie, when it leaves the game
    // (or when the run ends, see LogRunOver).
    private void LogEnd(string outcome)
    {
        AnalyticsEvent e = Analytics.NewEvent("zombie");
        e.Add("zombie_id", Id);
        e.Add("outcome", outcome);
        e.Add("kind", Kind.ToString().ToLowerInvariant());
        e.Add("ambusher", IsAmbusher);
        e.Add("chain_linked", IsChainLinked);
        e.Add("word", Word);
        e.Add("word_length", Word.Length);
        e.Add("frenzy_word", realWord != null);
        e.Add("letters_typed", TypedCount);
        e.Add("was_targeted", firstTypedPlayTime >= 0f);
        e.Add("typing_starts", typingStarts);
        e.Add("bites", bites); // bites it gave while standing at the player
        e.Add("seconds_alive", GameManager.Instance.Stats.Seconds - spawnPlayTime); // an ambusher's includes its lurking
        if (IsAmbusher)
        {
            e.Add("seconds_since_wake", wokePlayTime >= 0f ? GameManager.Instance.Stats.Seconds - wokePlayTime : -1f);
        }
        if (Kind == ZombieKind.Shifter)
        {
            e.Add("word_shifts", wordShifts);
        }
        if (IsExplosive)
        {
            e.Add("blast_radius", BlastRadius);
            e.Add("in_ring_at_first_letter", inRingAtFirstLetter);
            e.Add("in_ring_at_last_start", inRingAtLastStart);
            e.Add("in_ring_max_while_typed", inRingMaxWhileTyped);
            e.Add("in_ring_max", inRingMax);
        }
        AddPosition(e);
        Analytics.Send(e);
    }

    // RED zombies: how many OTHER zombies stand inside the ring right now,
    // measured like Explosion does (flat distance from this zombie's feet).
    private int CountInRing()
    {
        int count = 0;
        foreach (Zombie other in spawner.AliveZombies)
        {
            if (other != this && other.IsAlive && FlatDistance(other.transform.position, transform.position) <= BlastRadius)
            {
                count += 1;
            }
        }
        return count;
    }

    // A WordBank word styled for the typing difficulty. Its first character
    // avoids the ones already on screen (this zombie's own word is not on the
    // spawner's list yet, or is about to be replaced).
    private string Styled(string word)
    {
        return TypingSettings.Style(word, spawner.UsedFirstLetters());
    }

    // Rebuilds the rich text: typed letters in yellow, the rest in the label's
    // colour. On Easy the word is stored UPPERCASE (typing compares in uppercase)
    // but SHOWN in lowercase; otherwise it is shown exactly as it must be typed.
    // A hidden GHOST word shows "_" for every letter not typed yet; a
    // SPLITLING underlines the half of the splitter's word it hides.
    private void RefreshLabel()
    {
        string shown = TypingSettings.Shown(Word, caseSensitive);
        string typedPart = shown.Substring(0, TypedCount);
        string remainingPart;
        if (GhostHidesWord)
        {
            remainingPart = GhostBlankColor + new string('_', shown.Length - TypedCount) + "</color>";
        }
        else
        {
            remainingPart = WithFragmentMarked(shown, TypedCount);
        }
        ColoredWord = TypedColorTag + typedPart + "</color>" + remainingPart;
        Label.text = ColoredWord;
    }

    // shown from index 'from' on, with the splitling's fragment (if it has one
    // and it is in this word, not a frenzy word) underlined and bold.
    private string WithFragmentMarked(string shown, int from)
    {
        if (string.IsNullOrEmpty(fragment) || realWord != null)
        {
            return shown.Substring(from);
        }
        int start = Word.ToUpperInvariant().IndexOf(fragment, System.StringComparison.Ordinal);
        if (start < 0)
        {
            return shown.Substring(from);
        }
        int end = start + fragment.Length;
        int markFrom = Mathf.Max(start, from);
        if (markFrom >= end)
        {
            return shown.Substring(from); // the fragment is already typed
        }
        return shown.Substring(from, markFrom - from)
            + "<u><b>" + shown.Substring(markFrom, end - markFrom) + "</b></u>"
            + shown.Substring(end);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void ApplySpeedMultiplier(float multiplier)
    {
        speed *= Mathf.Max(0f, multiplier);
    }
}
