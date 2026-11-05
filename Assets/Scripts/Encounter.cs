// Encounter.cs
// ---------------------------------------------------------------------------
// One fight in the level: a place where the player stops and zombies come at
// them (Typing of the Dead style). Every fight is an object under
// "Level > Fights" in the scene, and Level plays them in the order of its
// Encounters list. For each fight, WaveSpawner:
//
//   1. runs the player through the Route points to the Stop, and turns them
//      to face where the Stop's blue arrow (forward) points,
//   2. starts the fight: zombies come out of this fight's spawn points (or the
//      boss appears at Boss Stand), and its barrels and crates can be typed,
//   3. when every zombie is dead, opens the Exit Gate: the run goes on.
//
// Everything under this object with a SpawnPoint, Barrel or SupplyCrate
// component belongs to this fight. To add a barrel, drag the Barrel prefab
// onto this object in the Hierarchy and move it where you want it.
//
// In the Scene view, a fight draws itself (see OnDrawGizmos): the stop and
// where the player faces in green, each zombie's walk to the player in red,
// and the boss's space in magenta. Level draws the run between fights.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class Encounter : MonoBehaviour
{
    [Header("The run to this fight")]
    [SerializeField] private Transform route;    // its children are the points to run through, in order
    [SerializeField] private Transform stop;     // where the player stops; its blue arrow is where they face

    [Header("Zombies")]
    [SerializeField] private int groupSize = 3;  // how many zombies come out of one spawn point together

    [Header("After the fight")]
    [SerializeField] private Gate exitGate;      // sinks into the ground when the fight is won (empty = none)
    [SerializeField] private bool openGateFromStart; // the gate is down from the start and zombies come through it (the player does not move during a fight anyway)

    [Header("Boss fight")]
    [SerializeField] private bool isBossFight;
    [SerializeField] private Transform bossStand; // where the boss stands (it turns to face the player)

    private const float GizmoHeight = 0.2f;      // lines are drawn a little above the ground

    // The points to run through. The LAST one is the stop.
    public List<Vector3> Route
    {
        get
        {
            List<Vector3> points = new List<Vector3>();
            if (route != null)
            {
                foreach (Transform point in route) // the children of Route, in Hierarchy order
                {
                    points.Add(point.position);
                }
            }
            if (stop != null)
            {
                points.Add(stop.position);
            }
            return points;
        }
    }

    // Where the player stands while fighting here.
    public Vector3 StopPosition
    {
        get { return stop != null ? stop.position : transform.position; }
    }

    // The direction the player faces while fighting here (flat on the ground).
    public Vector3 Facing
    {
        get
        {
            if (stop == null)
            {
                return Vector3.forward;
            }
            Vector3 forward = stop.forward;
            forward.y = 0f;
            return forward.normalized;
        }
    }

    // Where this fight's zombies come from (every SpawnPoint under this object).
    public List<SpawnPoint> SpawnPoints
    {
        get { return new List<SpawnPoint>(GetComponentsInChildren<SpawnPoint>()); }
    }

    // This fight's barrels and supply crates. They can only be typed during the fight.
    public List<Barrel> Barrels
    {
        get { return new List<Barrel>(GetComponentsInChildren<Barrel>()); }
    }

    public List<SupplyCrate> Crates
    {
        get { return new List<SupplyCrate>(GetComponentsInChildren<SupplyCrate>()); }
    }

    // openGateFromStart: the gate goes down at once and a spawn point is made
    // in the passage behind it (on the far side from the stop), so this
    // fight's zombies also come through it. Called by WaveSpawner.Awake.
    public void OpenGateAsSpawn()
    {
        if (!openGateFromStart || exitGate == null)
        {
            return;
        }
        exitGate.OpenAtOnce();
        Transform gate = exitGate.transform;
        Vector3 through = gate.forward;
        through.y = 0f;
        through.Normalize();
        if (Vector3.Dot(gate.position - StopPosition, through) < 0f)
        {
            through = -through; // "behind" = away from the player
        }
        GameObject spawn = new GameObject("Through the gate");
        spawn.transform.SetParent(transform, false);
        spawn.transform.position = new Vector3(gate.position.x, 0f, gate.position.z) + through * 10f;
        GameObject exitPoint = new GameObject("Exit");
        exitPoint.transform.SetParent(spawn.transform, false);
        exitPoint.transform.position = new Vector3(gate.position.x, 0f, gate.position.z) - through * 1.8f;
        spawn.AddComponent<SpawnPoint>().Configure(exitPoint.transform, 0.4f);
    }

    public int GroupSize
    {
        get { return groupSize; }
    }

    public Gate ExitGate
    {
        get { return exitGate; }
    }

    public bool IsBossFight
    {
        get { return isBossFight; }
    }

    public Vector3 BossStand
    {
        get { return bossStand.position; }
    }

    // Only drawn in the Editor's Scene view, never in the game.
    private void OnDrawGizmos()
    {
        if (stop == null)
        {
            return;
        }
        Vector3 up = Vector3.up * GizmoHeight;

        // The stop, and an arrow where the player faces.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(stop.position + up, 0.75f);
        Gizmos.DrawLine(stop.position + up, stop.position + up + Facing * 6f);

        // Each zombie's walk from its exit to the player (the walk from the
        // spawn point to its exit is drawn by the SpawnPoint).
        Gizmos.color = Color.red;
        foreach (SpawnPoint point in GetComponentsInChildren<SpawnPoint>())
        {
            Gizmos.DrawLine(point.Exit + up, stop.position + up);
        }

        // The boss's space: about 6 m wide, 2 m deep and 7.5 m tall.
        if (isBossFight && bossStand != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(bossStand.position + Vector3.up * 3.75f, new Vector3(6f, 7.5f, 2f));
        }
    }
}
