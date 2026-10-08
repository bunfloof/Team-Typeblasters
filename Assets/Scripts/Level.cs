// Level.cs
// ---------------------------------------------------------------------------
// The level: where the player starts and the fights, in the order they are
// played. Lives on the "Level" object in the scene; WaveSpawner reads it.
//
// THE MAP is the real USC School of Cinematic Arts block in Los Angeles, the
// whole block between W 34th St (north), Watt Way (east), W 35th St (south)
// and McClintock Ave (west). It is built from plain boxes placed in the scene
// under "Map" (one group per building: SCI, SCB, SCC, SCA and its courtyard,
// SCX, SCE, the park, the ground and the buildings across the streets).
// Only the layout is copied (footprints, heights, openings); there is no
// detail, no text and no colour on purpose (greybox). Footprints came from
// OpenStreetMap and the LA County roof outlines, the SCA courtyard from the
// USC ground-floor plan. One metre in the game is one metre on the block.
// The map's objects are marked Static; their colours are the materials in
// Assets/Materials/Map.
//
// THE FIGHTS are under "Level > Fights" (see Encounter.cs), in this order:
//   1. W 34th St at McClintock: SCI's front door, the SCI|SCB palm walk
//   2. The Cinematic Arts Park, seen from the Muybridge plaza
//   3. The lane between the SCC stages and the Spielberg building
//   4. Watt Way at the Lucas building's east portico
//   5. Boss: the SCA courtyard, entered through the Main Gates
//
// HOW TO CHANGE IT: move things in the Scene view. Zombies walk in straight
// lines from a spawn point to its Exit, then straight at the player, so keep
// those lines (red in the Scene view) free of buildings. Keep every spawn
// Exit, barrel and crate within about 33 degrees of the direction the player
// faces at the stop (doors within about 40): the screen edge is only about
// 45 degrees to each side, and the view turns up to 12 degrees toward the
// word being typed.
//
// The route guide is the faint arrows on the ground under "Level > Route Guide"
// (RouteArrow prefab, one every 3.2 m). If you move a route point, move its
// arrows too. In the Scene view the run is drawn as a cyan line.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)] // ready before WaveSpawner starts (it reads Encounters in its Awake)
public class Level : MonoBehaviour
{
    [SerializeField] private Transform start;        // where the player stands before the first run; blue arrow = facing
    [SerializeField] private Encounter[] encounters; // the fights, in the order they are played

    // The fights, in the order they are played. Filled in Awake.
    public List<Encounter> Encounters { get; private set; }

    // Where the player stands (on the ground) and faces before the first run.
    public Vector3 StartPosition
    {
        get { return start.position; }
    }

    public Vector3 StartFacing
    {
        get
        {
            Vector3 forward = start.forward;
            forward.y = 0f;
            return forward.normalized;
        }
    }

    private void Awake()
    {
        Encounters = new List<Encounter>();
        foreach (Encounter encounter in encounters)
        {
            if (encounter != null)
            {
                Encounters.Add(encounter);
            }
        }
    }

    // Only drawn in the Editor's Scene view, never in the game: the whole run
    // in cyan, from the start (with an arrow where the player first faces)
    // through every fight's route points to its stop.
    private void OnDrawGizmos()
    {
        if (start == null || encounters == null)
        {
            return;
        }
        Vector3 up = Vector3.up * 0.2f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start.position + up, start.position + up + StartFacing * 4f);

        // The run as the player really rides it: the corners rounded into arcs
        // (RailMover.RoundCorners, with the Player's Corner Radius).
        RailMover rail = FindFirstObjectByType<RailMover>();
        float radius = rail != null ? rail.CornerRadius : 0f;
        Vector3 from = start.position;
        foreach (Encounter encounter in encounters)
        {
            if (encounter == null)
            {
                continue;
            }
            List<Vector3> corners = new List<Vector3> { Flat(from) };
            foreach (Vector3 point in encounter.Route)
            {
                if (Vector3.Distance(corners[corners.Count - 1], Flat(point)) >= 0.05f)
                {
                    corners.Add(Flat(point));
                }
            }
            List<Vector3> run = radius > 0f ? RailMover.RoundCorners(corners, radius) : corners;
            for (int i = 0; i + 1 < run.Count; i++)
            {
                Gizmos.DrawLine(run[i] + up, run[i + 1] + up);
            }
            from = corners[corners.Count - 1];
        }
    }

    // The point on the ground (y = 0).
    private static Vector3 Flat(Vector3 point)
    {
        point.y = 0f;
        return point;
    }
}
