// RailMover.cs
// ---------------------------------------------------------------------------
// Moves the Player rig through the level like a camera on rails (Typing of the
// Dead style). There is no mouse look and no WASD: the player's hands stay on
// the keyboard. Lives on the "Player" object; the Main Camera is its child, so
// the camera rides along (CameraDirector adds the head movement on top).
//
//   RideAlong(route, facing) - ride through the route's points (speeding up and
//                              slowing down smoothly, turning at corners), stop
//                              at the last point and turn to face "facing".
//   IsStopped                - true once it has arrived AND turned: time to fight.
//   SpeedCap, PointAhead     - used by WaveSpawner for ambushes on the run.
// A long run is ridden faster (longRunSeconds, maxRailSpeed).
//
// WaveSpawner decides where to ride (from the Level's encounters).
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class RailMover : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float railSpeed = 9f;           // metres per second between fights (a run)
    // A LONG run goes faster so it never drags: its speed is raised so that it
    // takes about longRunSeconds (but never above maxRailSpeed). Runs shorter
    // than railSpeed x longRunSeconds (108 m) keep railSpeed.
    [SerializeField] private float longRunSeconds = 12f;
    [SerializeField] private float maxRailSpeed = 14f;
    [SerializeField] private float accelerationSeconds = 0.8f; // time to reach full speed (and to stop)
    [SerializeField] private float turnSpeed = 160f;         // degrees per second the body turns
    [SerializeField] private float cornerLookAhead = 5f;     // start turning this many metres before a corner
    // Every corner of a route is ridden as a smooth ARC of this radius (metres),
    // not a sharp turn on the spot, so the view sweeps round the corner and
    // whatever is behind it comes into view gradually. A short leg gets a
    // tighter arc (each arc uses at most 45% of the legs next to it).
    [SerializeField] private float cornerRadius = 6f;

    private readonly List<Vector3> route = new List<Vector3>();
    private int nextPoint;          // index in route of the point we are heading to
    private Vector3 finalFacing = Vector3.forward;
    private float currentSpeed;
    private float cruiseSpeed;      // top speed of this run (railSpeed, or more for a long run)
    private bool riding;

    // True while the player is moving between two fights.
    public bool IsRiding
    {
        get { return riding; }
    }

    // While an ambush zombie is in the road (WaveSpawner), the run slows down to
    // this many metres per second so there is time to type its word.
    // float.MaxValue = no limit.
    public float SpeedCap { get; set; } = float.MaxValue;

    // Metres still to ride along the route.
    public float RemainingDistance
    {
        get { return riding ? DistanceLeft() : 0f; }
    }

    // The way the player is riding right now (flat, normalised).
    public Vector3 Heading
    {
        get { return riding ? Flat(RideDirection()) : finalFacing; }
    }

    // The point on the route 'metres' ahead of the player (the stop if the
    // route ends sooner).
    public Vector3 PointAhead(float metres)
    {
        Vector3 from = transform.position;
        for (int i = nextPoint; i < route.Count; i++)
        {
            float leg = FlatDistance(from, route[i]);
            if (metres <= leg)
            {
                Vector3 point = Vector3.MoveTowards(Flat0(from), Flat0(route[i]), metres);
                return point;
            }
            metres -= leg;
            from = route[i];
        }
        return Flat0(from);
    }

    private static Vector3 Flat0(Vector3 point)
    {
        point.y = 0f;
        return point;
    }

    // True once the last ride is over and the body faces the fight.
    public bool IsStopped
    {
        get { return !riding && Vector3.Angle(transform.forward, finalFacing) < 1f; }
    }

    // Puts the player somewhere right away (the start of the level).
    public void PlaceAt(Vector3 position, Vector3 facing)
    {
        transform.position = position;
        finalFacing = Flat(facing);
        transform.rotation = Quaternion.LookRotation(finalFacing);
        riding = false;
        currentSpeed = 0f;
    }

    // The radius of the arcs at the corners (Level draws the run with it in the Scene view).
    public float CornerRadius
    {
        get { return cornerRadius; }
    }

    // Metres ridden since the current run started (RideAlong).
    public float Travelled { get; private set; }

    // Starts riding through points; at the end, turns to face facingAtEnd.
    // The corners are rounded into arcs (cornerRadius).
    public void RideAlong(List<Vector3> points, Vector3 facingAtEnd)
    {
        // Where we stand, then the points (any point we are already on is skipped).
        List<Vector3> corners = new List<Vector3> { Flat0(transform.position) };
        foreach (Vector3 point in points)
        {
            if (FlatDistance(corners[corners.Count - 1], point) >= 0.05f)
            {
                corners.Add(Flat0(point));
            }
        }

        route.Clear();
        List<Vector3> rounded = RoundCorners(corners, cornerRadius);
        for (int i = 1; i < rounded.Count; i++) // [0] is where we stand
        {
            route.Add(rounded[i]);
        }
        finalFacing = Flat(facingAtEnd);
        nextPoint = 0;
        Travelled = 0f;
        riding = route.Count > 0;

        // A long run is ridden faster (see longRunSeconds).
        cruiseSpeed = Mathf.Clamp(DistanceLeft() / Mathf.Max(1f, longRunSeconds), railSpeed, Mathf.Max(railSpeed, maxRailSpeed));
    }

    private void Update()
    {
        // Only ride while the game is being played.
        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        if (riding)
        {
            Ride();
        }

        // Turn the body: toward the road ahead while riding, toward the fight when stopped.
        Vector3 wanted = riding ? RideDirection() : finalFacing;
        if (wanted.sqrMagnitude > 0.001f)
        {
            // Ease toward the wanted direction (no hard starts or stops), but
            // never faster than turnSpeed.
            Quaternion target = Quaternion.LookRotation(wanted);
            Quaternion eased = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-TurnSmoothing * Time.deltaTime));
            transform.rotation = Quaternion.RotateTowards(transform.rotation, eased, turnSpeed * Time.deltaTime);
        }
    }

    private void Ride()
    {
        // Speed up at the start and slow down so that we stop exactly at the end:
        // the speed from which we can still brake in the distance left is
        // sqrt(2 x deceleration x distance).
        float topSpeed = Mathf.Max(railSpeed, cruiseSpeed);
        float acceleration = topSpeed / Mathf.Max(0.05f, accelerationSeconds);
        float brakingSpeed = Mathf.Sqrt(2f * acceleration * DistanceLeft());
        float wantedSpeed = Mathf.Min(Mathf.Min(topSpeed, SpeedCap), brakingSpeed);
        currentSpeed = Mathf.MoveTowards(currentSpeed, wantedSpeed, acceleration * Time.deltaTime);
        currentSpeed = Mathf.Max(currentSpeed, 0.25f); // never crawl forever right before the end

        // Move along the route, possibly passing several points in one frame.
        float step = currentSpeed * Time.deltaTime;
        Travelled += step; // (a last step past the end counts too; it does not matter)
        while (step > 0f && nextPoint < route.Count)
        {
            Vector3 target = route[nextPoint];
            target.y = transform.position.y;
            float distance = Vector3.Distance(transform.position, target);
            if (distance <= step)
            {
                transform.position = target;
                step -= distance;
                nextPoint += 1;
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, target, step);
                step = 0f;
            }
        }

        if (nextPoint >= route.Count)
        {
            riding = false;
            currentSpeed = 0f;
        }
    }

    // Distance still to ride, along the route.
    private float DistanceLeft()
    {
        float total = 0f;
        Vector3 from = transform.position;
        for (int i = nextPoint; i < route.Count; i++)
        {
            total += FlatDistance(from, route[i]);
            from = route[i];
        }
        return total;
    }

    private const float LookAheadDistance = 4f; // metres: while riding, look at the point this far ahead on the route
    private const float TurnSmoothing = 8f;     // how quickly the body eases toward where it should look (higher = snappier)

    // Where to look while riding: at the point LookAheadDistance ahead on the
    // route. That point slides smoothly along the arcs, so the direction
    // changes smoothly too (looking at the NEXT route point instead would jump
    // a little every time one of the arc's points is passed). Arriving, the
    // view also turns toward the fight over the last cornerLookAhead metres.
    private Vector3 RideDirection()
    {
        if (nextPoint >= route.Count)
        {
            return finalFacing;
        }

        float left = DistanceLeft();
        Vector3 toLookAt = PointAhead(Mathf.Min(LookAheadDistance, left)) - Flat0(transform.position);
        Vector3 direction = toLookAt.sqrMagnitude > 0.0025f ? toLookAt.normalized : finalFacing;
        if (left < cornerLookAhead)
        {
            float blend = 1f - left / Mathf.Max(0.01f, cornerLookAhead);
            direction = Vector3.Slerp(direction, finalFacing, blend);
        }
        return direction;
    }

    // ---- Rounded corners ----

    private const float ArcStep = 0.8f;                   // metres between two points of an arc
    private const float MinTurnDegrees = 5f;              // gentler bends are left as they are
    private const float MaxShareOfLeg = 0.45f;            // an arc never uses more than this much of a leg

    // Replaces every corner of the polyline (except the first and last point)
    // by a circular arc of 'radius' that touches both legs (a fillet). The arc
    // starts 'tangent' metres before the corner and ends as far after it.
    public static List<Vector3> RoundCorners(List<Vector3> points, float radius)
    {
        List<Vector3> result = new List<Vector3>();
        if (points.Count == 0)
        {
            return result;
        }
        result.Add(points[0]);
        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 corner = points[i];
            Vector3 legIn = points[i] - points[i - 1];
            Vector3 legOut = points[i + 1] - points[i];
            float lengthIn = legIn.magnitude;
            float lengthOut = legOut.magnitude;
            Vector3 a = legIn / lengthIn;   // direction coming in
            Vector3 b = legOut / lengthOut; // direction going out
            float turn = Mathf.Acos(Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f)); // radians
            if (turn < MinTurnDegrees * Mathf.Deg2Rad || turn > 170f * Mathf.Deg2Rad)
            {
                result.Add(corner); // almost straight (or a U-turn): keep the corner
                continue;
            }

            // How far before / after the corner the arc touches the legs; a
            // short leg makes the arc tighter.
            float tangent = radius * Mathf.Tan(turn * 0.5f);
            float arcRadius = radius;
            float maxTangent = MaxShareOfLeg * Mathf.Min(lengthIn, lengthOut);
            if (tangent > maxTangent)
            {
                tangent = maxTangent;
                arcRadius = tangent / Mathf.Tan(turn * 0.5f);
            }

            // The centre of the arc is on the inside of the turn.
            float side = Mathf.Sign(a.x * b.z - a.z * b.x);          // which way it turns
            Vector3 inward = new Vector3(-a.z, 0f, a.x) * side;      // perpendicular to the way in, toward the inside
            Vector3 start = corner - a * tangent;
            Vector3 centre = start + inward * arcRadius;
            Vector3 fromCentre = start - centre;

            int steps = Mathf.Max(2, Mathf.CeilToInt(turn * arcRadius / ArcStep));
            for (int k = 0; k <= steps; k++)
            {
                float angle = side * turn * k / steps;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                Vector3 rotated = new Vector3(fromCentre.x * cos - fromCentre.z * sin, 0f, fromCentre.x * sin + fromCentre.z * cos);
                result.Add(centre + rotated);
            }
        }
        result.Add(points[points.Count - 1]);
        return result;
    }

    // The direction on the ground (y = 0), normalised.
    private static Vector3 Flat(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }
        return direction.normalized;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
