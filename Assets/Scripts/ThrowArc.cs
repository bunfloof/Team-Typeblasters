// ThrowArc.cs
// ---------------------------------------------------------------------------
// The aiming preview of the lure bomb: a DASHED orange arc from the player's
// hand to where the bomb will land, and the bomb's blast ring on the ground
// there. It follows exactly the path the bomb will fly (LureBomb.ArcPoint),
// so what you see is where it lands.
//
// How it is used (by Powers, while the 1 key is held):
//   ThrowArc arc = ThrowArc.Create();
//   arc.Show(hand, landingPoint);   // every frame while aiming
//   arc.Hide();                     // when the bomb is thrown or aiming stops
//
// Each dash is its own small LineRenderer (the game uses no textures, so a
// dashed line is made of separate short lines), built once and reused.
// ---------------------------------------------------------------------------
using UnityEngine;

public class ThrowArc : MonoBehaviour
{
    private const int DashCount = 22;         // dashes along the whole arc
    private const float DashFill = 0.55f;     // part of each step that is drawn (the rest is the gap)
    private const int PointsPerDash = 3;      // points inside one dash, so it follows the curve
    private const float DashWidth = 0.07f;    // metres

    private LineRenderer[] dashes;
    private Transform landingMarker;          // carries the blast ring

    public static ThrowArc Create()
    {
        GameObject arcObject = new GameObject("ThrowArc");
        ThrowArc arc = arcObject.AddComponent<ThrowArc>();
        arc.Build();
        arc.Hide();
        return arc;
    }

    private void Build()
    {
        Material material = Palette.Unlit(Palette.BlastOrange);

        dashes = new LineRenderer[DashCount];
        for (int i = 0; i < DashCount; i++)
        {
            GameObject dashObject = new GameObject("Dash " + i);
            dashObject.transform.SetParent(transform, false);
            LineRenderer dash = dashObject.AddComponent<LineRenderer>();
            dash.useWorldSpace = true;
            dash.positionCount = PointsPerDash;
            dash.startWidth = DashWidth;
            dash.endWidth = DashWidth;
            dash.numCapVertices = 2; // rounded ends
            dash.sharedMaterial = material;
            dash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dash.receiveShadows = false;
            dashes[i] = dash;
        }

        // The ring shows what the bomb's blast will cover where it lands.
        landingMarker = new GameObject("Landing").transform;
        landingMarker.SetParent(transform, false);
        BuildRing();
    }

    // The ring is drawn for one radius: built again if the blast radius
    // changed (the tutorial's lessons use a bigger blast than its final wave).
    private BlastRing ring;
    private float ringRadius;

    private void BuildRing()
    {
        if (ring != null)
        {
            Destroy(ring.gameObject);
        }
        ringRadius = LureBomb.BlastRadius;
        ring = BlastRing.Create(landingMarker, ringRadius, Palette.BlastOrange);
    }

    // Draws the arc from "from" (the hand) to "to" (the landing point on the ground).
    public void Show(Vector3 from, Vector3 to)
    {
        gameObject.SetActive(true);

        for (int i = 0; i < DashCount; i++)
        {
            float start = (float)i / DashCount;
            float end = (i + DashFill) / DashCount;
            for (int k = 0; k < PointsPerDash; k++)
            {
                float t = Mathf.Lerp(start, end, (float)k / (PointsPerDash - 1));
                dashes[i].SetPosition(k, LureBomb.ArcPoint(from, to, t));
            }
        }

        landingMarker.position = to;
        if (ringRadius != LureBomb.BlastRadius)
        {
            BuildRing();
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
