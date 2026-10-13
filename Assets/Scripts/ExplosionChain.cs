// ExplosionChain.cs
// ---------------------------------------------------------------------------
// Groups the explosions of one chain reaction (a barrel sets off a red zombie,
// which sets off another barrel...) so that all their kills are scored
// together as ONE multi-kill when the last explosion is done.
//
// How it is used (you normally never touch it yourself, Explosion does):
//   - Begin() is called when an explosion is scheduled (before its short
//     chain delay), so the chain knows one more explosion is coming.
//   - Explosion.Detonate calls End() when that explosion has done its damage,
//     and adds every enemy it killed to Kills.
//   - When nothing is pending any more, the chain is over: Kills are scored
//     ONCE with GameManager.Instance.AddKills(Kills), so 2+ kills show the
//     multi-kill popup ("TRIPLE KILL!") and count as the biggest blast. The
//     points also pop up where the chain started ("+600").
//
// Example: the player shoots a barrel. The barrel calls Begin() (Pending = 1),
// its explosion kills 2 zombies, one of them red: Begin() again (Pending = 2),
// then End() (Pending = 1). 0.15 s later the red zombie's explosion kills 3
// more and calls End() (Pending = 0): the chain scores 5 kills at once.
//
// It is plain data (no MonoBehaviour): create one with "new ExplosionChain()".
// ---------------------------------------------------------------------------
using UnityEngine;

public class ExplosionChain
{
    public int Kills;                        // enemies killed by this chain so far
    public int Pending { get; private set; } // explosions scheduled but not finished yet

    // ---- Analytics ----
    // The chain's "blast" event, started by whatever set it off (Zombie: a
    // red zombie's word, Barrel: a barrel shot, LureBomb, Bullet: an RPG
    // rocket) with its own details. When the chain is over, what it did is
    // added (kills by kind, explosions, barrels, boss damage) and it is sent.
    // null = not logged.
    public AnalyticsEvent Report;
    public float BossDamage;                 // damage dealt to the boss by the whole chain
    private int explosions;
    private int barrels;
    private readonly int[] killsByKind = new int[System.Enum.GetValues(typeof(ZombieKind)).Length];

    // Called by Explosion for every zombie it kills.
    public void NoteKill(Zombie zombie)
    {
        Kills += 1;
        killsByKind[(int)zombie.Kind] += 1;
    }

    // Called by Barrel for every barrel of this chain that goes off.
    public void NoteBarrel()
    {
        barrels += 1;
    }

    // True once the chain has been scored. It never scores twice.
    public bool IsFinished { get; private set; }

    // Where the chain started (its first explosion); the points pop up there.
    public Vector3 Origin { get; private set; }
    private bool hasOrigin;

    // One more explosion of this chain is on its way.
    public void Begin()
    {
        Pending += 1;
    }

    // Called by Explosion.Detonate for every explosion; only the first one is remembered.
    public void NoteExplosion(Vector3 position)
    {
        explosions += 1;
        if (!hasOrigin)
        {
            Origin = position;
            hasOrigin = true;
        }
    }

    // One explosion of this chain has done its damage. When it was the last
    // one, the chain scores all its kills together.
    public void End()
    {
        if (IsFinished)
        {
            return; // already scored: a stray extra End() must not score again
        }

        Pending -= 1;
        if (Pending > 0)
        {
            return; // more explosions are still coming
        }

        Pending = 0;
        IsFinished = true;

        // AddKills(0) does nothing, so a blast that hit nobody is fine.
        // Not after a lost game: the "You died" screen already shows the score.
        GameManager gameManager = GameManager.Instance;
        bool scored = gameManager != null && gameManager.State != GameState.Lost;
        int points = 0;
        if (scored)
        {
            points = gameManager.AddKills(Kills);
            if (points > 0 && hasOrigin)
            {
                ShowPoints(points);
            }
        }
        SendReport(scored, points);
    }

    // Finishes and sends the "blast" analytics event (see Report). The kills
    // include a red zombie that set the chain off (it dies in its own blast).
    private void SendReport(bool scored, int points)
    {
        if (Report == null)
        {
            return;
        }
        Report.Add("kills", Kills);
        for (int kind = 0; kind < killsByKind.Length; kind++)
        {
            Report.Add("kills_" + ((ZombieKind)kind).ToString().ToLowerInvariant(), killsByKind[kind]);
        }
        Report.Add("explosions", explosions);
        Report.Add("barrels", barrels);
        Report.Add("boss_damage", BossDamage);
        Report.Add("scored", scored);
        Report.Add("points", points);
        if (hasOrigin)
        {
            Report.Add("x", Origin.x); // where the chain started, on the map
            Report.Add("z", Origin.z);
        }
        Analytics.AddPlayerState(Report);
        Analytics.Send(Report);
        Report = null;
    }

    // "+600" rising above the place where the chain started (like a bullet kill's points).
    private void ShowPoints(int points)
    {
        WaveSpawner spawner = Object.FindFirstObjectByType<WaveSpawner>();
        if (spawner != null && spawner.Hud != null)
        {
            spawner.Hud.ShowFloatingText(Origin + Vector3.up * 2f, "+" + points, Palette.WordBarrel);
        }
    }
}
