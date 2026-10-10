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
//
// Where it walks: out of its doorway first (to the spawn point's exit), then
// straight at the player. If a lure bomb is on the ground nearby, it walks to
// the bomb instead and crowds around it. While the freeze power is on, it
// stands still and turns icy blue. Every bullet that hits it pushes it back a
// little. When it dies, the capsule is knocked over (see Corpse).
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
    Explosive
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

    // TextMeshPro rich-text colour for the letters typed so far (yellow).
    public const string TypedColorTag = "<color=#FFD21E>";

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
    private Transform player;
    private WaveSpawner spawner;
    private Transform body;         // the capsule
    private Renderer bodyRenderer;
    private Material normalMaterial; // the body's material when not frozen
    private float headHeight;       // metres from the feet to the top of the head

    // Where it walks first (out of its doorway), and whether it got there.
    private Vector3 exitPoint;
    private bool reachedExit;
    private float exitTimer;

    // How close it stands to a lure bomb (each zombie a bit different, so they crowd around it).
    private float crowdDistance;

    private float flinchTimer;
    private bool lookingFrozen;

    // Called by WaveSpawner right after it creates this zombie.
    //   exit: the point just outside its doorway; it walks there first.
    public void Setup(string word, float moveSpeed, Transform playerTransform, WaveSpawner owner,
        ZombieKind kind, Vector3 exit)
    {
        Word = word;
        player = playerTransform;
        spawner = owner;
        Kind = kind;
        exitPoint = exit;

        IsAlive = true;
        TypedCount = 0;
        IsExplosive = kind == ZombieKind.Explosive;
        HasArmor = kind == ZombieKind.Armored;
        crowdDistance = Random.Range(0.8f, 1.8f);

        // The body: the prefab's capsule (2 m tall, 1 m wide), sized and coloured by kind.
        body = transform.Find("Body");
        bodyRenderer = body.GetComponent<Renderer>();
        Vector3 size = Vector3.one;
        speed = moveSpeed;
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
        Label.color = HasArmor ? Palette.WordArmor : Color.white;
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
        Word = shortWord;
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
        IsLurking = false;
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

        // Freeze power: stand still, icy blue.
        SetFrozenLook(Powers.IsFrozen);
        if (Powers.IsFrozen)
        {
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

        float currentSpeed = speed;
        if (flinchTimer > 0f)
        {
            flinchTimer -= Time.deltaTime;
            currentSpeed *= flinchSlowdown;
        }
        transform.position += direction * currentSpeed * Time.deltaTime;

        // 3. Close enough to bite.
        if (chasingPlayer && FlatDistance(transform.position, player.position) <= attackDistance)
        {
            Attack();
            return;
        }

        // Safety net: wandered off somewhere it can never matter again.
        if (FlatDistance(transform.position, player.position) > LostDistance)
        {
            Remove();
        }
    }

    // Sum of "step away" directions from every zombie that is too close. Each
    // push is stronger the closer the other zombie is (0 at the wanted distance).
    private Vector3 SeparationPush()
    {
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
            float wanted = BodyRadius + other.BodyRadius + separationDistance;
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
            transform.position += push.normalized * 0.06f;
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
        get { return false; }
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
    }

    // Called when the player drops this zombie as their target.
    public void ResetProgress()
    {
        TypedCount = 0;
        RefreshLabel();
    }

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
            // scores everything it kills together.
            Explosion.Detonate(transform.position, BlastRadius, null, BlastBossDamage);
            return;
        }

        KillByBullet();
    }

    // Killed by the player's bullet: counts for score, the body is knocked back.
    private void KillByBullet()
    {
        int points = GameManager.Instance.AddKill();
        spawner.Hud.ShowFloatingText(LabelAnchor, "+" + points, Color.white);

        Vector3 away = transform.position - player.position;
        away.y = 0f;
        KnockOver(away.normalized * 4f + Vector3.up * 2f);
        Remove();
    }

    // Killed by an explosion centred at center. No score here: the explosion's
    // chain scores all its kills together. The body is thrown away from center.
    public void KillByBlast(Vector3 center)
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
        Remove();
    }

    // The first word of an armored zombie: the armour breaks (it turns grey) and
    // it gets a second word.
    private void BreakArmor()
    {
        HasArmor = false;
        bodyRenderer.sharedMaterial = Palette.Lit(Palette.ZombieGrey);
        normalMaterial = bodyRenderer.sharedMaterial;
        spawner.Hud.ShowFloatingText(LabelAnchor, "ARMOR BROKEN", Palette.WordArmor);
        flinchTimer = flinchSeconds * 3f;

        // A new word whose first letter is not in use (ignoring our own old word).
        List<char> used = spawner.UsedFirstLetters();
        used.Remove(Word[0]);
        Word = WordBank.PickWord(used, ZombieKind.Armored);
        TypedCount = 0;
        Label.color = Color.white;

        // Broken during a FRENZY: the new word is its real word for later, and
        // it carries another short word for now.
        if (realWord != null)
        {
            realWord = Word;
            realColor = Color.white;
            Word = WordBank.PickFrenzyWord(used);
            Label.color = Palette.WordFrenzy;
        }
        RefreshLabel();
    }

    // Reached the player: bites (damage) and is shoved away. No score.
    private void Attack()
    {
        GameManager.Instance.TakeDamage(damagePerHit, IsAmbusher); // an ambusher only costs one step of combo

        Vector3 away = transform.position - player.position;
        away.y = 0f;
        KnockOver(away.normalized * 4f + Vector3.up * 2f);
        Remove();
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
            Remove();
        }
    }

    // Takes this zombie out of the game. No score here: also used when it reaches the player.
    private void Remove()
    {
        // Destroy() only really happens at the end of the frame, so other scripts
        // check IsAlive to know right away that this zombie is gone.
        IsAlive = false;
        spawner.RemoveZombie(this);
        Destroy(Label.gameObject); // the word is on the HUD, not a child of this zombie
        Destroy(gameObject);
    }

    // Rebuilds the rich text: typed letters in yellow, remaining letters in white.
    // Word is stored UPPERCASE (typing compares in uppercase) but SHOWN in lowercase.
    private void RefreshLabel()
    {
        string shown = Word.ToLowerInvariant();
        string typedPart = shown.Substring(0, TypedCount);
        string remainingPart = shown.Substring(TypedCount);
        ColoredWord = TypedColorTag + typedPart + "</color>" + remainingPart;
        Label.text = ColoredWord;
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
