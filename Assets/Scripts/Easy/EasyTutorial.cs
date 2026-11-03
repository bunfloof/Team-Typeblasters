using System;
using System.Collections;
using System.Linq;
using UnityEngine;

// One safe arena. Each tool is shown with the actual gameplay effect, then practised.
public class EasyTutorial : MonoBehaviour
{
    public static EasyTutorial Active { get; private set; }
    public int CurrentLesson { get; private set; }
    public bool IsDemonstrating { get; private set; }
    public bool Finished { get; private set; }
    public bool IsFinalWave { get; private set; }
    public bool LessonComplete => Finished;
    public bool AllowsTyping => !IsDemonstrating && !Finished &&
        (IsFinalWave || CurrentLesson == 1 || CurrentLesson == 2 && ShieldWord.Current != null ||
         CurrentLesson >= 4 && (used || CurrentLesson == 6 && Power.RocketLoaded));

    private static readonly string[] Tools = { "", "", "shield", "lure", "freeze", "frenzy", "rpg" };
    private static readonly string[] Names = { "", "", "SHIELD", "LURE BOMB", "FREEZE", "FRENZY", "RPG" };
    private static readonly string[] Keys = { "", "", "TAB", "1", "2", "3", "4" };
    private WaveSpawner spawner;
    private TutorialCoachView view;
    private bool used, blocked, scripted, replay;
    private float nextRefill;
    private Powers Power => GameManager.Instance.Powers;

    private void OnEnable() { Active = this; Powers.ActionPerformed += OnPower; }
    private void OnDisable()
    {
        Powers.ActionPerformed -= OnPower;
        if (Active == this) Active = null;
        if (view != null) Destroy(view.gameObject);
    }

    public bool AllowsPower(string item)
    {
        return scripted || !IsDemonstrating && !Finished && (IsFinalWave || CurrentLesson >= 2 && item == Tools[CurrentLesson]);
    }

    public bool AllowsTarget(ITypingTarget target)
    {
        return AllowsTyping && (CurrentLesson != 2 || target is ShieldWord);
    }

    public void ReplayDemo() { if (!IsFinalWave && !IsDemonstrating && CurrentLesson >= 3 && !Finished) replay = true; }

    private void OnPower(string action, string item)
    {
        if (IsFinalWave || IsDemonstrating || Finished || CurrentLesson < 2 || item != Tools[CurrentLesson]) return;

        if (action == "block" && item == "shield") blocked = true;
        if (action == (item == "lure" ? "throw" : item == "rpg" ? "fire" : "use")) used = true;
    }

    private void Execute(Action action)
    {
        scripted = true;
        try { action(); } finally { scripted = false; }
    }

    private void ResetStage()
    {
        GameManager.Instance.Typing.DropWord();
        TouchInput.ReleaseAll();
        spawner.ClearTrainingTargets();
        foreach (LureBomb bomb in FindObjectsByType<LureBomb>(FindObjectsSortMode.None)) Destroy(bomb.gameObject);
        foreach (Bullet bullet in FindObjectsByType<Bullet>(FindObjectsSortMode.None)) Destroy(bullet.gameObject);
        Power.ResetTrainingState();
        Power.SetScriptedAim(false, Vector2.zero);
        view.ShowDemoArrows(Vector2.zero);
        GameManager.Instance.GrantTrainingEnergy(25);
        used = blocked = false;
        nextRefill = Time.time + 3f;
    }

    private void SpawnGroup(bool demo)
    {
        bool pair = CurrentLesson == 4 || CurrentLesson == 5;
        string[] words = CurrentLesson == 5 ? new[] { "BUTTERFLY", "SUNSHINE" }
            : pair ? new[] { "GO", "STOP" } : new[] { "CAT", "DOG", "SUN" };
        for (int i = 0; i < words.Length; i++)
        {
            // The lure demo is long (hold, steer, throw): its zombies walk slowly, so
            // they are still out in front when the bomb lands and can be seen
            // turning toward it. Practice keeps the real pace.
            float speed = CurrentLesson == 3 ? (demo ? 0.5f : 1.4f) : CurrentLesson == 4 ? 0.7f : 0f;
            spawner.SpawnTrainingTarget(words[i], 12f, (i - (words.Length - 1) * 0.5f) * 1.4f, speed);
        }
    }

    private void Log(string phase)
    {
        AnalyticsEvent e = Analytics.NewEvent("tutorial_step");
        e.Add("step", CurrentLesson); e.Add("phase", phase); Analytics.Send(e);
    }

    public IEnumerator Run(WaveSpawner owner)
    {
        spawner = owner;
        view = TutorialCoachView.Create(this, owner.Hud);
        CurrentLesson = 1;
        ResetStage();
        view.Cue("TYPE THE WORD!", "KILL THE ENEMY", "", false);
        Log("practice");
        int kills = GameManager.Instance.Stats.Kills;
        while (GameManager.Instance.Stats.Kills == kills)
        {
            if (spawner.AliveZombies.Count == 0) spawner.SpawnTrainingTarget("CAT", 12f, 0f, 0f);
            yield return null;
        }
        yield return Success();

        CurrentLesson = 2;
        ResetStage(); Log("practice");
        Zombie attacker = spawner.SpawnTrainingTarget("WAIT", 8f, 0f, 0f);
        while (!Powers.IsShielded)
        {
            view.Cue("SHIELD", ShieldWord.Current == null ? "TAP TAB" : "TYPE THE SHIELD WORD!",
                ShieldWord.Current == null ? "TAB" : "", false);
            yield return null;
        }
        view.Cue("SHIELD UP", "", "", false); // done with Tab: no key card any more
        attacker.transform.position = owner.PlayerPosition + owner.CurrentEncounter.Facing * 4f;
        attacker.SetTrainingSpeed(4f);
        while (!blocked) yield return null;
        view.Cue("BLOCKED!", "", "", false);
        yield return new WaitForSeconds(1f);
        yield return Success();

        for (int lesson = 3; lesson <= 6; lesson++)
        {
            CurrentLesson = lesson;
            do
            {
                replay = false;
                yield return Demonstrate();
                ResetStage(); IsDemonstrating = false;
                SpawnGroup(false); Log("practice");
                int before = GameManager.Instance.Stats.Kills;
                int wanted = lesson == 4 || lesson == 5 ? 2 : 3;
                while (!used || GameManager.Instance.Stats.Kills - before < wanted || spawner.AliveZombies.Count > 0)
                {
                    if (replay) break;
                    if (Time.time >= nextRefill)
                    {
                        Power.RefillTrainingSupplies(); GameManager.Instance.GrantTrainingEnergy(25); nextRefill = Time.time + 3f;
                    }
                    if (used && (lesson == 4 && !Powers.IsFrozen || lesson == 5 && !Powers.IsFrenzy))
                        used = false;
                    if (spawner.AliveZombies.Count == 0 && GameManager.Instance.Stats.Kills - before < wanted)
                    {
                        used = false;
                        SpawnGroup(false);
                    }
                    string instruction = lesson == 3 ? LureInstruction()
                        : lesson < 5 ? "TAP " + Keys[lesson]
                        : lesson == 6 && Power.RocketLoaded ? "TYPE A WORD!" : "TAP " + Keys[lesson];
                    if (used && lesson == 4) instruction = "TYPE THE WORDS!";
                    if (used && lesson == 5) instruction = "TYPE THE SHORT WORDS!";
                    if (used && lesson == 6) instruction = "NICE SHOT!";
                    view.Cue("YOUR TURN", instruction, used && lesson == 6 ? "" : Keys[lesson], false);
                    yield return null;
                }
            } while (replay);
            yield return Success();
        }
        ResetStage();
        IsFinalWave = true;
        CurrentLesson = 7;
        view.gameObject.SetActive(false);
        owner.Hud.RestoreTutorialCombatHud();
        Log("final_wave");
        yield return owner.RunTutorialFinalWave();
        if (GameManager.Instance.State == GameState.Playing)
        {
            Log("complete");
            Finished = true;
            GameManager.Instance.WinGame();
        }
    }

    private IEnumerator Success()
    {
        Log("complete");
        spawner.ClearTrainingTargets();
        view.Success(CurrentLesson);
        yield return new WaitForSeconds(1f);
    }

    // The lure lesson's practice line: hold 1, aim (arrow keys, or drag on a phone), let go.
    private string LureInstruction()
    {
        if (Power.IsAiming)
        {
            return TouchControls.Active ? "DRAG TO AIM, THEN LET GO" : "AIM WITH THE ARROW KEYS, LET GO OF 1";
        }
        return TouchControls.Active ? "HOLD 1, DRAG TO AIM, LET GO" : "HOLD 1, AIM WITH ARROWS, LET GO";
    }

    // How long each lesson's demo lasts, roughly (for the demo progress bar).
    private static float DemoSeconds(int lesson)
    {
        switch (lesson)
        {
            case 3: return 1.5f + LureHoldSeconds + LureSteerSeconds + 5.5f;
            case 4: return 1.5f + 0.2f + 2.5f;
            case 5: return 1.5f + 0.18f + 2.5f;
            default: return 1.5f + 0.18f + 0.7f + 1f + 2.5f;
        }
    }

    // The lure demo, slow enough to follow: 1 is pressed and HELD (a bar fills
    // on its key card, the throw arc appears), the throw is turned right,
    // left and back to the middle (the arrow keys light up one at a time,
    // with short stops between), then 1 is let go and the bomb flies.
    private const float LureHoldSeconds = 1.0f;
    private const float LureSteerSeconds = 4.7f; // the sum of SteerSeconds
    private static readonly Vector2[] SteerArrows = { Vector2.right, Vector2.zero, Vector2.left, Vector2.zero, Vector2.right, Vector2.zero };
    private static readonly float[] SteerSeconds = { 1.0f, 0.35f, 1.7f, 0.35f, 0.7f, 0.6f }; // right 1.0 - left 1.7 + right 0.7 = straight again

    private IEnumerator SteerDemo()
    {
        for (int i = 0; i < SteerArrows.Length; i++)
        {
            Power.SetScriptedAim(true, SteerArrows[i]);
            view.ShowDemoArrows(SteerArrows[i]);
            yield return new WaitForSeconds(SteerSeconds[i]);
        }
        Power.SetScriptedAim(false, Vector2.zero);
        view.ShowDemoArrows(Vector2.zero);
    }

    private IEnumerator ActivateDemo(bool rocket)
    {
        view.Cue("HOW IT WORKS", Names[CurrentLesson], Keys[CurrentLesson], true);
        view.DemoPressed = true;
        Execute(() => Power.WeaponKeyDown(rocket));
        yield return new WaitForSeconds(.18f);
        Execute(() => Power.WeaponKeyUp(rocket));
        view.DemoPressed = false;
    }

    private IEnumerator Demonstrate()
    {
        IsDemonstrating = true;
        ResetStage(); SpawnGroup(true); Log("demo");
        view.Cue("HOW IT WORKS", Names[CurrentLesson], Keys[CurrentLesson], true);
        view.StartDemoTimer(DemoSeconds(CurrentLesson));
        yield return new WaitForSeconds(1.5f);
        if (CurrentLesson == 3)
        {
            bool thrown;
            do
            {
                // Hold 1, steer with the arrows (they light up), let go.
                view.Cue("HOW IT WORKS", "HOLD 1, AIM WITH ARROWS, LET GO", Keys[CurrentLesson], true);
                view.DemoPressed = true;
                Execute(Power.BeginLureAim);
                yield return new WaitForSeconds(LureHoldSeconds); // just holding: the arc is out
                yield return SteerDemo();
                thrown = Power.IsAiming;
                Execute(Power.ReleaseLureAim);
            } while (!thrown);
            view.Cue("HOW IT WORKS", Names[CurrentLesson], Keys[CurrentLesson], true);
            view.DemoPressed = false;
            yield return new WaitForSeconds(5.5f); // actual flight, attraction and explosion
        }
        else if (CurrentLesson == 4)
        {
            view.DemoPressed = true; Execute(Power.TryUseFreeze);
            yield return new WaitForSeconds(0.2f); view.DemoPressed = false;
            yield return new WaitForSeconds(2.5f); // watch walking enemies turn icy and stop
        }
        else
        {
            yield return ActivateDemo(CurrentLesson == 6);
            if (CurrentLesson == 6)
            {
                yield return new WaitForSeconds(0.7f);
                view.Cue("HOW IT WORKS", "THEN TYPE A WORD", "", true);
                Zombie target = spawner.AliveZombies.First(z => z != null && z.IsAlive);
                while (!target.IsWordComplete)
                {
                    target.AdvanceProgress();
                    Bullet.Fire(target, target.IsWordComplete, target.IsWordComplete && Power.ConsumeRocket());
                    yield return new WaitForSeconds(0.22f);
                }
            }
            yield return new WaitForSeconds(2.5f); // word transformation / real rocket area damage
        }
        view.DemoPressed = false;
    }

    public void ShowCompletion()
    {
        Finished = true;
        if (view != null) { view.gameObject.SetActive(true); view.Complete(); }
    }

    private void LateUpdate()
    {
        if (view == null || spawner == null) return;
        bool playing = GameManager.Instance.State == GameState.Playing;
        view.gameObject.SetActive(!IsFinalWave && playing || Finished);
        foreach (Zombie zombie in spawner.AliveZombies)
            if (zombie != null && zombie.Label != null && CurrentLesson == 2) zombie.Label.enabled = false;
    }
}
