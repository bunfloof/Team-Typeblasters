// RouteGuideFader.cs
// ---------------------------------------------------------------------------
// The arrows on the ground ("Level > Route Guide") show the way only while the
// player is riding to the next fight. During a fight they shrink away (they
// are clutter there, and they point away from the zombies), and they grow
// back when the next ride starts.
//
// Added by itself to the Route Guide of every level when it loads; nothing to
// set up in the scene.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RouteGuideFader : MonoBehaviour
{
    private const float FadeSeconds = 0.35f;

    private RailMover rail;
    private readonly List<Transform> arrows = new List<Transform>();
    private readonly List<Vector3> sizes = new List<Vector3>();
    private float shown = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    private static void Attach()
    {
        GameObject guide = GameObject.Find("Route Guide");
        if (guide != null && guide.GetComponent<RouteGuideFader>() == null)
        {
            guide.AddComponent<RouteGuideFader>();
        }
    }

    private void Start()
    {
        rail = FindFirstObjectByType<RailMover>();
        foreach (Transform arrow in transform)
        {
            arrows.Add(arrow);
            sizes.Add(arrow.localScale);
        }
    }

    private void Update()
    {
        if (rail == null)
        {
            return;
        }
        float wanted = rail.IsRiding ? 1f : 0f;
        if (Mathf.Approximately(shown, wanted))
        {
            return;
        }
        shown = Mathf.MoveTowards(shown, wanted, Time.deltaTime / FadeSeconds);
        float scale = Ui.EaseOut(shown);
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i] == null) continue;
            arrows[i].gameObject.SetActive(shown > 0.001f);
            arrows[i].localScale = sizes[i] * scale;
        }
    }
}
