// GuidedIntro.cs
// ---------------------------------------------------------------------------
// The NORMAL level's first wave teaches the game (there is no separate
// tutorial level). The HUD starts empty (HudReveal) and each thing is shown
// the first time it is needed. When something new needs a key, the world is
// HELD for a moment (zombies wait, everything else runs) while one short card
// low on the screen says what to press, and the player does it for real.
// Everything else is learned by playing: pieces of the HUD simply pop in.
//
//   ride       "You move automatically, don't worry."  ENTER skips the whole
//              intro (only now; wave 1 is then a normal wave)
//   shoot      the first zombie: type its word (HEALTH, SCORE, WAVE pop in)
//              two more: keep shooting
//   Backspace  start a word, then BACKSPACE drops it
//   energy     COMBO and ENERGY pop in; type the teal ENERGY BOX for +7
//   barrel     a group walks into a barrel's ring (held there): type its word
//   [1]        LURE BOMB: six come out; hold 1, aim with the arrow keys, let go
//   [2]        FREEZE        [3] FRENZY        [4] RPG (load, then fire)
//   red        once the field is clear, a RED zombie with three others in
//              its ring: shoot the red one
//   [TAB]      SHIELD: press Tab (one every wave)
//   end        the whole HUD; the rest of the run plays normally
//
// Only NORMAL zombies and one red one come in wave 1 (the other kinds are
// introduced one at a time in later waves, see WaveSpawner.IntroduceNewKinds).
// Every lesson waits for a clear field (and a lure bomb to have gone off)
// before it starts, so two never overlap (Calm).
// While a card waits for a power key, words cannot be typed (they are
// ignored, never wrong; the card shakes) and the power's energy is topped
// up, so a step can never get stuck. Damage is halved during the intro.
//
// The player's typing speed is measured through wave 1 as in every level
// (DynamicDifficulty) and applied when it is over (WaveSpawner).
// Run by WaveSpawner: Begin when the level starts, RunGuidedWave as wave 1.
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GuidedIntro
{
    // True from the start of the level until the intro is over or skipped.
    public static bool Running { get; private set; }

    // True while the world is held for a card (Zombie.Update waits).
    public static bool Holding { get; private set; }

    // True while a card waits for a power key: words wait too (TypingController).
    public static bool BlocksLetters { get; private set; }

    private const float WalkOutSeconds = 2f;   // new zombies walk out of their door before anything else
    private const float SkipArmSeconds = 1f;   // Enter is not a skip in the first second (a double press to start the level)
    private const float CloseEnough = 9f;      // metres: a group must come this close before a blast is taught
                                               // (a quick tap throws the lure bomb 9 m straight ahead)
    private const float GroupLanes = 1.6f;     // metres between the zombies of a group (close enough for one blast)
    private const float PairLanes = 2.6f;      // ...and of two zombies that are shot one by one

    private static bool skipOffered;
    private static float skipArmedAt;
    private static bool wordDropped;

    // SAFETY: a lesson that waits for the player (a power key, Backspace, a
    // word) can never hold them forever: after SkipAfterSeconds its card
    // offers "ENTER to skip" (a tap on the card works too).
    private const float SkipAfterSeconds = 12f;
    private static float lessonStarted;
    private static bool lessonSkippable;
    private static bool lessonSkipped;
    private static string lessonTitle, lessonBody;
    private static GuideStep[] lessonSteps;
    private static WaveSpawner spawner;
    private static HUD hud;
    private static GuidedIntroView view;
    internal static bool TryGetCardBounds(RectTransform wordLayer, out Rect bounds)
    {
        bounds = default;
        return view != null && view.TryGetCardBounds(wordLayer, out bounds);
    }
    private static TypingController typing;
    private static readonly HashSet<string> powerActions = new HashSet<string>(); // "lure:throw", "rpg:fire"...

    // ---- Called by the game ----

    // WaveSpawner, when the level starts (before the first ride).
    public static void Begin(WaveSpawner owner, HUD hudRef)
    {
        spawner = owner;
        hud = hudRef;
        typing = Object.FindFirstObjectByType<TypingController>();
        Running = true;
        Holding = false;
        BlocksLetters = false;
        skipOffered = true;
        skipArmedAt = Time.unscaledTime + SkipArmSeconds;
        powerActions.Clear();
        HudReveal.HideAll();
        Powers.ActionPerformed -= OnPower;
        Powers.ActionPerformed += OnPower;
        view = GuidedIntroView.Create(hudRef);
        view.ShowCard("", "You move automatically.", null, false);
        view.ShowSkip(true);
        Log("start");
    }

    // TypingController: Enter. Skips the intro while that is offered (only
    // during the opening ride). The skip chip and a tap on the card call it too.
    public static void OnEnter()
    {
        if (!Running)
        {
            return;
        }
        if (skipOffered)
        {
            if (Time.unscaledTime < skipArmedAt)
            {
                return; // the Enter that started the level, pressed twice
            }
            Log("skipped");
            End();
        }
        else if (lessonSkippable)
        {
            lessonSkipped = true;
        }
    }

    // TypingController: Backspace dropped a half-typed word.
    public static void OnWordDropped()
    {
        wordDropped = true;
    }

    // TypingController: letters arrived while a power key is wanted.
    public static void Nudge()
    {
        if (view != null)
        {
            view.Nudge();
        }
    }

    // The scene is going away in the middle of the intro (restart, menu): forget it.
    public static void Abandon()
    {
        Running = false;
        Holding = false;
        BlocksLetters = false;
        skipOffered = false;
        Powers.ActionPerformed -= OnPower;
    
        if (view != null)
            view.Close();
    
        view = null;
    }

    private static void OnPower(string action, string item)
    {
        powerActions.Add(item + ":" + action);
    }

    // The intro is over (or skipped): the whole HUD, every power, normal play.
    private static void End()
    {
        Running = false;
        Holding = false;
        BlocksLetters = false;
        skipOffered = false;
        Powers.ActionPerformed -= OnPower;
        if (hud != null)
        {
            hud.ShowWholeHud();
        }
        if (view != null)
        {
            view.Close();
        }
        view = null;
        if (spawner != null)
        {
            spawner.ActivateAllProps(); // the barrels and boxes not taught yet get their words (only during a fight)
        }
    }

    // ---- Wave 1, guided (WaveSpawner runs it in place of a normal wave 1) ----

    public static IEnumerator RunGuidedWave()
    {
        skipOffered = false; // from here on the player is in
        view.ShowSkip(false);
        view.HideCard();
        Log("guided_wave");
        bool touch = TouchControls.Active;

        // SHOOT: the first zombie, held until its word is typed.
        Zombie first = spawner.SpawnGuided(FrontPoint(), ZombieKind.Normal);
        yield return new WaitForSeconds(WalkOutSeconds);
        Holding = true;
        view.ShowCard("TYPE TO SHOOT", "Type the word above the zombie.", null, false);
        view.PointAt(first);
        yield return UntilDead(first);
        view.PointAt(null);
        view.HideCard();
        Holding = false;
        HudReveal.Reveal(HudFeature.Health);
        HudReveal.Reveal(HudFeature.Score);
        HudReveal.Reveal(HudFeature.Wave);

        // Two more, one from each side: just play.
        yield return new WaitForSeconds(0.8f);
        List<Zombie> group = Pair(ZombieKind.Normal);
        view.ShowLine("Keep typing enemy words.", 3f);
        yield return UntilAllDead(group);

        // BACKSPACE: start a word, then drop it.
        yield return new WaitForSeconds(0.8f);
        group = Pair(ZombieKind.Normal);
        yield return new WaitForSeconds(WalkOutSeconds * 0.5f);
        view.ShowLine("Start typing a word...", 6f);
        while (AnyAlive(group) && !IsTypingOneOf(group))
        {
            yield return null;
        }
        if (AnyAlive(group) && IsTypingOneOf(group))
        {
            Holding = true;
            BlocksLetters = true;
            wordDropped = false;
            BeginLesson("CHANGE TARGET", "Drop your current word.",
                new[] { GuideStep.Key(touch ? "DEL" : "BACKSPACE", touch ? "TAP" : "PRESS") });
            while (!wordDropped && typing != null && typing.IsTypingWord && !LessonSkipped())
            {
                yield return null;
            }
            view.HideCard();
            BlocksLetters = false;
            Holding = false;
            view.ShowLine("Now pick any word.", 3f);
        }
        yield return UntilAllDead(group);

        // ENERGY: it fills with every word; the teal box gives more.
        yield return new WaitForSeconds(0.6f);
        HudReveal.Reveal(HudFeature.Combo);
        HudReveal.Reveal(HudFeature.Energy);
        SupplyCrate box = EnergyBox();
        if (box != null)
        {
            spawner.ActivateProp(box);
            yield return UntilNotTyping();
            Holding = true;
            BeginLesson("ENERGY", "Type the teal crate's word for +" + SupplyCrate.EnergyAmount + " energy.", null);
            view.PointAt(box);
            float giveUp = Time.time + 25f;
            while (!box.IsOpened && Time.time < giveUp && !LessonSkipped())
            {
                yield return null;
            }
            view.PointAt(null);
            view.HideCard();
            Holding = false;
        }
        else
        {
            view.ShowLine("Every word you finish gives +1 ENERGY.", 4f);
        }

        // BARREL: a group walks past one; its word blasts them all.
        int point;
        Barrel barrel = BarrelOnPath(out point);
        if (barrel != null)
        {
            spawner.ActivateProp(barrel);
            yield return new WaitForSeconds(0.8f);
            group = Group(point, ZombieKind.Normal, 3, GroupLanes);
            yield return new WaitForSeconds(WalkOutSeconds);
            // Hold once EVERY zombie of the group stands inside the barrel's
            // ring (or, if one lags behind, once the first is about to walk out of it).
            float giveUp = Time.time + 16f;
            while (AnyAlive(group) && barrel.IsAlive && !AllNearBarrel(group, barrel) && !LeavingRing(group, barrel) && Time.time < giveUp)
            {
                yield return null;
            }
            if (AnyAlive(group) && barrel.IsAlive)
            {
                Holding = true;
                BeginLesson("BARREL", "Type the barrel's word to blow it up.", null);
                view.PointAt(barrel);
                while (barrel.IsAlive && AnyAlive(group) && !LessonSkipped())
                {
                    yield return null;
                }
                view.PointAt(null);
                view.HideCard();
                Holding = false;
            }
            yield return Finish(group, "Finish them off.");
        }

        // [1] LURE BOMB: a crowd of six from the nearest door; the moment the
        // last one is out, the world waits for the bomb (even a fast typist
        // has shot two or three at most, so a crowd is left for it).
        yield return Calm(1f);
        point = NearPoint();
        group = Group(point, ZombieKind.Normal, 3, GroupLanes);
        yield return new WaitForSeconds(0.9f);
        group.AddRange(Group(point, ZombieKind.Normal, 3, GroupLanes));
        yield return new WaitForSeconds(WalkOutSeconds);
        yield return OfferPower(HudFeature.Lure, "lure", "throw", Powers.LureCost,
            "LURE BOMB", touch ? "Tap Lure to throw or drag to aim."
                : "Hold 1, aim with the arrows, then release.",
            touch ? new[] { GuideStep.Text("LURE", "TAP OR DRAG") }
                  : new[] { GuideStep.Key("1", "HOLD"), GuideStep.Arrows("AIM"), GuideStep.Key("1", "LET GO") });
        yield return Finish(group, "Keep shooting!");

        // [2] FREEZE, only once the lure bomb has gone off and the dust settled.
        // Five come out (three from one side, two from the other): plenty to freeze.
        yield return Calm(2.5f);
        int[] sides = TwoDoors();
        group = Group(sides[0], ZombieKind.Normal, 3, GroupLanes);
        yield return new WaitForSeconds(0.6f);
        group.AddRange(Group(sides[1], ZombieKind.Normal, 2, PairLanes));
        yield return new WaitForSeconds(WalkOutSeconds + 0.5f);
        yield return OfferPower(HudFeature.Freeze, "freeze", "use", Powers.FreezeCost,
            "FREEZE", touch ? "Tap Freeze to stop the zombies." : "Press 2 to freeze the zombies.", null);
        yield return Finish(group, "Shoot the frozen zombies.");

        // [3] FRENZY: words far too long, made short.
        yield return Calm(1.5f);
        int[] doors = TwoDoors();
        group = new List<Zombie> { spawner.SpawnGuided(doors[0], ZombieKind.Normal, 0f, true), spawner.SpawnGuided(doors[1], ZombieKind.Normal, 0f, true) };
        group.RemoveAll(zombie => zombie == null);
        yield return new WaitForSeconds(WalkOutSeconds + 0.5f);
        yield return OfferPower(HudFeature.Frenzy, "frenzy", "use", Powers.FrenzyCost,
            "FRENZY", touch ? "Tap Frenzy to shorten enemy words." : "Press 3 to shorten enemy words.", null);
        yield return Finish(group, null); // the FRENZY popup says it already

        // [4] RPG: load it, then any finished word fires it into the group.
        // A crowd of five; the card shows the moment they are all out.
        yield return Calm(1.5f);
        point = NearPoint();
        group = Group(point, ZombieKind.Normal, 3, GroupLanes);
        yield return new WaitForSeconds(0.9f);
        group.AddRange(Group(point, ZombieKind.Normal, 2, PairLanes));
        yield return new WaitForSeconds(WalkOutSeconds);
        yield return OfferPower(HudFeature.Rpg, "rpg", "use", Powers.RocketCost,
            "RPG", touch ? "Tap RPG, then finish a word to fire." : "Press 4, then finish a word to fire.",
            new[] { touch ? GuideStep.Text("RPG", "TAP") : GuideStep.Key("4", "PRESS"), GuideStep.Text("TYPE", "A WORD") });
        // The lesson is not over until the rocket has really flown: the world
        // waits, and if every target is somehow gone, new ones walk out.
        Holding = true;
        bool fireShown = false;
        while (!powerActions.Contains("rpg:fire") && GameManager.Instance.Powers.RocketLoaded && !LessonSkipped())
        {
            if (!AnyAlive(group))
            {
                Holding = false;
                group = Group(NearPoint(), ZombieKind.Normal, 3, GroupLanes);
                yield return new WaitForSeconds(WalkOutSeconds);
                Holding = true;
            }
            if (!fireShown)
            {
                BeginLesson("RPG LOADED", "Finish a word to fire.", null);
                fireShown = true;
            }
            view.PointAt(Middle(group));
            yield return null;
        }
        view.PointAt(null);
        view.HideCard();
        Holding = false;
        yield return Finish(group, "Finish them off.");

        // RED ZOMBIE, only once the RPG's group is gone and the field is
        // clear: it walks in with three others right beside it (in its ring).
        yield return Calm(2.5f);
        point = NearPoint();
        Zombie red = spawner.SpawnGuided(point, ZombieKind.Explosive, 0.01f);
        group = new List<Zombie> { red, spawner.SpawnGuided(point, ZombieKind.Normal, -1.3f), spawner.SpawnGuided(point, ZombieKind.Normal, 1.3f) };
        yield return new WaitForSeconds(0.7f);
        group.Add(spawner.SpawnGuided(point, ZombieKind.Normal, 0.01f)); // right behind it
        group.RemoveAll(zombie => zombie == null);
        yield return UntilClose(group);
        if (red != null && red.IsAlive)
        {
            yield return UntilNotTyping();
            Holding = true; // the others stay in its ring until it goes off
            BeginLesson("RED ZOMBIE", "Type the red zombie's word to set off its blast.", null);
            view.PointAt(red);
            while (red != null && red.IsAlive && !LessonSkipped())
            {
                yield return null;
            }
            view.PointAt(null);
            view.HideCard();
            Holding = false;
        }
        yield return Finish(group, "Finish them off.");

        // [TAB] SHIELD: four come close (two from each side)...
        yield return Calm(1.5f);
        int[] shieldSides = TwoDoors();
        group = Group(shieldSides[0], ZombieKind.Normal, 2, PairLanes);
        yield return new WaitForSeconds(0.5f);
        group.AddRange(Group(shieldSides[1], ZombieKind.Normal, 2, PairLanes));
        yield return new WaitForSeconds(WalkOutSeconds);
        float closeBy = Time.time + 10f;
        while (AnyAlive(group) && Time.time < closeBy && !AnyWithin(group, 6f))
        {
            yield return null;
        }
        yield return UntilNotTyping();
        Powers powers = GameManager.Instance.Powers;
        powers.RefillTrainingSupplies();
        Holding = true;
        BlocksLetters = true;
        HudReveal.Reveal(HudFeature.Shield);
        BeginLesson("SHIELD", touch ? "Tap Shield to block hits." : "Press Tab to block hits.", null);
        while (!Powers.IsShielded && !LessonSkipped())
        {
            if (powers.ShieldCharges == 0)
            {
                powers.RefillTrainingSupplies(); // never stuck
            }
            yield return null;
        }
        view.HideCard();
        BlocksLetters = false;
        Holding = false;

        // ...and while it is up, three more walk in and bounce off it: the
        // shield is never shown with nothing to block.
        group.AddRange(Group(NearPoint(), ZombieKind.Normal, 3, GroupLanes));
        yield return Finish(group, "Shoot the zombies while shielded.");

        // Everything has been shown: normal play from here.
        Log("complete");
        Tutorial.MarkShown("type");
        Tutorial.MarkShown("barrel");
        Tutorial.MarkShown("crate");
        Tutorial.MarkShown("explosive");
        End();
        while (spawner.AliveZombies.Count > 0)
        {
            yield return null;
        }
    }

    // ---- Steps ----

    // A power appears and the world waits until it is used ('done' is the
    // Powers action: "throw" for the lure bomb, "use" for the others).
    private static IEnumerator OfferPower(HudFeature feature, string item, string done, int cost,
        string title, string body, GuideStep[] steps)
    {
        powerActions.Remove(item + ":" + done);
        powerActions.Remove("rpg:fire");
        yield return UntilNotTyping(); // never cut into a word being typed
        Holding = true;
        BlocksLetters = true;
        GameManager.Instance.GrantTrainingEnergy(cost);
        HudReveal.Reveal(feature);
        BeginLesson(title, body, steps);
        while (!powerActions.Contains(item + ":" + done) && !LessonSkipped())
        {
            GameManager.Instance.GrantTrainingEnergy(cost); // never stuck for lack of energy
            yield return null;
        }
        view.HideCard();
        BlocksLetters = false;
        Holding = false;
    }

    // A lesson card that waits for the player. It can be skipped after a while (LessonSkipped).
    private static void BeginLesson(string title, string body, GuideStep[] steps)
    {
        lessonStarted = Time.time;
        lessonSkippable = false;
        lessonSkipped = false;
        lessonTitle = title;
        lessonBody = body;
        lessonSteps = steps;
        view.ShowCard(title, body, steps, false);
    }

    // Every frame of a lesson's wait: after SkipAfterSeconds the card offers
    // "ENTER to skip"; true once the player has skipped it.
    private static bool LessonSkipped()
    {
        if (!lessonSkippable && Time.time - lessonStarted >= SkipAfterSeconds)
        {
            lessonSkippable = true;
            view.ShowCard(lessonTitle, lessonBody, lessonSteps, true);
        }
        if (lessonSkipped)
        {
            lessonSkippable = false;
            lessonSkipped = false;
            Log("lesson_skipped:" + lessonTitle);
            return true;
        }
        return false;
    }

    // Until the player is not in the middle of a word (at most a few
    // seconds), so a lesson never cuts into one.
    private static IEnumerator UntilNotTyping()
    {
        float giveUp = Time.time + 4f;
        while (typing != null && typing.IsTypingWord && Time.time < giveUp)
        {
            yield return null;
        }
    }

    // Until the group is dead, with a short line saying so.
    private static IEnumerator Finish(List<Zombie> group, string line)
    {
        if (!AnyAlive(group))
        {
            yield break;
        }
        yield return new WaitForSeconds(0.4f);
        if (AnyAlive(group) && line != null)
        {
            view.ShowLine(line, 3f);
        }
        yield return UntilAllDead(group);
        view.HideLine();
    }

    // Until nothing is left on the field (no zombie, no lure bomb still
    // ticking), then a breather, so two lessons never overlap.
    private static IEnumerator Calm(float seconds)
    {
        float rocketWait = Time.time + 5f;
        while (spawner.AliveZombies.Count > 0 || LureBomb.Active.Count > 0
            || (GameManager.Instance.Powers.RocketLoaded && Time.time < rocketWait))
        {
            if (!GameManager.Instance.Powers.RocketLoaded)
            {
                rocketWait = Time.time + 5f;
            }
            yield return null;
        }
        // A rocket loaded with nothing left to fire at would hit the next lesson's group: unloaded (energy back).
        GameManager.Instance.Powers.UnloadRocket();
        yield return new WaitForSeconds(seconds);
    }

    // ---- Spawning ----

    // 'count' zombies side by side out of one spawn point.
    private static List<Zombie> Group(int point, ZombieKind kind, int count, float lanes)
    {
        List<Zombie> group = new List<Zombie>();
        for (int i = 0; i < count; i++)
        {
            float lane = (i - (count - 1) * 0.5f) * lanes;
            Zombie zombie = spawner.SpawnGuided(point, kind, lane == 0f ? 0.01f : lane);
            if (zombie != null)
            {
                group.Add(zombie);
            }
        }
        return group;
    }

    // Two zombies out of two different doors (one from each side when it can).
    private static List<Zombie> Pair(ZombieKind kind)
    {
        int[] doors = TwoDoors();
        List<Zombie> pair = new List<Zombie>();
        if (doors[0] == doors[1])
        {
            pair.AddRange(Group(doors[0], kind, 2, PairLanes));
            return pair;
        }
        foreach (int door in doors)
        {
            Zombie zombie = spawner.SpawnGuided(door, kind);
            if (zombie != null)
            {
                pair.Add(zombie);
            }
        }
        return pair;
    }

    // The spawn point most straight ahead of the player (the first zombie is easy to see).
    private static int FrontPoint()
    {
        Encounter fight = spawner.CurrentEncounter;
        List<SpawnPoint> points = fight.SpawnPoints;
        int best = 0;
        float bestDot = float.MinValue;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 to = points[i].Exit - fight.StopPosition;
            to.y = 0f;
            float dot = Vector3.Dot(to.normalized, fight.Facing);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = i;
            }
        }
        return best;
    }

    // The NEAREST spawn point in front of the player (within 40 degrees):
    // a crowd from there is soon in reach of a quick lure bomb or rocket.
    private static int NearPoint()
    {
        Encounter fight = spawner.CurrentEncounter;
        List<SpawnPoint> points = fight.SpawnPoints;
        int best = FrontPoint();
        float bestDistance = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 to = Flat(points[i].Exit - fight.StopPosition);
            if (Vector3.Angle(to, fight.Facing) <= 40f && to.magnitude < bestDistance)
            {
                bestDistance = to.magnitude;
                best = i;
            }
        }
        return best;
    }

    // The leftmost and rightmost spawn points as the player sees them.
    private static int[] TwoDoors()
    {
        Encounter fight = spawner.CurrentEncounter;
        List<SpawnPoint> points = fight.SpawnPoints;
        Vector3 right = new Vector3(fight.Facing.z, 0f, -fight.Facing.x);
        int left = 0, rightmost = 0;
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < points.Count; i++)
        {
            float side = Vector3.Dot(points[i].Exit - fight.StopPosition, right);
            if (side < min)
            {
                min = side;
                left = i;
            }
            if (side > max)
            {
                max = side;
                rightmost = i;
            }
        }
        return new[] { left, rightmost };
    }

    // A barrel that a group from some door walks right past (that door in 'point'), or null.
    private static Barrel BarrelOnPath(out int point)
    {
        point = 0;
        Encounter fight = spawner.CurrentEncounter;
        List<SpawnPoint> points = fight.SpawnPoints;
        Barrel best = null;
        float bestDistance = Barrel.BlastRadius * 0.6f;
        foreach (Barrel barrel in fight.Barrels)
        {
            if (barrel == null || barrel.IsExploded || Flat(barrel.Position - fight.StopPosition).magnitude < Barrel.BlastRadius + 2f)
            {
                continue; // too close to the player to be a good first example
            }
            for (int i = 0; i < points.Count; i++)
            {
                float distance = DistanceToPath(barrel.Position, points[i].Exit, fight.StopPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = barrel;
                    point = i;
                }
            }
        }
        return best;
    }

    private static SupplyCrate EnergyBox()
    {
        foreach (SupplyCrate crate in spawner.CurrentEncounter.Crates) // only the ones still standing (health kits are hidden)
        {
            if (crate != null && !crate.IsOpened && crate.Kind == SupplyKind.Energy)
            {
                return crate;
            }
        }
        return null;
    }

    // ---- Waiting ----

    // Until one of the group is within CloseEnough (at most 9 s), so a blast
    // taught now has them in reach.
    private static IEnumerator UntilClose(List<Zombie> group)
    {
        yield return new WaitForSeconds(WalkOutSeconds);
        float giveUp = Time.time + 9f;
        while (Time.time < giveUp && AnyAlive(group) && !AnyWithin(group, CloseEnough))
        {
            yield return null;
        }
    }

    private static IEnumerator UntilDead(Zombie zombie)
    {
        while (zombie != null && zombie.IsAlive)
        {
            yield return null;
        }
    }

    private static IEnumerator UntilAllDead(List<Zombie> group)
    {
        while (AnyAlive(group))
        {
            yield return null;
        }
    }

    // ---- Small checks ----

    private static bool AnyAlive(List<Zombie> group)
    {
        foreach (Zombie zombie in group)
        {
            if (zombie != null && zombie.IsAlive)
            {
                return true;
            }
        }
        return false;
    }

    private static bool AnyWithin(List<Zombie> group, float metres)
    {
        foreach (Zombie zombie in group)
        {
            if (zombie != null && zombie.IsAlive && Flat(zombie.Position - spawner.PlayerPosition).magnitude <= metres)
            {
                return true;
            }
        }
        return false;
    }

    // Every zombie of the group still alive stands well inside the barrel's ring.
    private static bool AllNearBarrel(List<Zombie> group, Barrel barrel)
    {
        bool any = false;
        foreach (Zombie zombie in group)
        {
            if (zombie != null && zombie.IsAlive)
            {
                if (Flat(zombie.Position - barrel.Position).magnitude > Barrel.BlastRadius * 0.8f)
                {
                    return false;
                }
                any = true;
            }
        }
        return any;
    }

    // One of the group is inside the ring but already past the barrel (it is
    // about to walk out of it toward the player): hold now or never.
    private static bool LeavingRing(List<Zombie> group, Barrel barrel)
    {
        float barrelToPlayer = Flat(barrel.Position - spawner.PlayerPosition).magnitude;
        foreach (Zombie zombie in group)
        {
            if (zombie == null || !zombie.IsAlive)
            {
                continue;
            }
            float inside = Flat(zombie.Position - barrel.Position).magnitude;
            float toPlayer = Flat(zombie.Position - spawner.PlayerPosition).magnitude;
            if (inside <= Barrel.BlastRadius * 0.8f && toPlayer < barrelToPlayer - Barrel.BlastRadius * 0.5f)
            {
                return true;
            }
        }
        return false;
    }

    // True while the word being typed belongs to one of the group.
    private static bool IsTypingOneOf(List<Zombie> group)
    {
        if (typing == null || !typing.IsTypingWord)
        {
            return false;
        }
        Zombie target = typing.CurrentTarget as Zombie;
        return target != null && group.Contains(target);
    }

    private static Zombie Middle(List<Zombie> group)
    {
        foreach (Zombie zombie in group)
        {
            if (zombie != null && zombie.IsAlive)
            {
                return zombie;
            }
        }
        return null;
    }

    private static float DistanceToPath(Vector3 point, Vector3 from, Vector3 to)
    {
        Vector3 a = Flat(from), b = Flat(to), p = Flat(point);
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return (a + ab * t - p).magnitude;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static void Log(string phase)
    {
        AnalyticsEvent e = Analytics.NewEvent("guided_intro");
        e.Add("phase", phase);
        Analytics.Send(e);
    }
}

// One step of a power's "how": a key cap ("1", "TAB"), the arrow keys, or
// a word, each with a one-word label under it.
public struct GuideStep
{
    public string KeyText;
    public bool IsArrows;
    public string Label;

    public static GuideStep Key(string key, string label)
    {
        return new GuideStep { KeyText = key, Label = label };
    }

    public static GuideStep Arrows(string label)
    {
        return new GuideStep { IsArrows = true, Label = label };
    }

    public static GuideStep Text(string word, string label)
    {
        return new GuideStep { KeyText = word, Label = label };
    }
}
