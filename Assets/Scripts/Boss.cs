// Boss.cs
// ---------------------------------------------------------------------------
// The boss of the last area (see Level and WaveSpawner), made by Boss.Create:
// a big grey figure with a torso, a head, 2 arms and 2 legs. The torso, arms
// and legs are capsules, the same shape as a zombie's body; the head is a
// sphere. Arms and legs hang from a shoulder / hip "pivot", so they can swing
// (the attack animations).
//
// WORDS - 9 at the start:
//   - HEAD: one hard word (WordBank.PickBossWord). Typing it deals headDamage
//     and the head gets a NEW word at once: the head never falls off.
//   - ARMS and LEGS: two long words each. Every word typed deals
//     limbWordDamage; when both words of a limb are typed, the limb FALLS OFF
//     (limbSeverDamage more).
//   - QUIZ: when all 4 limbs are gone the boss is stunned and asks a question
//     (QuizBank) with 3 answers floating in front of it (QuizAnswer). Type the
//     RIGHT answer: critical hit (quizCorrectDamage). A WRONG answer heals it
//     (quizWrongHeal) and it fires an orb at once. Too slow: nothing happens.
//     Then the limbs grow back with new words after regrowSeconds.
//
// DAMAGE counts, not words: the boss dies as soon as its health reaches 0,
// however many words are left. Explosions hurt it too (TakeBlastDamage):
// barrels, lure bombs, and the RED zombies it summons, blown up next to it.
// The combo weapons hurt it less: words typed during a FRENZY deal only
// FrenzyWordDamageScale of their damage, and the RPG's blast is weaker on the
// boss (Bullet.RocketBossDamage).
//
// ATTACKS - one every attackInterval seconds (angryAttackInterval below half
// health). Each has a wind-up of attackWarningSeconds, while the body turns
// red, so the player sees it coming:
//   - THROW:  raises an arm, an energy orb charges in its hand, then it throws
//             the orb (EnergyOrb: type its case-sensitive word to shoot it
//             down, or take attackDamage when it arrives).
//   - VOLLEY: (only when angry) raises both arms and fires 3 orbs at once.
//   - STOMP:  lifts a leg and slams it down: a shockwave, the camera shakes,
//             and summonCount zombies crawl out next to it (the first one red).
//   Every third attack is a stomp; when angry, the second of three is a volley.
//   Without arms it charges orbs in its chest; without legs it cannot stomp.
//   The freeze power stops its attacks (and the quiz timer).
//
// Health 0 = boss dies (it shrinks into the ground). WaveSpawner waits for that.
// The red health bar at the top of the screen is drawn by HUD (ShowBossBar).
// The words are HUD texts too, placed by WaveSpawner.LayoutLabels.
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Boss : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 1000f;
    [SerializeField] private float headDamage = 70f;            // each word typed on the head
    [SerializeField] private float limbWordDamage = 50f;        // each word typed on an arm or a leg
    [SerializeField] private float limbSeverDamage = 60f;       // extra when a limb falls off
    // During a FRENZY the boss's words are tiny and fast to type, so each one
    // hurts it less.
    private const float FrenzyWordDamageScale = 0.4f;
    [SerializeField] private float regrowSeconds = 1.5f;        // pause before the limbs grow back

    [Header("Quiz")]
    [SerializeField] private float quizSeconds = 10f;           // time to type an answer
    [SerializeField] private float quizCorrectDamage = 400f;    // critical hit for the right answer
    [SerializeField] private float quizWrongHeal = 150f;        // the boss heals this much on a wrong answer

    [Header("Attack")]
    [SerializeField] private float attackInterval = 10f;        // seconds between attacks (0 = never attacks); each also has its wind-up
    [SerializeField] private float angryAttackInterval = 7f;    // below half health
    [SerializeField] private int attackDamage = 15;             // damage if an orb reaches the player
    [SerializeField] private float attackWarningSeconds = 2.5f; // the wind-up: the body turns red, the arm rises...
    [SerializeField] private float orbFlightSeconds = 5f;       // time the player has to type an orb's word
    [SerializeField] private float volleyExtraSeconds = 3.5f;   // a volley's orbs fly slower (3 words to type)
    [SerializeField] private int summonCount = 2;               // zombies summoned by a stomp (the first one is red)

    [Header("Look")]
    [SerializeField] private Color bodyColor = new Color(0.55f, 0.58f, 0.62f);   // normal colour (grey)
    [SerializeField] private Color attackColor = new Color(0.9f, 0.08f, 0.08f);  // colour at the end of the wind-up
    [SerializeField] private Color hitColor = Color.white;                       // flash when a word hits a part
    [SerializeField] private float shakeSeconds = 0.35f;
    [SerializeField] private float shakeStrength = 0.25f;       // metres

    private enum Attack
    {
        Throw,
        Volley,
        Stomp
    }

    // Arm raise angles (degrees around the shoulder, + = forward and up).
    private const float ArmRaised = 165f;     // overhead, ready to throw
    private const float ArmThrown = 60f;      // pointing at the player after the throw
    private const float VolleyRaised = 140f;
    private const float LegLifted = 50f;      // a leg lifted forward before the stomp
    private const float ChargeSize = 0.9f;    // diameter of an orb charging in a hand (the same as an EnergyOrb)
    private static readonly Color ChargeColor = new Color(1f, 0.15f, 0.55f); // the energy orb's hot pink

    public bool IsAlive { get; private set; }
    public float Health { get; private set; }

    private readonly List<BossLimb> limbs = new List<BossLimb>();         // head first, then arms and legs
    private readonly List<BossPart> words = new List<BossPart>();         // every word slot on the body (9)
    private readonly List<EnergyOrb> orbs = new List<EnergyOrb>();        // orbs fired and still in the air
    private readonly List<QuizAnswer> answers = new List<QuizAnswer>();   // the quiz answers on screen
    private readonly List<GameObject> charges = new List<GameObject>();   // orbs charging in a hand / the chest
    private BossLimb head, leftArm, rightArm, leftLeg, rightLeg;
    private Renderer torso;
    private HUD hud;
    private WaveSpawner spawner;
    private Vector3 homePosition;  // where the boss stands (shaking moves it around this point)
    private float attackTimer;
    private int attackCount;
    private bool attacking;        // an attack (wind-up, release, recovery) is playing
    private float windUp;          // 0..1 during a wind-up: the body turns red
    private float shakeTimer;
    private bool regrowing;
    private bool stunned;          // the quiz is on
    private bool angry;

    // The quiz answer the player chose (set by OnQuizAnswer while the quiz is open).
    private bool quizOpen;
    private QuizAnswer chosenAnswer;

    // Builds the boss standing at stand (on the ground), facing the player at playerPosition.
    public static Boss Create(Vector3 stand, Vector3 playerPosition, WaveSpawner owner, HUD hud)
    {
        GameObject root = new GameObject("Boss");
        root.transform.position = stand;

        // The boss's local -Z must face the player (the parts and their words
        // are laid out that way): its forward (+Z) points AWAY from the player.
        Vector3 away = stand - playerPosition;
        away.y = 0f;
        if (away.sqrMagnitude > 0.001f)
        {
            root.transform.rotation = Quaternion.LookRotation(away);
        }

        Boss boss = root.AddComponent<Boss>();
        boss.spawner = owner;
        boss.Build(hud);
        return boss;
    }

    private void Build(HUD hudRef)
    {
        hud = hudRef;
        homePosition = transform.position;
        Health = maxHealth;
        IsAlive = true;

        // Body layout, in metres, relative to the boss's feet. -X is the player's
        // left and -Z is the side facing the player.
        // A Unity capsule is 1 m wide and 2 m tall at scale 1, centred on its middle.
        torso = CreateBlock("Torso", PrimitiveType.Capsule, transform, new Vector3(0f, 4f, 0f), new Vector3(2.4f, 1.5f, 1.4f));

        // Each word's pivot says which side of its anchor point it sits on:
        // the head's word is centred above it, left-side words end at their
        // anchor (pivot on the right), right-side words start at it.
        Vector2 above = new Vector2(0.5f, 0f);
        Vector2 toTheLeft = new Vector2(1f, 0.5f);
        Vector2 toTheRight = new Vector2(0f, 0.5f);

        // name, shape, pivot (shoulder / hip), block under the pivot, block size, tip under the pivot
        head = AddLimb("Head", PrimitiveType.Sphere, true, new Vector3(0f, 6.3f, 0f), Vector3.zero, Vector3.one * 1.6f, Vector3.zero);
        leftArm = AddLimb("LeftArm", PrimitiveType.Capsule, false, new Vector3(-1.75f, 5.3f, 0f), new Vector3(0f, -1.3f, 0f),
            new Vector3(0.8f, 1.4f, 0.8f), new Vector3(0f, -2.6f, 0f));
        rightArm = AddLimb("RightArm", PrimitiveType.Capsule, false, new Vector3(1.75f, 5.3f, 0f), new Vector3(0f, -1.3f, 0f),
            new Vector3(0.8f, 1.4f, 0.8f), new Vector3(0f, -2.6f, 0f));
        leftLeg = AddLimb("LeftLeg", PrimitiveType.Capsule, false, new Vector3(-0.6f, 2.6f, 0f), new Vector3(0f, -1.3f, 0f),
            new Vector3(0.9f, 1.3f, 0.9f), new Vector3(0f, -2.6f, 0f));
        rightLeg = AddLimb("RightLeg", PrimitiveType.Capsule, false, new Vector3(0.6f, 2.6f, 0f), new Vector3(0f, -1.3f, 0f),
            new Vector3(0.9f, 1.3f, 0.9f), new Vector3(0f, -2.6f, 0f));

        // The 9 word slots: 1 on the head, 2 on every limb (one above the other).
        AddWord(head, new Vector3(0f, 7.4f, -0.8f), above);
        AddWord(leftArm, new Vector3(-2.35f, 4.8f, -0.8f), toTheLeft);
        AddWord(leftArm, new Vector3(-2.35f, 3.6f, -0.8f), toTheLeft);
        AddWord(rightArm, new Vector3(2.35f, 4.8f, -0.8f), toTheRight);
        AddWord(rightArm, new Vector3(2.35f, 3.6f, -0.8f), toTheRight);
        AddWord(leftLeg, new Vector3(-1.2f, 1.8f, -0.8f), toTheLeft);
        AddWord(leftLeg, new Vector3(-1.2f, 0.7f, -0.8f), toTheLeft);
        AddWord(rightLeg, new Vector3(1.2f, 1.8f, -0.8f), toTheRight);
        AddWord(rightLeg, new Vector3(1.2f, 0.7f, -0.8f), toTheRight);

        foreach (BossPart word in words)
        {
            GiveNewWord(word);
        }
        hud.SetBossHealth(Health, maxHealth);
        Tutorial.Once("boss", "BOSS! Every word hurts it. Lure bombs, barrels and RED zombies blown up next to it hurt it too!", 7f);
    }

    // Every block gets its OWN copy of a lit material, because its colour
    // changes on its own (red wind-up, hit flash, frozen).
    private Renderer CreateBlock(string blockName, PrimitiveType shape, Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        GameObject block = Shapes.Block(shape, blockName, parent, localPosition, localScale, Palette.Lit(bodyColor));
        Renderer blockRenderer = block.GetComponent<Renderer>();
        blockRenderer.material = new Material(Palette.Lit(bodyColor));
        return blockRenderer;
    }

    private BossLimb AddLimb(string limbName, PrimitiveType shape, bool isHead, Vector3 pivotPosition,
        Vector3 blockUnderPivot, Vector3 blockSize, Vector3 tipUnderPivot)
    {
        Transform pivot = new GameObject(limbName + "Pivot").transform;
        pivot.SetParent(transform, false);
        pivot.localPosition = pivotPosition;

        BossLimb limb = new BossLimb();
        limb.Name = limbName;
        limb.IsHead = isHead;
        limb.Pivot = pivot;
        limb.Block = CreateBlock(limbName, shape, pivot, blockUnderPivot, blockSize);
        limb.BlockPosition = blockUnderPivot;
        limb.BlockSize = blockSize;
        limb.TipUnderPivot = tipUnderPivot;
        limbs.Add(limb);
        return limb;
    }

    private void AddWord(BossLimb limb, Vector3 labelLocalPosition, Vector2 labelPivot)
    {
        TMP_Text label = hud.CreateWordLabel(labelPivot);
        BossPart word = new BossPart(this, limb, label, labelLocalPosition);
        limb.Words.Add(word);
        words.Add(word);
    }

    // Gives a word slot a new word: a hard boss word on the head, a long word
    // on a limb. Its first letter differs from everything else on screen.
    private void GiveNewWord(BossPart slot)
    {
        List<char> used = spawner.UsedFirstLetters();
        foreach (BossPart other in words) // the boss's own words (it may not be on the spawner's list yet)
        {
            if (other != slot && !other.IsCleared)
            {
                used.Add(char.ToUpperInvariant(other.Word[0]));
            }
        }
        string word = slot.Limb.IsHead ? WordBank.PickBossWord(used) : WordBank.PickWord(used, ZombieKind.Explosive);
        slot.SetWord(word);

        AnalyticsEvent e = Analytics.NewEvent("boss_word");
        e.Add("part", slot.Limb.IsHead ? "head" : slot.Limb.Name.Contains("Arm") ? "arm" : "leg");
        e.Add("word", word);
        Analytics.Send(e);

        // During a FRENZY a new word (the head's next one, a limb growing back)
        // is short too; its real word comes back when the frenzy ends.
        if (Powers.IsFrenzy)
        {
            used.Add(char.ToUpperInvariant(word[0]));
            slot.EnterFrenzy(WordBank.PickFrenzyWord(used));
        }
    }

    // ---- FRENZY (a combo weapon, see Powers) ----

    // Every word on the body becomes a short word (first letters not in
    // 'used', which grows as they are picked).
    public void EnterFrenzy(List<char> used)
    {
        foreach (BossPart slot in words)
        {
            if (slot.IsAlive)
            {
                string shortWord = WordBank.PickFrenzyWord(used);
                used.Add(char.ToUpperInvariant(shortWord[0]));
                slot.EnterFrenzy(shortWord);
            }
        }
    }

    // Every word on the body gets its real word back.
    public void ExitFrenzy()
    {
        foreach (BossPart slot in words)
        {
            slot.ExitFrenzy();
        }
    }

    // Adds every word, energy orb and quiz answer that can still be typed to
    // the list (used by WaveSpawner.GetTypingTargets).
    public void AddTypeableParts(List<ITypingTarget> targets)
    {
        foreach (BossPart word in words)
        {
            if (word.IsAlive)
            {
                targets.Add(word);
            }
        }

        orbs.RemoveAll(orb => !orb.IsAlive); // forget orbs that burst or hit the player
        foreach (EnergyOrb orb in orbs)
        {
            targets.Add(orb);
        }

        foreach (QuizAnswer answer in answers)
        {
            if (answer.IsAlive)
            {
                targets.Add(answer);
            }
        }
    }

    // The middle of the boss's body (explosions measure from here).
    public Vector3 BodyCenter
    {
        get { return transform.position + Vector3.up * 3f; }
    }

    private void Update()
    {
        if (!IsAlive || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        if (!stunned && !attacking && !Powers.IsFrozen)
        {
            UpdateAttackTimer();
        }
        UpdateColors();
        UpdateShake();
    }

    // ---- Attacks ----

    private void UpdateAttackTimer()
    {
        float interval = angry ? angryAttackInterval : attackInterval;
        if (interval <= 0f)
        {
            return;
        }

        attackTimer += Time.deltaTime;
        if (attackTimer >= interval)
        {
            attackTimer = 0f;
            StartCoroutine(PerformAttack(NextAttack()));
        }
    }

    // Every third attack is a stomp (if it still has a leg); when angry, the
    // second of three is a volley. Everything else is a throw.
    private Attack NextAttack()
    {
        attackCount += 1;
        int step = attackCount % 3;
        if (step == 0 && LegsLeft().Count > 0)
        {
            return Attack.Stomp;
        }
        if (step == 2 && angry)
        {
            return Attack.Volley;
        }
        return Attack.Throw;
    }

    private IEnumerator PerformAttack(Attack attack)
    {
        attacking = true;

        AnalyticsEvent e = Analytics.NewEvent("boss_attack");
        e.Add("attack", attack.ToString().ToLowerInvariant());
        e.Add("angry", angry);
        e.Add("boss_health", Health);
        Analytics.AddPlayerState(e);
        Analytics.Send(e);

        if (attack == Attack.Stomp)
        {
            yield return Stomp();
        }
        else if (attack == Attack.Volley)
        {
            yield return Volley();
        }
        else
        {
            yield return Throw();
        }
        windUp = 0f;
        ClearCharges();
        attacking = false;
    }

    // THROW: an arm rises overhead while an orb grows in its hand, then swings
    // forward and throws the orb. With no arm left, the orb charges in the chest.
    private IEnumerator Throw()
    {
        BossLimb arm = rightArm.IsGone ? (leftArm.IsGone ? null : leftArm) : rightArm;
        GameObject charge = CreateCharge();

        yield return Animate(attackWarningSeconds, t =>
        {
            windUp = t;
            if (arm != null && !arm.IsGone)
            {
                SetSwing(arm, Mathf.Lerp(0f, ArmRaised, SmoothStep(t)));
            }
            PlaceCharge(charge, arm, t);
        });
        if (!IsAlive)
        {
            yield break;
        }

        // The swing, then the release at its end.
        yield return Animate(0.15f, t =>
        {
            if (arm != null && !arm.IsGone)
            {
                SetSwing(arm, Mathf.Lerp(ArmRaised, ArmThrown, t));
            }
            PlaceCharge(charge, arm, 1f);
        });
        if (!IsAlive)
        {
            yield break;
        }
        Vector3 from = charge.transform.position;
        ClearCharges();
        windUp = 0f;
        LaunchOrb(from, orbFlightSeconds);
        Shake();

        // The arm comes back down.
        yield return Animate(0.5f, t =>
        {
            if (arm != null && !arm.IsGone)
            {
                SetSwing(arm, Mathf.Lerp(ArmThrown, 0f, SmoothStep(t)));
            }
        });
    }

    // VOLLEY: both arms up, a big orb charges in the chest, then 3 orbs fly one
    // after another (from the left hand, the chest and the right hand).
    private IEnumerator Volley()
    {
        GameObject charge = CreateCharge();

        yield return Animate(attackWarningSeconds, t =>
        {
            windUp = t;
            float swing = Mathf.Lerp(0f, VolleyRaised, SmoothStep(t));
            SetSwingIfThere(leftArm, swing);
            SetSwingIfThere(rightArm, swing);
            charge.transform.position = Chest();
            charge.transform.localScale = Vector3.one * ChargeSize * 1.6f * t;
        });
        if (!IsAlive)
        {
            yield break;
        }
        ClearCharges();
        windUp = 0f;

        Vector3[] starts =
        {
            leftArm.IsGone ? Chest() : leftArm.Tip,
            Chest(),
            rightArm.IsGone ? Chest() : rightArm.Tip
        };
        foreach (Vector3 start in starts)
        {
            LaunchOrb(start, orbFlightSeconds + volleyExtraSeconds);
            Shake();
            yield return Animate(0.3f, null);
            if (!IsAlive)
            {
                yield break;
            }
        }

        yield return Animate(0.6f, t =>
        {
            float swing = Mathf.Lerp(VolleyRaised, 0f, SmoothStep(t));
            SetSwingIfThere(leftArm, swing);
            SetSwingIfThere(rightArm, swing);
        });
    }

    // STOMP: a leg lifts, slams down: a shockwave, a big camera shake, and
    // zombies crawl out next to the boss (the first one red).
    private IEnumerator Stomp()
    {
        List<BossLimb> legs = LegsLeft();
        BossLimb leg = legs[Random.Range(0, legs.Count)];

        yield return Animate(attackWarningSeconds, t =>
        {
            windUp = t;
            SetSwingIfThere(leg, Mathf.Lerp(0f, LegLifted, SmoothStep(t)));
        });
        if (!IsAlive)
        {
            yield break;
        }

        yield return Animate(0.12f, t => SetSwingIfThere(leg, Mathf.Lerp(LegLifted, 0f, t * t)));
        if (!IsAlive)
        {
            yield break;
        }
        windUp = 0f;

        // Impact.
        CameraDirector.Shake(0.6f);
        Shake();
        StartCoroutine(Shockwave(leg.IsGone ? transform.position : leg.Tip));
        for (int i = 0; i < summonCount; i++)
        {
            float side = (i % 2 == 0) ? -1f : 1f;
            float outward = 2.8f + (i / 2) * 1.5f;
            Vector3 spot = transform.TransformPoint(new Vector3(side * outward, 0f, -2.5f));
            spawner.SpawnBossMinion(spot, i == 0 ? ZombieKind.Explosive : ZombieKind.Normal);
        }
        Tutorial.Once("boss summon", "The boss SUMMONS zombies! Blow up the RED one next to it to hurt the boss.", 6f);

        yield return Animate(0.4f, null);
    }

    // A ring on the ground that races out from the stomp and fades.
    private IEnumerator Shockwave(Vector3 at)
    {
        Transform ringHolder = new GameObject("Shockwave").transform;
        at.y = transform.position.y;
        ringHolder.position = at;
        BlastRing ring = BlastRing.Create(ringHolder, 0.5f, attackColor);

        const float duration = 0.6f;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            ring.SetRadius(Mathf.Lerp(0.5f, 9f, time / duration));
            yield return null;
        }
        Destroy(ringHolder.gameObject);
    }

    // Runs step(t) with t going 0 -> 1 over 'seconds' of play. Time stands still
    // while the game is paused or frozen (the freeze power stops the boss).
    private IEnumerator Animate(float seconds, System.Action<float> step)
    {
        float time = 0f;
        while (time < seconds)
        {
            if (!IsAlive)
            {
                yield break;
            }
            if (GameManager.Instance.State == GameState.Playing && !Powers.IsFrozen)
            {
                time += Time.deltaTime;
                if (step != null)
                {
                    step(Mathf.Clamp01(time / seconds));
                }
            }
            yield return null;
        }
    }

    // Fires an energy orb from 'from' at the player. Its word never starts with
    // the same letter as anything else on screen.
    private void LaunchOrb(Vector3 from, float flightSeconds)
    {
        string word = WordBank.PickOrbWord(spawner.UsedFirstLetters());
        orbs.Add(EnergyOrb.Launch(from, word, flightSeconds, attackDamage, hud));
    }

    // A glowing orb that grows in a hand (or the chest) during a wind-up.
    private GameObject CreateCharge()
    {
        GameObject charge = Shapes.Block(PrimitiveType.Sphere, "Charge", null, Chest(), Vector3.zero, Palette.Unlit(ChargeColor));
        charges.Add(charge);
        return charge;
    }

    private void PlaceCharge(GameObject charge, BossLimb arm, float grown)
    {
        bool inHand = arm != null && !arm.IsGone;
        charge.transform.position = inHand ? arm.Tip : Chest();
        charge.transform.localScale = Vector3.one * ChargeSize * grown;
    }

    private void ClearCharges()
    {
        foreach (GameObject charge in charges)
        {
            if (charge != null)
            {
                Destroy(charge);
            }
        }
        charges.Clear();
    }

    private Vector3 Chest()
    {
        return transform.TransformPoint(new Vector3(0f, 4.5f, -1.2f));
    }

    // Swings a limb around its shoulder / hip: + = forward and up.
    private static void SetSwing(BossLimb limb, float degrees)
    {
        limb.Pivot.localRotation = Quaternion.Euler(degrees, 0f, 0f);
    }

    private static void SetSwingIfThere(BossLimb limb, float degrees)
    {
        if (limb != null && !limb.IsGone)
        {
            SetSwing(limb, degrees);
        }
    }

    private List<BossLimb> LegsLeft()
    {
        List<BossLimb> legs = new List<BossLimb>();
        if (!leftLeg.IsGone)
        {
            legs.Add(leftLeg);
        }
        if (!rightLeg.IsGone)
        {
            legs.Add(rightLeg);
        }
        return legs;
    }

    // The body colour: grey, turning red during a wind-up, icy blue while
    // frozen, a white flash on a part just hit.
    private void UpdateColors()
    {
        Color color = bodyColor;
        if (Powers.IsFrozen)
        {
            color = Palette.FrozenIce;
        }
        else if (!stunned)
        {
            color = Color.Lerp(bodyColor, attackColor, windUp);
        }

        torso.material.color = color;
        foreach (BossLimb limb in limbs)
        {
            if (limb.IsGone)
            {
                continue;
            }
            limb.HitFlash = Mathf.Max(0f, limb.HitFlash - Time.deltaTime * 4f);
            limb.Block.material.color = Color.Lerp(color, hitColor, limb.HitFlash);
        }
    }

    // ---- Taking damage ----

    // Called by BossPart when one of its words is typed (the final bullet hit).
    public void OnWordTyped(BossPart slot)
    {
        BossLimb limb = slot.Limb;
        GameManager.Instance.AddKill();
        limb.HitFlash = 1f;
        float scale = Powers.IsFrenzy ? FrenzyWordDamageScale : 1f;

        if (limb.IsHead)
        {
            ShowDamage(slot.LabelAnchor, headDamage * scale);
            TakeHit(headDamage * scale, "head_word");
            if (IsAlive)
            {
                GiveNewWord(slot); // the head never falls off: it just gets a new word
            }
            return;
        }

        ShowDamage(slot.LabelAnchor, limbWordDamage * scale);
        TakeHit(limbWordDamage * scale, "limb_word");
        if (!IsAlive)
        {
            return;
        }

        // Both words of this limb typed: it falls off.
        foreach (BossPart word in limb.Words)
        {
            if (!word.IsCleared)
            {
                return;
            }
        }
        SeverLimb(limb);
    }

    private void SeverLimb(BossLimb limb)
    {
        limb.IsGone = true;
        string text = limb.Name.Contains("Arm") ? "ARM OFF!" : "LEG OFF!";
        hud.ShowFloatingText(limb.Block.transform.position + Vector3.up, text, Color.yellow);
        CameraDirector.Shake(0.35f);
        StartCoroutine(FallOff(limb));
        TakeHit(limbSeverDamage * (Powers.IsFrenzy ? FrenzyWordDamageScale : 1f), "limb_off");
        if (!IsAlive)
        {
            return;
        }

        // All 4 limbs gone: the quiz.
        foreach (BossLimb other in limbs)
        {
            if (!other.IsHead && !other.IsGone)
            {
                return;
            }
        }
        if (!stunned && !regrowing)
        {
            StartCoroutine(QuizRound());
        }
    }

    // The limb drops and shrinks away, then is hidden until it grows back.
    private IEnumerator FallOff(BossLimb limb)
    {
        Transform block = limb.Block.transform;
        Vector3 start = block.localPosition;
        const float duration = 0.5f;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float t = time / duration;
            block.localPosition = start + Vector3.down * 2f * t * t;
            block.localScale = limb.BlockSize * (1f - t);
            yield return null;
        }
        block.gameObject.SetActive(false);
    }

    // Brings every fallen limb back, with two new words each.
    private void RegrowLimbs()
    {
        foreach (BossLimb limb in limbs)
        {
            if (!limb.IsGone)
            {
                continue;
            }
            limb.IsGone = false;
            limb.Pivot.localRotation = Quaternion.identity;
            Transform block = limb.Block.transform;
            block.localPosition = limb.BlockPosition;
            block.localScale = limb.BlockSize;
            block.gameObject.SetActive(true);
            foreach (BossPart word in limb.Words)
            {
                GiveNewWord(word);
            }
        }
    }

    private void ShowDamage(Vector3 at, float damage)
    {
        hud.ShowFloatingText(at + Vector3.up * 0.5f, "-" + Mathf.RoundToInt(damage), Color.white);
    }

    // An explosion (barrel, lure bomb, red zombie) went off next to the boss.
    public void TakeBlastDamage(float damage)
    {
        if (IsAlive)
        {
            hud.ShowFloatingText(BodyCenter + Vector3.up * 2f, "-" + Mathf.RoundToInt(damage), Palette.WordBarrel);
            TakeHit(damage, "blast");
        }
    }

    // cause (for analytics): "head_word", "limb_word", "limb_off", "blast" or "quiz_right".
    private void TakeHit(float damage, string cause)
    {
        if (!IsAlive)
        {
            return;
        }
        Health = Mathf.Max(0f, Health - damage);
        hud.SetBossHealth(Health, maxHealth);
        Shake();

        AnalyticsEvent e = Analytics.NewEvent("boss_hit");
        e.Add("cause", cause);
        e.Add("damage", damage);
        e.Add("boss_health", Health); // after the hit; 0 = this hit killed it
        e.Add("angry", angry);
        Analytics.Send(e);

        if (Health <= 0f)
        {
            Die(); // enough damage: it dies, whatever words are left
            return;
        }

        // Below half health: angry, attacks come faster (and volleys start).
        if (!angry && Health <= maxHealth * 0.5f)
        {
            angry = true;
            hud.ShowFloatingText(BodyCenter + Vector3.up * 3f, "IT'S ANGRY!", new Color(1f, 0.3f, 0.2f));
        }
    }

    private void Heal(float amount)
    {
        Health = Mathf.Min(maxHealth, Health + amount);
        hud.SetBossHealth(Health, maxHealth);

        AnalyticsEvent e = Analytics.NewEvent("boss_heal");
        e.Add("amount", amount);
        e.Add("boss_health", Health);
        Analytics.Send(e);
    }

    // ---- The quiz ----

    // Called by a QuizAnswer when the player's final shot hits it.
    public void OnQuizAnswer(QuizAnswer answer)
    {
        if (quizOpen && chosenAnswer == null)
        {
            chosenAnswer = answer;
        }
    }

    private IEnumerator QuizRound()
    {
        stunned = true;
        Shake();

        // 1. The question and its 3 answers, floating in a row in front of the
        //    boss. They float low (2.3 m) so that, seen from the player, they
        //    stay BELOW the quiz box at the top of the screen, never behind it.
        QuizQuestion question = QuizBank.Pick(spawner.UsedFirstLetters());
        hud.ShowQuiz(question.Question);
        Tutorial.Once("quiz", "BOSS QUIZ! Type the RIGHT answer for a critical hit. A wrong one heals it!", 6f);

        Vector3[] spots =
        {
            new Vector3(-3.2f, 2.3f, -2.5f),
            new Vector3(0f, 2.3f, -3f),
            new Vector3(3.2f, 2.3f, -2.5f)
        };
        answers.Clear();
        for (int i = 0; i < question.Answers.Length && i < spots.Length; i++)
        {
            answers.Add(QuizAnswer.Create(transform.TransformPoint(spots[i]), question.Answers[i], this, hud));
        }

        // 2. Wait for an answer (the timer stops while frozen or while a
        //    killing shot is still flying to an answer).
        chosenAnswer = null;
        quizOpen = true;
        float elapsed = 0f;
        while (IsAlive && chosenAnswer == null && (elapsed < quizSeconds || AnswerShotInFlight()))
        {
            if (GameManager.Instance.State == GameState.Playing && !Powers.IsFrozen)
            {
                elapsed += Time.deltaTime;
            }
            hud.SetQuizTimer(1f - Mathf.Clamp01(elapsed / quizSeconds));
            yield return null;
        }
        quizOpen = false;
        if (!IsAlive)
        {
            yield break; // Die already cleared the quiz
        }

        // Analytics: the question and what the player did with it.
        AnalyticsEvent e = Analytics.NewEvent("quiz");
        e.Add("question_index", QuizBank.LastPickedIndex);
        e.Add("question", question.Question);
        e.Add("answers", string.Join(" | ", question.Answers)); // in screen order: left, middle, right
        e.Add("correct_position", question.CorrectIndex);       // 0 = left
        e.Add("chosen_position", chosenAnswer == null ? -1 : answers.IndexOf(chosenAnswer)); // -1 = too slow
        e.Add("result", chosenAnswer == null ? "timeout" : chosenAnswer.Word == question.CorrectAnswer ? "right" : "wrong");
        e.Add("seconds", elapsed);                              // quiz timer (stops while frozen)
        e.Add("boss_health", Health);
        Analytics.AddPlayerState(e);
        Analytics.Send(e);

        // 3. The verdict.
        Vector3 textPosition = BodyCenter + Vector3.up * 3.5f;
        if (chosenAnswer == null)
        {
            hud.ShowFloatingText(textPosition, "TOO SLOW!", Color.white);
        }
        else if (chosenAnswer.Word == question.CorrectAnswer)
        {
            hud.ShowFloatingText(textPosition, "CRITICAL HIT!", Color.yellow);
            CameraDirector.Shake(0.6f);
            GameManager.Instance.AddKill();
            TakeHit(quizCorrectDamage, "quiz_right");
        }
        else
        {
            hud.ShowFloatingText(textPosition, "WRONG! IT HEALS", new Color(1f, 0.3f, 0.2f));
            GameManager.Instance.ResetCombo();
            Heal(quizWrongHeal);
            LaunchOrb(Chest(), orbFlightSeconds);
        }

        foreach (QuizAnswer answer in answers)
        {
            answer.Remove();
        }
        answers.Clear();
        hud.HideQuiz();
        stunned = false;

        // 4. The limbs grow back with new words.
        if (IsAlive)
        {
            regrowing = true;
            yield return new WaitForSeconds(regrowSeconds);
            regrowing = false;
            if (IsAlive)
            {
                RegrowLimbs();
            }
        }
    }

    // True if the player finished typing an answer and the bullet has not hit yet.
    private bool AnswerShotInFlight()
    {
        foreach (QuizAnswer answer in answers)
        {
            if (answer.IsAlive && answer.IsWordComplete)
            {
                return true;
            }
        }
        return false;
    }

    // ---- Dying ----

    private void Die()
    {
        IsAlive = false; // TypingController and WaveSpawner see this right away
        StopAllCoroutines(); // any attack or quiz in progress
        attacking = false;
        windUp = 0f;
        ClearCharges();

        GameManager.Instance.AddKill();
        foreach (BossPart word in words)
        {
            Destroy(word.Label.gameObject); // the words are on the HUD, not children of the boss
        }
        foreach (EnergyOrb orb in orbs)
        {
            orb.Dissipate(); // orbs still in the air fizzle out harmlessly
        }
        orbs.Clear();
        foreach (QuizAnswer answer in answers)
        {
            answer.Remove();
        }
        answers.Clear();
        quizOpen = false;
        hud.HideQuiz();
        StartCoroutine(DeathEffect());
    }

    // Shrinks the boss into the ground, then removes it.
    private IEnumerator DeathEffect()
    {
        const float duration = 1f;
        transform.position = homePosition;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float scale = 1f - time / duration;
            transform.localScale = new Vector3(scale, scale, scale);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        ClearCharges(); // charges have no parent: they would stay behind
    }

    // ---- Shake ----

    private void Shake()
    {
        shakeTimer = shakeSeconds;
    }

    private void UpdateShake()
    {
        if (shakeTimer <= 0f)
        {
            transform.position = homePosition;
            return;
        }

        shakeTimer -= Time.deltaTime;
        float strength = shakeStrength * Mathf.Clamp01(shakeTimer / shakeSeconds);
        Vector2 offset = Random.insideUnitCircle * strength;
        transform.position = homePosition + new Vector3(offset.x, 0f, offset.y);
    }

    // 0..1 -> 0..1, starting and ending gently.
    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}

// One part of the boss's body: the head, an arm or a leg, with its word slots.
// Plain data, owned by the Boss.
public class BossLimb
{
    public string Name;
    public bool IsHead;
    public Transform Pivot;           // the shoulder / hip it swings around (the head's own centre)
    public Renderer Block;            // the shape
    public Vector3 BlockPosition;     // the block's place under the pivot (restored when it grows back)
    public Vector3 BlockSize;
    public Vector3 TipUnderPivot;     // the hand / foot, relative to the pivot
    public readonly List<BossPart> Words = new List<BossPart>();
    public bool IsGone;               // a limb whose words were all typed has fallen off
    public float HitFlash;            // 1 right after a word hits it, fades to 0

    // Where the hand / foot is now (it moves with the swing).
    public Vector3 Tip
    {
        get { return Pivot.TransformPoint(TipUnderPivot); }
    }
}

// One word on the boss's body (1 on the head, 2 on every limb). Not a
// MonoBehaviour: the Boss creates and owns these, and TypingController sees
// them as ITypingTarget.
public class BossPart : ITypingTarget
{
    private readonly Boss boss;
    private readonly Vector3 labelLocalPosition; // where the word sits, relative to the boss's feet

    public BossLimb Limb { get; private set; }
    public TMP_Text Label { get; private set; }
    public string Word { get; private set; }
    public string ColoredWord { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsCleared { get; private set; } // typed (on a limb: until the limb grows back)

    public BossPart(Boss owner, BossLimb limb, TMP_Text label, Vector3 labelPosition)
    {
        boss = owner;
        Limb = limb;
        Label = label;
        labelLocalPosition = labelPosition;
        IsCleared = true; // until SetWord gives it a word
        Word = "?";       // a placeholder until the first SetWord
    }

    public bool IsAlive
    {
        get { return boss.IsAlive && !IsCleared && !Limb.IsGone; }
    }

    public bool IsTargeted
    {
        get { return TypedCount > 0; }
    }

    public bool IsCaseSensitive
    {
        get { return false; }
    }

    public Vector3 Position
    {
        get { return Limb.Block.transform.position; }
    }

    public Vector3 HitPoint
    {
        get { return Limb.Block.transform.position; }
    }

    public Vector3 LabelAnchor
    {
        get { return boss.transform.TransformPoint(labelLocalPosition); }
    }

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

    public void ResetProgress()
    {
        TypedCount = 0;
        RefreshLabel();
    }

    public void CompleteWord()
    {
        if (IsCleared)
        {
            return;
        }
        IsCleared = true;
        TypedCount = 0;
        Label.enabled = false; // WaveSpawner.LayoutLabels shows it again once it has a new word
        boss.OnWordTyped(this);
    }

    // A new word for this slot.
    public void SetWord(string word)
    {
        Word = word;
        realWord = null; // a fresh word: not a frenzy word
        Label.color = Color.white;
        TypedCount = 0;
        IsCleared = false;
        RefreshLabel();
    }

    // FRENZY: a short word in a vivid colour for a while; ExitFrenzy gives the
    // real word back (unless the word was typed meanwhile).
    private string realWord;

    public void EnterFrenzy(string shortWord)
    {
        if (IsCleared || realWord != null)
        {
            return;
        }
        realWord = Word;
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
        Label.color = Color.white;
        RefreshLabel();
    }

    // Same look as a zombie's word: stored UPPERCASE, shown in lowercase.
    private void RefreshLabel()
    {
        string shown = Word.ToLowerInvariant();
        ColoredWord = Zombie.TypedColorTag + shown.Substring(0, TypedCount) + "</color>" + shown.Substring(TypedCount);
        Label.text = ColoredWord;
    }
}
