// SpawnPoint.cs
// ---------------------------------------------------------------------------
// A place zombies come out of: a doorway, the end of a walk, an arcade...
// A zombie appears where this object is (out of sight, for example just
// inside a building), walks to Exit (just outside), then heads straight for
// the player. If the spawn point has a Door, it bursts open when the first
// zombie comes out.
//
// Put spawn points under a fight (an Encounter): they belong to that fight.
// Usual setup in the Hierarchy:
//   SCI front door      (this component, placed just inside the building)
//     Exit              (an empty object just outside the door)
//     Door              (the Door prefab, on the building's face)
//
// Keep the straight line from Exit to the fight's stop free of walls, because
// zombies walk straight (they do not path-find). The Scene view draws these
// walks as red lines.
// ---------------------------------------------------------------------------
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private Transform exit;        // zombies walk here first, then straight at the player
    [SerializeField] private Door door;             // bursts open when the first zombie comes out (empty = no door)
    [SerializeField] private float spread = 0.4f;   // random sideways offset (metres) so a pack does not stack up

    // Set up in code (Encounter.OpenGateAsSpawn): where its zombies walk first.
    public void Configure(Transform exitPoint, float sideSpread)
    {
        exit = exitPoint;
        door = null;
        spread = sideSpread;
    }

    // Where zombies appear (on the ground).
    public Vector3 Position
    {
        get { return transform.position; }
    }

    // Where they walk first.
    public Vector3 Exit
    {
        get { return exit != null ? exit.position : transform.position; }
    }

    public Door Door
    {
        get { return door; }
    }

    public float Spread
    {
        get { return spread; }
    }

    // Only drawn in the Editor's Scene view, never in the game.
    private void OnDrawGizmos()
    {
        Vector3 up = Vector3.up * 0.2f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Position + up, 0.3f);
        Gizmos.DrawLine(Position + up, Exit + up);
        Gizmos.DrawSphere(Exit + up, 0.15f);
    }
}
