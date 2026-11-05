// GuidedIntro.cs
// ---------------------------------------------------------------------------
// The NORMAL level's first wave teaches the game (there is no separate
// tutorial level). The HUD starts empty (HudReveal) and each thing is shown
// the first time it is needed: the world is HELD for a moment (zombies wait,
// everything else runs) while one short message says what it is, and the
// player uses it for real. No demos.
//
//   ride     "You move on your own. Just type."  ENTER skips the whole intro
//            (only now; wave 1 is then a normal wave)
//   A        the first zombie: type its word to shoot it
//   B        HEALTH, SCORE (+ wave), COMBO, ENERGY appear, one at a time
//   C  [1]   LURE BOMB: hold 1, aim with the arrow keys, let go
//   D  [2]   FREEZE        E  [3] FRENZY        F  [4] RPG (load, then fire)
//   G        a RED zombie: it explodes when shot
//   H  [TAB] SHIELD: press Tab, type the word
//   end      everything is shown; the rest of the wave plays normally
//
// While a message waits for a power key, words cannot be typed (they are
// ignored, never wrong; the key on screen bounces) and the power's energy is
// topped up, so a step can never get stuck. Enter (or a tap on the message)
// continues the read-only messages; they also go on by themselves.
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

    // True while the world is held for a message (Zombie.Update waits).
    public static bool Holding { get; private set; }

    // True while a message waits for a power key: words wait too (TypingController).
    public static bool BlocksLetters { get; private set; }

    private const float ReadSeconds = 8f;    // a read-only message goes on by itself after this
    private const float WalkOutSeconds = 1.8f; // a new zombie walks out of its door before the hold

    private const float SkipArmSeconds = 1f;  // Enter is not a skip in the first second (a double press to start the level)
    private const float CloseEnough = 9f;     // metres: a group must come this close before a blast is taught
                                              // (a quick tap throws the lure bomb 9 m straight ahead)

    private static bool skipOffered;
    private static float skipArmedAt;
    private static bool waitingContinue;
    private static bool continued;
    private static WaveSpawner spawner;
    private static HUD hud;
    private static GuidedIntroView view;
    private static readonly HashSet<string> powerActions = new HashSet<string>(); // "lure:throw", "rpg:fire"...

    // ---- Called by the game ----

    // WaveSpawner, when the level starts (before the first ride).
    public static void Begin(WaveSpawner owner, HUD hudRef)
    {
        spawner = owner;
        hud = hudRef;
        Running = true;
        Holding = false;
        BlocksLetters = false;
        skipOffered = true;
        skipArmedAt = Time.unscaledTime + SkipArmSeconds;
        waitingContinue = false;
        powerActions.Clear();
        HudReveal.HideAll();
        Powers.ActionPerformed -= OnPower;
        Powers.ActionPerformed += OnPower;
        view = GuidedIntroView.Create(hudRef);
        view.ShowCard(CardPlace.Center, "", "You move on your own. Just type.", null, false);
        view.ShowSkip(true);
        Log("start");
    }

    // TypingController: Enter. Skips the intro while that is offered, else
    // continues a read-only message. (A tap on the message or the skip
    // button does the same, see GuidedIntroView.)
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
        else if (waitingContinue)
        {
            continued = true;
        }
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
        waitingContinue = false;
        Powers.ActionPerformed -= OnPower;
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
        waitingContinue = false;
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
    }

    // ---- Wave 1, guided (WaveSpawner runs it in place of a normal wave 1) ----

    public static IEnumerator RunGuidedWave()
    {
        skipOffered = false; // from here on the player is in
        view.ShowSkip(false);
        view.HideCard();
        Log("guided_wave");

        // A. The first zombie: type its word.
        Zombie first = spawner.SpawnGuided(0, ZombieKind.Normal);
        yield return new WaitForSeconds(WalkOutSeconds);
        Holding = true;
        view.ShowCard(CardPlace.Center, "TYPE TO SHOOT", "Type the word above a zombie to shoot it.", null, false);
        view.PointAt(first);
        yield return UntilDead(first);
        view.PointAt(null);
        view.HideCard();
        Holding = false;
        yield return new WaitForSeconds(0.6f);

        // B. What is on the screen, one piece at a time.
        Holding = true;
        yield return Explain(HudFeature.Health, CardPlace.TopLeft, "HEALTH", "Zombies that reach you bite.");
        HudReveal.Reveal(HudFeature.Wave);
        yield return Explain(HudFeature.Score, CardPlace.TopRight, "SCORE", "Every kill adds to it.");
        yield return Explain(HudFeature.Combo, CardPlace.TopCenter, "COMBO", "Multiplies your score. A wrong key lowers it.");
        yield return Explain(HudFeature.Energy, CardPlace.TopCenter, "ENERGY", "+1 for every word you finish. Spend it on POWERS.");
        Holding = false;

        // C. [1] LURE BOMB: hold, aim with the arrows, let go.
        List<Zombie> group = Group(1, ZombieKind.Normal, 3);
        yield return UntilClose(group); // near enough to walk to the bomb before it goes off
        yield return OfferPower(HudFeature.Lure, "lure", "throw", Powers.LureCost,
            "LURE BOMB", "Pulls zombies in, then explodes.",
            new[] { GuideStep.Key("1", "HOLD"), GuideStep.Arrows("AIM"), GuideStep.Key("1", "LET GO") });
        yield return UntilAllDead(group);

        // D. [2] FREEZE.
        group = new List<Zombie> { spawner.SpawnGuided(2, ZombieKind.Runner, -0.8f), spawner.SpawnGuided(2, ZombieKind.Runner, 0.8f) };
        yield return new WaitForSeconds(WalkOutSeconds);
        yield return OfferPower(HudFeature.Freeze, "freeze", "use", Powers.FreezeCost,
            "FREEZE", "Stops every zombie for a few seconds.",
            new[] { GuideStep.Key("2", "PRESS") });
        yield return UntilAllDead(group);

        // E. [3] FRENZY: words far too long, made tiny.
        group = new List<Zombie> { spawner.SpawnGuided(3, ZombieKind.Normal, 0f, true), spawner.SpawnGuided(0, ZombieKind.Normal, 0f, true) };
        yield return new WaitForSeconds(WalkOutSeconds + 0.4f);
        yield return OfferPower(HudFeature.Frenzy, "frenzy", "use", Powers.FrenzyCost,
            "FRENZY", "Every word turns tiny for a few seconds.",
            new[] { GuideStep.Key("3", "PRESS") });
        yield return UntilAllDead(group);

        // F. [4] RPG: load it, then any finished word fires it into the group.
        group = Group(1, ZombieKind.Normal, 3);
        yield return UntilClose(group);
        yield return OfferPower(HudFeature.Rpg, "rpg", "use", Powers.RocketCost,
            "RPG", "Loads a rocket. Your next word fires it.",
            new[] { GuideStep.Key("4", "PRESS"), GuideStep.Text("TYPE", "A WORD") });
        view.ShowCard(CardPlace.Center, "FIRE!", "Finish any word: the rocket blasts the whole group.", null, false);
        view.PointAt(Middle(group));
        while (!powerActions.Contains("rpg:fire") && AnyAlive(group))
        {
            yield return null;
        }
        view.PointAt(null);
        view.HideCard();
        yield return UntilAllDead(group);

        // G. A RED zombie in a group.
        Zombie red = spawner.SpawnGuided(2, ZombieKind.Explosive);
        group = new List<Zombie> { red, spawner.SpawnGuided(2, ZombieKind.Normal, -1.6f), spawner.SpawnGuided(2, ZombieKind.Normal, 1.6f) };
        yield return UntilClose(group);
        Holding = true;
        view.ShowCard(CardPlace.Center, "RED ZOMBIE", "Explodes when shot. Everything in its ring dies too.", null, true);
        view.PointAt(red);
        yield return WaitContinue();
        view.PointAt(null);
        view.HideCard();
        Holding = false;
        yield return UntilAllDead(group);

        // H. [TAB] SHIELD: a fast one comes close.
        Zombie runner = spawner.SpawnGuided(3, ZombieKind.Runner);
        float giveUp = Time.time + 4f;
        while (runner != null && runner.IsAlive && Time.time < giveUp && Distance(runner) > 8f)
        {
            yield return null;
        }
        GameManager.Instance.Powers.RefillTrainingSupplies(); // a shield charge to practise with
        Holding = true;
        HudReveal.Reveal(HudFeature.Shield);
        view.ShowCard(CardPlace.Powers, "SHIELD", "Blocks all damage for a few seconds.",
            new[] { GuideStep.Key("TAB", "PRESS"), GuideStep.Text("TYPE", "THE WORD") }, false);
        while (!Powers.IsShielded)
        {
            if (ShieldWord.Current == null && GameManager.Instance.Powers.ShieldCharges == 0)
            {
                GameManager.Instance.Powers.RefillTrainingSupplies(); // a missed word can be tried again
            }
            yield return null;
        }
        view.HideCard();
        Holding = false;
        GameManager.Instance.Powers.RefillTrainingSupplies(); // the level's own shield is still there
        if (runner != null)
        {
            yield return UntilDead(runner);
        }

        // Everything has been shown: normal play from here.
        Log("complete");
        End();
        while (spawner.AliveZombies.Count > 0)
        {
            yield return null;
        }
    }

    // ---- Steps ----

    // A read-only message about a piece of the HUD, which pops in now.
    private static IEnumerator Explain(HudFeature feature, CardPlace place, string title, string body)
    {
        HudReveal.Reveal(feature);
        view.ShowCard(place, title, body, null, true);
        yield return WaitContinue();
        view.HideCard();
        yield return new WaitForSeconds(0.2f);
    }

    // A power appears and the world waits until it is used ('done' is the
    // Powers action: "throw" for the lure bomb, "use" for the others).
    private static IEnumerator OfferPower(HudFeature feature, string item, string done, int cost,
        string title, string body, GuideStep[] steps)
    {
        powerActions.Remove(item + ":" + done);
        Holding = true;
        BlocksLetters = true;
        GameManager.Instance.GrantTrainingEnergy(cost);
        HudReveal.Reveal(feature);
        view.ShowCard(CardPlace.Powers, title, body, steps, false);
        while (!powerActions.Contains(item + ":" + done))
        {
            GameManager.Instance.GrantTrainingEnergy(cost); // never stuck for lack of energy
            yield return null;
        }
        view.HideCard();
        BlocksLetters = false;
        Holding = false;
    }

    private static IEnumerator WaitContinue()
    {
        waitingContinue = true;
        continued = false;
        float until = Time.time + ReadSeconds;
        while (!continued && Time.time < until)
        {
            yield return null;
        }
        waitingContinue = false;
    }

    // ---- Helpers ----

    // 'count' zombies side by side out of one spawn point.
    private static List<Zombie> Group(int point, ZombieKind kind, int count)
    {
        List<Zombie> group = new List<Zombie>();
        for (int i = 0; i < count; i++)
        {
            float lane = (i - (count - 1) * 0.5f) * 1.4f;
            Zombie zombie = spawner.SpawnGuided(point, kind, lane == 0f ? 0.01f : lane);
            if (zombie != null)
            {
                group.Add(zombie);
            }
        }
        return group;
    }

    // Until one of the group is within CloseEnough (at most 9 s), so a blast
    // taught now has them in reach.
    private static IEnumerator UntilClose(List<Zombie> group)
    {
        yield return new WaitForSeconds(WalkOutSeconds);
        float giveUp = Time.time + 9f;
        while (Time.time < giveUp && AnyAlive(group))
        {
            foreach (Zombie zombie in group)
            {
                if (zombie != null && zombie.IsAlive && Distance(zombie) <= CloseEnough)
                {
                    yield break;
                }
            }
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

    private static Zombie Middle(List<Zombie> group)
    {
        return group.Count > 0 ? group[group.Count / 2] : null;
    }

    private static float Distance(Zombie zombie)
    {
        Vector3 offset = zombie.Position - spawner.PlayerPosition;
        offset.y = 0f;
        return offset.magnitude;
    }

    private static void Log(string phase)
    {
        AnalyticsEvent e = Analytics.NewEvent("guided_intro");
        e.Add("phase", phase);
        Analytics.Send(e);
    }
}

// Where a message card sits: next to what it explains.
public enum CardPlace
{
    Center,
    TopLeft,     // under the health bar
    TopCenter,   // under the combo / energy plate
    TopRight,    // under the score
    Powers       // beside the powers (bottom-left)
}

// One step of a power's "how": a key cap ("1", "TAB"), the arrow keys, or
// two words, each with a one-word label under it.
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
