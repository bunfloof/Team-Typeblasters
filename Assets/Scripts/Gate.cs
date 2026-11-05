// Gate.cs
// ---------------------------------------------------------------------------
// A gate between two fights: a plain box that blocks the way until the fight
// is won. Then WaveSpawner calls Open() and it sinks into the floor, and the
// player runs over it.
//
// It is a prefab (Assets/Prefabs/Gate): place it on the ground across the
// way, with its blue arrow (forward) pointing along the run. To make it wider,
// change the X scale of its Panel. To make it taller, set the Panel's Y scale
// to the height and its Y position to half of that (change the Panel, not the
// Gate itself).
// Drag it into the fight's Exit Gate field.
// ---------------------------------------------------------------------------
using System.Collections;
using UnityEngine;

public class Gate : MonoBehaviour
{
    private const float OpenSeconds = 1.5f;    // time to sink all the way down

    [SerializeField] private Transform panel;  // the box (it keeps its collider while closed: thrown bodies bounce off it, a lure bomb throw stops short of it)

    public bool IsOpen { get; private set; }

    // Sinks the gate into the floor. Does nothing if it is already open.
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }
        IsOpen = true;
        panel.GetComponent<Collider>().enabled = false;
        StartCoroutine(Sink());
    }

    // Already down when the level starts (a passage zombies come through, see
    // Encounter.openGateFromStart, or a fight passed before a checkpoint).
    public void OpenAtOnce()
    {
        IsOpen = true;
        panel.GetComponent<Collider>().enabled = false;
        panel.gameObject.SetActive(false);
    }

    private IEnumerator Sink()
    {
        float height = panel.localScale.y;
        Vector3 closed = panel.localPosition;
        Vector3 open = closed + Vector3.down * (height + 0.1f); // fully below the floor
        for (float time = 0f; time < OpenSeconds; time += Time.deltaTime)
        {
            panel.localPosition = Vector3.Lerp(closed, open, time / OpenSeconds);
            yield return null;
        }
        panel.gameObject.SetActive(false);
    }
}
