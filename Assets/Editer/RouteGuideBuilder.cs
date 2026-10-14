// RouteGuideBuilder.cs   (put it in a folder called "Editor", e.g. Assets/Editor/)
// ---------------------------------------------------------------------------
// Menu: Tools > Rebuild Route Guide
//
// Deletes the arrows under "Level > Route Guide" and places new ones every
// Spacing metres along the path the player really rides: the Level's Start,
// then every fight's Route points and Stop, with the corners rounded exactly
// like RailMover does (RailMover.RoundCorners with the Player's Corner Radius).
// This is the same cyan line Level draws in the Scene view.
//
// The arrow prefab and the arrow height are copied from the FIRST arrow that is
// already in Route Guide, so keep at least one arrow there before running it.
//
// Ctrl+Z undoes the whole rebuild.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RouteGuideBuilder
{
    private const float Spacing = 3.2f;   // metres between two arrows (the current guide's spacing)
    private const float EndGap = 1.5f;    // no arrow closer than this to a stop
    private const float YawOffset = 0f;   // if the arrows point sideways or backwards, try 90, -90 or 180

    [MenuItem("Tools/Rebuild Route Guide")]
    private static void Rebuild()
    {
        GameObject guide = GameObject.Find("Route Guide");
        Level level = Object.FindFirstObjectByType<Level>();
        RailMover rail = Object.FindFirstObjectByType<RailMover>();
        if (guide == null || level == null || rail == null)
        {
            Debug.LogError("RouteGuideBuilder: needs an object called \"Route Guide\", a Level and a RailMover (Player) in the open scene.");
            return;
        }
        if (guide.transform.childCount == 0)
        {
            Debug.LogError("RouteGuideBuilder: Route Guide has no arrow to copy. Drag one RouteArrow prefab into it first.");
            return;
        }

        GameObject firstArrow = guide.transform.GetChild(0).gameObject;
        GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(firstArrow);
        if (prefab == null)
        {
            Debug.LogError("RouteGuideBuilder: the first arrow in Route Guide is not a prefab instance.");
            return;
        }
        float height = firstArrow.transform.position.y;

        // Level keeps its list in private serialized fields (filled in Awake at runtime), so read them as serialized data.
        SerializedObject levelData = new SerializedObject(level);
        Transform start = levelData.FindProperty("start").objectReferenceValue as Transform;
        SerializedProperty encounters = levelData.FindProperty("encounters");
        if (start == null)
        {
            Debug.LogError("RouteGuideBuilder: Level has no Start.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild Route Guide");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = guide.transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(guide.transform.GetChild(i).gameObject);
        }

        int placed = 0;
        Vector3 from = start.position;
        for (int e = 0; e < encounters.arraySize; e++)
        {
            Encounter encounter = encounters.GetArrayElementAtIndex(e).objectReferenceValue as Encounter;
            if (encounter == null)
            {
                continue;
            }

            // The same steps as Level.OnDrawGizmos and RailMover.RideAlong.
            List<Vector3> corners = new List<Vector3> { Flat(from) };
            foreach (Vector3 point in encounter.Route)
            {
                if (Vector3.Distance(corners[corners.Count - 1], Flat(point)) >= 0.05f)
                {
                    corners.Add(Flat(point));
                }
            }
            List<Vector3> run = rail.CornerRadius > 0f ? RailMover.RoundCorners(corners, rail.CornerRadius) : corners;
            placed += PlaceAlong(run, prefab, guide.transform, height);
            from = corners[corners.Count - 1];
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(guide.scene);
        Debug.Log("RouteGuideBuilder: placed " + placed + " arrows.");
    }

    // Puts an arrow every Spacing metres along the polyline, pointing the way it runs.
    private static int PlaceAlong(List<Vector3> run, GameObject prefab, Transform parent, float height)
    {
        float total = 0f;
        for (int i = 0; i + 1 < run.Count; i++)
        {
            total += Vector3.Distance(run[i], run[i + 1]);
        }

        int count = 0;
        float walked = 0f;
        float next = Spacing;
        for (int i = 0; i + 1 < run.Count; i++)
        {
            Vector3 a = run[i];
            Vector3 b = run[i + 1];
            float length = Vector3.Distance(a, b);
            if (length < 0.0001f)
            {
                continue;
            }
            Vector3 direction = (b - a) / length;

            while (next <= walked + length)
            {
                if (next <= total - EndGap)
                {
                    Vector3 position = a + direction * (next - walked);
                    GameObject arrow = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    arrow.transform.position = new Vector3(position.x, height, position.z);
                    arrow.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, YawOffset, 0f);
                    Undo.RegisterCreatedObjectUndo(arrow, "Rebuild Route Guide");
                    count += 1;
                }
                next += Spacing;
            }
            walked += length;
        }
        return count;
    }

    private static Vector3 Flat(Vector3 point)
    {
        point.y = 0f;
        return point;
    }
}
