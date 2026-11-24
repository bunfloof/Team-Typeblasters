// Hydra.cs
// ---------------------------------------------------------------------------
// THE HYDRA: Endless's second boss (wave 15, then every 10 waves: 25, 35...).
//
// ITS NEW IDEA: CUT & SEAR. A kill that has to be CONFIRMED in time.
//   - It has three heads on long necks, each with a hard word. Typing a
//     head's word CUTS it off.
//   - The stump then burns with a short word ("sear", "burn", "ash"...) and a
//     countdown. Type it before the time runs out and the stump is SEALED for
//     good. Miss it and TWO heads grow back from the stump (up to MaxHeads).
//   - When every head is cut and every stump sealed, its body opens: one last
//     phrase on the body kills it.
//   - Meanwhile a head now and then REARS UP (its scanner burns white, it
//     lifts) and then STRIKES. Cutting that head while it rears stops the
//     strike: the player always knows which word to go for first.
//   FREEZE stops strikes and countdowns; the SHIELD blocks a strike.
//
// The look is the game's: beans with the slit visor and the red scanner,
// here a big one with heads on necks of linked capsules.
//
//   WaveSpawner.RunHydraFight   creates it and waits for it to die
//   AddTypeableParts            its words for the typing (heads, stumps, body)
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Hydra : MonoBehaviour
{
    public const string BossName = "THE HYDRA";
    private const int StartHeads = 3;
    private const int MaxHeads = 5;
    private const float SearSeconds = 6f;          // to type a stump's word before two heads grow back
    private const float AttackEvery = 6f;          // between strikes (less for a stronger hydra)
    private const float RearSeconds = 1.6f;        // the warning before a strike
    private const int StrikeDamage = 12;
    private const float NeckBase = 3.2f;           // where the necks leave the body (height)
    private const float HeadRadius = 5.2f;         // how far out the heads are
    private const float HeadHeight = 6.0f;

    private static readonly Color Skin = new Color(0.32f, 0.80f, 0.56f);      // emerald: none of the zombies' colours
    private static readonly Color SkinDark = new Color(0.16f, 0.42f, 0.30f);
    private static readonly Color VisorGlass = new Color(0.05f, 0.06f, 0.09f);
    private static readonly Color ScanRed = new Color(1.00f, 0.22f, 0.14f);
    private static readonly Color ScanHot = new Color(1.00f, 0.92f, 0.80f);
    private static readonly Color Fire = new Color(1.00f, 0.55f, 0.12f);
    private static readonly Color Char = new Color(0.10f, 0.09f, 0.09f);
    private static readonly string[] SearWords = { "SEAR", "BURN", "ASH", "CHAR", "EMBER", "FLAME", "SINGE", "BLAZE", "TORCH", "HEAT", "COAL", "FIRE" };

    private WaveSpawner spawner;
    private HUD hud;
    private Transform player;
    private Transform body;
    private float strength;
    private readonly List<Head> heads = new List<Head>();
    private readonly List<Stump> stumps = new List<Stump>();
    private Core core;
    private float nextAttack;
    private int worstHealth; // for the bar: the most "work" left it has ever had

    public bool IsAlive { get; private set; }

    public Vector3 BodyCenter
    {
        get { return body.position + Vector3.up * 2f; }
    }

    // level: 1 for the first hydra, 2 for the next (wave 25)... faster and tougher.
    public static Hydra Create(Vector3 stand, Vector3 playerPosition, WaveSpawner owner, HUD hudRef, Transform playerRef, int level)
    {
        GameObject root = new GameObject("The Hydra");
        Vector3 toPlayer = playerPosition - stand;
        toPlayer.y = 0f;
        root.transform.SetPositionAndRotation(stand, Quaternion.LookRotation(toPlayer.sqrMagnitude > 0.01f ? toPlayer : Vector3.forward));
        Hydra hydra = root.AddComponent<Hydra>();
        hydra.spawner = owner;
        hydra.hud = hudRef;
        hydra.player = playerRef;
        hydra.strength = 1f + 0.35f * Mathf.Max(0, level - 1);
        hydra.Build();
        return hydra;
    }

    // ---- Building ----

    private void Build()
    {
        IsAlive = true;
        body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        Shapes.Block(PrimitiveType.Capsule, "Core", body, new Vector3(0f, 2.6f, 0f), new Vector3(3.2f, 2.6f, 3.2f), Palette.Lit(Skin));
        // Its big visor and scanner, like every enemy's, across the body.
        Shapes.Block(PrimitiveType.Capsule, "Visor", body, new Vector3(0f, 3.3f, 1.25f), new Vector3(0.55f, 1.45f, 0.6f),
            Palette.Lit(VisorGlass), new Vector3(0f, 0f, 90f));
        Shapes.Block(PrimitiveType.Cube, "Scanner", body, new Vector3(0f, 3.3f, 1.62f), new Vector3(0.6f, 0.2f, 0.05f), Palette.Unlit(ScanRed));
        for (int side = -1; side <= 1; side += 2)
        {
            Shapes.Block(PrimitiveType.Cylinder, "Foot", body, new Vector3(side * 0.75f, 0.08f, 0.35f), new Vector3(1.1f, 0.08f, 1.4f), Palette.Lit(SkinDark));
        }

        float[] angles = { -55f, 0f, 55f };
        for (int i = 0; i < StartHeads; i++)
        {
            heads.Add(new Head(this, angles[i]));
        }
        nextAttack = Time.time + AttackEvery * 0.8f;
        Almanac.Discover("hydra");
        UpdateBar();
    }

    private Vector3 NeckRoot(float angle)
    {
        return transform.TransformPoint(Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, NeckBase, 0.6f));
    }

    private Vector3 HeadRest(float angle)
    {
        // The middle heads stand taller than the outer ones, so their words never sit on top of each other.
        float height = HeadHeight + 2.2f * (1f - Mathf.Clamp01(Mathf.Abs(angle) / 75f));
        return transform.TransformPoint(Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, height, HeadRadius));
    }

    // ---- For the typing ----

    public void AddTypeableParts(List<ITypingTarget> targets)
    {
        foreach (Head head in heads)
        {
            if (head.IsAlive) targets.Add(head);
        }
        foreach (Stump stump in stumps)
        {
            if (stump.IsAlive) targets.Add(stump);
        }
        if (core != null && core.IsAlive)
        {
            targets.Add(core);
        }
    }

    private string NewHeadWord()
    {
        List<char> used = spawner.UsedFirstLetters();
        return TypingSettings.Style(WordBank.PickBossWord(used), used);
    }

    private string NewSearWord()
    {
        List<char> used = spawner.UsedFirstLetters();
        List<string> free = new List<string>();
        foreach (string word in SearWords)
        {
            if (!used.Contains(word[0])) free.Add(word);
        }
        string pick = free.Count > 0 ? free[Random.Range(0, free.Count)] : SearWords[Random.Range(0, SearWords.Length)];
        return TypingSettings.StyleShort(pick); // short and quick, never symbols
    }

    // ---- Every frame ----

    private void Update()
    {
        if (!IsAlive || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }
        bool frozen = Powers.IsFrozen;
        float time = Time.time;
        foreach (Head head in heads) head.Tick(frozen);
        for (int i = stumps.Count - 1; i >= 0; i--)
        {
            stumps[i].Tick(frozen);
        }
        heads.RemoveAll(head => head.Gone);
        stumps.RemoveAll(stump => stump.Gone);

        // A strike now and then, from a head that is not already rearing.
        if (frozen)
        {
            nextAttack += Time.deltaTime;
        }
        else if (time >= nextAttack && heads.Count > 0)
        {
            Head striker = heads[Random.Range(0, heads.Count)];
            if (striker.IsAlive && !striker.Rearing)
            {
                striker.Rear();
            }
            nextAttack = time + AttackEvery / strength * Random.Range(0.8f, 1.2f);
        }

        // Every head cut and every stump sealed: the body opens.
        if (core == null && heads.Count == 0 && stumps.Count == 0)
        {
            core = new Core(this);
            hud.ShowFloatingText(BodyCenter + Vector3.up * 3f, "ITS BODY OPENS!", Fire, true);
        }
        UpdateBar();
    }

    // Work left: every head (cut + sear), every burning stump (sear), the body.
    private void UpdateBar()
    {
        int left = heads.Count * 2 + stumps.Count + 1;
        worstHealth = Mathf.Max(worstHealth, left);
        hud.SetBossHealth(core != null ? 1 : left, worstHealth);
    }

    private void HeadCut(Head head)
    {
        CameraDirector.Shake(0.25f);
        stumps.Add(new Stump(this, head.Angle));
        hud.ShowFloatingText(head.Position + Vector3.up, "CUT!  NOW SEAR IT", Fire, true);
    }

    private void StumpSealed(Stump stump)
    {
        hud.ShowFloatingText(stump.Position + Vector3.up, "SEALED", new Color(0.9f, 0.9f, 0.9f), true);
        GameManager.Instance.AddKill();
    }

    // A stump that was not seared in time: two heads (one if it is full).
    private void StumpRegrows(Stump stump)
    {
        CameraDirector.Shake(0.3f);
        hud.ShowFloatingText(stump.Position + Vector3.up * 1.5f, "IT GROWS BACK!", ScanRed, true);
        int room = MaxHeads - heads.Count;
        if (room >= 2)
        {
            heads.Add(new Head(this, Mathf.Clamp(stump.Angle - 24f, -75f, 75f)));
            heads.Add(new Head(this, Mathf.Clamp(stump.Angle + 24f, -75f, 75f)));
        }
        else if (room == 1)
        {
            heads.Add(new Head(this, stump.Angle));
        }
        else
        {
            heads.Add(new Head(this, stump.Angle)); // full: at least the head comes back
        }
    }

    private void Strike(Head head)
    {
        AnalyticsEvent hit = Analytics.NewEvent("damage");
        hit.Add("source", "hydra_strike");
        GameManager.Instance.TakeDamage(Mathf.RoundToInt(StrikeDamage * strength), false, hit, null);
        CameraDirector.Shake(0.35f);
    }

    private void CoreTyped()
    {
        IsAlive = false;
        GameManager.Instance.AddKill();
        Sfx.PlayBossDefeated(0.1f);
        hud.ShowFloatingText(BodyCenter + Vector3.up * 3f, "THE HYDRA FALLS!", Fire, true);
        StartCoroutine(Sink());
    }

    private System.Collections.IEnumerator Sink()
    {
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            transform.position += Vector3.down * Time.deltaTime * 3f;
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        foreach (Head head in heads) head.Remove();
        foreach (Stump stump in stumps) stump.Remove();
        if (core != null) core.Remove();
    }

    // ======================================================================
    // A typeable word with a label (shared by heads, stumps and the body)
    // ======================================================================

    private abstract class Part : ITypingTarget
    {
        protected readonly Hydra hydra;
        private readonly bool caseSensitive = TypingSettings.IsCaseSensitive;

        public TMP_Text Label { get; private set; }
        public string Word { get; private set; }
        public string ColoredWord { get; private set; }
        public int TypedCount { get; private set; }
        public bool Done { get; protected set; }

        protected Part(Hydra owner, string word, Color color)
        {
            hydra = owner;
            Label = owner.hud.CreateWordLabel(new Vector2(0.5f, 0f));
            Label.color = color;
            Label.fontStyle |= FontStyles.Bold;
            Word = word;
            Refresh();
        }

        public virtual bool IsAlive { get { return hydra.IsAlive && !Done; } }
        public bool IsTargeted { get { return TypedCount > 0; } }
        public bool IsCaseSensitive { get { return caseSensitive; } }
        public char NextLetter { get { return Word[TypedCount]; } }
        public bool IsWordComplete { get { return TypedCount >= Word.Length; } }
        public abstract Vector3 Position { get; }
        public virtual Vector3 HitPoint { get { return Position; } }
        public abstract Vector3 LabelAnchor { get; }

        public void AdvanceProgress() { TypedCount += 1; Refresh(); }
        public void ResetProgress() { TypedCount = 0; Refresh(); }

        public void CompleteWord()
        {
            if (Done) return;
            Done = true;
            TypedCount = 0;
            if (Label != null) Label.gameObject.SetActive(false);
            OnTyped();
        }

        protected abstract void OnTyped();

        private void Refresh()
        {
            string shown = TypingSettings.Shown(Word, caseSensitive);
            ColoredWord = Zombie.TypedColorTag + shown.Substring(0, TypedCount) + "</color>" + shown.Substring(TypedCount);
            Label.text = ColoredWord;
        }

        public virtual void Remove()
        {
            if (Label != null) Object.Destroy(Label.gameObject);
        }
    }

    // ---- A head on its neck ----

    private class Head : Part
    {
        public readonly float Angle;
        private readonly Transform head, scanner;
        private readonly Renderer scannerRenderer;
        private readonly List<Transform> neck = new List<Transform>();
        private float bob, rearTimer, fallTimer = -1f;
        private Vector3 lunge;
        public bool Rearing { get { return rearTimer > 0f; } }
        public bool Gone { get; private set; }

        public Head(Hydra owner, float angle) : base(owner, owner.NewHeadWord(), Palette.WordBoss)
        {
            Angle = angle;
            bob = Random.Range(0f, 6.28f);
            Material skin = Palette.Lit(Skin);
            for (int i = 0; i < 5; i++)
            {
                neck.Add(Shapes.Block(PrimitiveType.Capsule,
                    "Neck", owner.transform, Vector3.zero, new Vector3(0.75f - i * 0.06f, 0.5f, 0.75f - i * 0.06f), skin).transform);
            }
            head = new GameObject("Head").transform;
            head.SetParent(owner.transform, false);
            Shapes.Block(PrimitiveType.Capsule, "Skull", head, Vector3.zero, new Vector3(1.3f, 0.95f, 1.3f), skin);
            Shapes.Block(PrimitiveType.Capsule, "Visor", head, new Vector3(0f, 0.25f, 0.5f), new Vector3(0.28f, 0.6f, 0.32f),
                Palette.Lit(VisorGlass), new Vector3(0f, 0f, 90f));
            GameObject light = Shapes.Block(PrimitiveType.Cube, "Scanner", head, new Vector3(0f, 0.25f, 0.68f), new Vector3(0.26f, 0.1f, 0.03f), Palette.Unlit(ScanRed));
            scanner = light.transform;
            scannerRenderer = light.GetComponent<Renderer>();
            head.position = owner.HeadRest(angle) + Vector3.down * 2f; // grows up into place
        }

        public override Vector3 Position { get { return head.position; } }
        public override Vector3 LabelAnchor { get { return head.position + Vector3.up * 1.4f; } }

        public void Rear()
        {
            rearTimer = RearSeconds;
            scannerRenderer.sharedMaterial = Palette.Unlit(ScanHot);
        }

        public void Tick(bool frozen)
        {
            if (fallTimer >= 0f)
            {
                // Cut: it drops and shrinks away.
                fallTimer += Time.deltaTime;
                head.position += Vector3.down * Time.deltaTime * 9f * fallTimer;
                head.localScale = Vector3.one * Mathf.Max(0.01f, 1f - fallTimer);
                if (fallTimer > 1f)
                {
                    Remove();
                    Gone = true;
                }
                return;
            }
            if (!frozen)
            {
                bob += Time.deltaTime * 1.6f;
            }
            Vector3 rest = hydra.HeadRest(Angle) + Vector3.up * Mathf.Sin(bob) * 0.35f;
            if (Rearing && !frozen)
            {
                rearTimer -= Time.deltaTime;
                if (rearTimer <= 0f)
                {
                    lunge = (hydra.player.position - rest).normalized * 4f;   // the strike: a snap forward
                    hydra.Strike(this);
                    scannerRenderer.sharedMaterial = Palette.Unlit(ScanRed);
                }
            }
            lunge = Vector3.MoveTowards(lunge, Vector3.zero, Time.deltaTime * 10f);
            Vector3 lift = Rearing ? Vector3.up * (1f - rearTimer / RearSeconds) * 1.8f - (hydra.player.position - rest).normalized * 0.8f : Vector3.zero;
            Vector3 target = rest + lift + lunge;
            head.position = Vector3.Lerp(head.position, target, Time.deltaTime * 6f);
            Vector3 look = hydra.player.position - head.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) head.rotation = Quaternion.LookRotation(look);
            if (Rearing)
            {
                scanner.localScale = new Vector3(0.26f * (1.4f + 0.4f * Mathf.Sin(Time.time * 30f)), 0.12f, 0.03f);
            }
            else
            {
                scanner.localScale = new Vector3(0.26f, 0.1f, 0.03f);
            }
            LayNeck(hydra.NeckRoot(Angle), head.position + Vector3.down * 0.4f);
        }

        // The neck: capsules along a curve from the body to the head.
        private void LayNeck(Vector3 from, Vector3 to)
        {
            Vector3 control = (from + to) * 0.5f + Vector3.up * 1.5f;
            for (int i = 0; i < neck.Count; i++)
            {
                float t = (i + 0.5f) / neck.Count;
                Vector3 a = Bezier(from, control, to, t - 0.5f / neck.Count);
                Vector3 b = Bezier(from, control, to, t + 0.5f / neck.Count);
                neck[i].position = (a + b) * 0.5f;
                Vector3 along = b - a;
                if (along.sqrMagnitude > 0.0001f)
                {
                    neck[i].rotation = Quaternion.FromToRotation(Vector3.up, along.normalized);
                }
                Vector3 size = neck[i].localScale;
                neck[i].localScale = new Vector3(size.x, along.magnitude * 0.62f, size.z);
            }
        }

        private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return Vector3.Lerp(Vector3.Lerp(a, c, t), Vector3.Lerp(c, b, t), t);
        }

        protected override void OnTyped()
        {
            rearTimer = 0f;          // cut while rearing: the strike never comes
            fallTimer = 0f;
            foreach (Transform piece in neck) if (piece != null) Object.Destroy(piece.gameObject);
            neck.Clear();
            hydra.HeadCut(this);
        }

        public override void Remove()
        {
            base.Remove();
            if (head != null) Object.Destroy(head.gameObject);
            foreach (Transform piece in neck) if (piece != null) Object.Destroy(piece.gameObject);
            neck.Clear();
        }
    }

    // ---- A burning stump: sear it in time ----

    private class Stump : Part
    {
        public readonly float Angle;
        private readonly Transform stump;
        private readonly Renderer flame;
        private float timeLeft = SearSeconds;
        public bool Gone { get; private set; }

        public Stump(Hydra owner, float angle) : base(owner, owner.NewSearWord(), Fire)
        {
            Angle = angle;
            Vector3 root = owner.NeckRoot(angle);
            Vector3 outward = (root - owner.transform.position);
            outward.y = 0f;
            stump = Shapes.Block(PrimitiveType.Capsule, "Stump", owner.transform, Vector3.zero, new Vector3(0.8f, 0.5f, 0.8f), Palette.Lit(Skin)).transform;
            stump.position = root + outward.normalized * 0.3f + Vector3.up * 0.3f;
            flame = Shapes.Block(PrimitiveType.Cylinder, "Flame", stump, new Vector3(0f, 1.1f, 0f), new Vector3(0.9f, 0.25f, 0.9f), Palette.Unlit(Fire)).GetComponent<Renderer>();
        }

        public override Vector3 Position { get { return stump.position; } }
        public override Vector3 LabelAnchor { get { return stump.position + Vector3.up * 1.3f; } }

        public void Tick(bool frozen)
        {
            if (Done || Gone)
            {
                return;
            }
            if (!frozen && !IsTargeted)
            {
                timeLeft -= Time.deltaTime; // the clock waits while its word is being typed: no cruel last-letter misses
            }
            // The flame flickers faster as time runs out.
            float urgency = 1f - timeLeft / SearSeconds;
            flame.enabled = Mathf.Repeat(Time.time * (2f + urgency * 12f), 1f) < 0.65f;
            flame.transform.localScale = new Vector3(0.9f, 0.25f + urgency * 0.5f, 0.9f);
            if (timeLeft <= 0f)
            {
                Done = true;
                Gone = true;
                Remove();
                hydra.StumpRegrows(this);
            }
        }

        protected override void OnTyped()
        {
            flame.enabled = false;
            stump.GetComponent<Renderer>().sharedMaterial = Palette.Lit(Char); // charred shut
            Gone = true;
            hydra.StumpSealed(this);
            if (Label != null) Object.Destroy(Label.gameObject); // the charred stump stays; its word is gone
        }

        public override void Remove()
        {
            base.Remove();
            if (stump != null) Object.Destroy(stump.gameObject);
        }
    }

    // ---- The body, once it opens ----

    private class Core : Part
    {
        public Core(Hydra owner) : base(owner, TypingSettings.Style(WordBank.PickPhrase(owner.spawner.UsedFirstLetters()), owner.spawner.UsedFirstLetters()), Color.white)
        {
        }

        public override Vector3 Position { get { return hydra.BodyCenter; } }
        public override Vector3 LabelAnchor { get { return hydra.body.position + Vector3.up * 5.6f; } }

        protected override void OnTyped()
        {
            hydra.CoreTyped();
        }
    }
}
