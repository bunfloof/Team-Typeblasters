// HardLevel.cs
// ---------------------------------------------------------------------------
// Present only in Hard.unity (the Hollow Forest). Enemies there move faster
// and normal zombies carry long words. Normal scenes keep their original rules.
//
// How words look and how exactly they must be typed (case, punctuation,
// symbols) and the word-changing PURPLE SHIFTERS come from the TYPING
// DIFFICULTY the player picks on the Start panel (TypingSettings), in every
// level, this one included.
// ---------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-1000)]
public class HardLevel : MonoBehaviour
{
    public static HardLevel Active { get; private set; }
    [Min(1f)] public float enemySpeedMultiplier = 1.30f;

    private readonly List<Scenery> scenery = new List<Scenery>();

    private sealed class Scenery
    {
        public Renderer[] renderers;
        public bool[] originallyHidden;
        public Bounds bounds;
        public bool hidden;
    }

    private void Awake()
    {
        Active = this;
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            // Keep an entire tree together, including its trunk and branches.
            // Only scenery participates; ground, enemies and supplies stay visible.
            if (!child.name.StartsWith("Forest Tree ") && child.name != "Mossy Boulder"
                && child.name != "Warden Standing Stone")
                continue;

            Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            var item = new Scenery
            {
                renderers = renderers,
                originallyHidden = new bool[renderers.Length],
                bounds = renderers[0].bounds
            };
            for (int i = 0; i < renderers.Length; i++)
            {
                item.bounds.Encapsulate(renderers[i].bounds);
                item.originallyHidden[i] = renderers[i].forceRenderingOff;
            }
            scenery.Add(item);
        }
    }

    // Called after CameraDirector has applied both the ride and map-view poses.
    // Route clearance alone cannot protect the camera as it rises through trees.
    internal void UpdateSceneryVisibility(Camera camera, Vector3 playerPosition)
    {
        if (!isActiveAndEnabled || camera == null) return;
        Vector3 origin = camera.transform.position;
        Vector3 toPlayer = playerPosition + Vector3.up - origin;
        float distance = toPlayer.magnitude;
        Ray sightline = new Ray(origin, distance > 0.001f ? toPlayer / distance : camera.transform.forward);
        foreach (Scenery item in scenery)
        {
            Bounds bounds = item.bounds;
            // Protect the full capsule, rather than just a ray to its centre.
            // Extra space on a hidden tree prevents flicker at its boundary.
            float padding = item.hidden ? 0.9f : 0.65f;
            bounds.Expand(new Vector3(padding * 2f, 2.4f, padding * 2f));
            bool hide = bounds.Contains(origin)
                || (bounds.IntersectRay(sightline, out float entry) && entry <= distance);
            if (hide == item.hidden) continue;
            item.hidden = hide;
            for (int i = 0; i < item.renderers.Length; i++)
                if (item.renderers[i] != null)
                    item.renderers[i].forceRenderingOff = hide || item.originallyHidden[i];
        }
    }

    private void OnDisable()
    {
        foreach (Scenery item in scenery)
        {
            item.hidden = false;
            for (int i = 0; i < item.renderers.Length; i++)
                if (item.renderers[i] != null)
                    item.renderers[i].forceRenderingOff = item.originallyHidden[i];
        }
    }

    private void OnDestroy() { if (Active == this) Active = null; }
}
