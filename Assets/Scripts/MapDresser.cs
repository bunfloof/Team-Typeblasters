// MapDresser.cs
// ---------------------------------------------------------------------------
// Dresses the greybox maps when a level loads: the same plain boxes, painted
// in the real places' colours and given a few more boxes of detail. Nothing
// is an image or a model: colours (Palette.Lit) and Unity primitives only,
// and nothing added has a collider, so gameplay (walking, spawning, blasts,
// the lure bomb's wall check) is exactly as before.
//
//   The colours are the real ones, MUTED (dark, low saturation), so zombies
//   and their words always stand out: see "USC colours, muted" below.
//
//   USC CAMPUS (Normal)   the School of Cinematic Arts at dusk: stucco,
//       clay-tile roofs, stone columns, dark glass, lawns, small cardinal-
//       and-gold banners and palms. Across the streets: USC brick.
//   THE COLISEUM (Endless)   a dark green field with faint yard lines and
//       deep cardinal end zones, cardinal seats with a gold band, concrete
//       peristyle, a lit torch and bright light towers.
//   THE HOLLOW FOREST (Hard)   the trees get three shades of green and the
//       lanterns glow.
//
// It runs by itself after every scene load (no object in the scene needed)
// and only touches a map that is switched on. The scene files and the shared
// material assets are not changed: delete this file and the greybox is back.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapDresser
{
    // ---- USC colours, muted ----
    // The hues are the real ones (cardinal, gold, brick, cream stucco, clay tile),
    // but every surface is kept DARK and LOW in saturation: the map is the stage,
    // not the show. Zombies are light and saturated, so they must stand against
    // something dark and dull. Rule: no background surface brighter than about
    // 10% relative luminance, which keeps the common grey zombie at 4.5:1 or
    // better against everything behind it (the WCAG AA figure for text).
    private static readonly Color Cardinal = new Color(0.42f, 0.05f, 0.06f);      // USC cardinal (#990000)
    private static readonly Color Gold = new Color(0.66f, 0.52f, 0.12f);          // USC gold (#FFCC00)
    private static readonly Color Stucco = new Color(0.37f, 0.34f, 0.30f);      // SCA's warm cream walls
    private static readonly Color StuccoLight = new Color(0.39f, 0.36f, 0.31f);
    private static readonly Color StuccoDeep = new Color(0.33f, 0.30f, 0.27f);
    private static readonly Color RoofTile = new Color(0.34f, 0.19f, 0.15f);    // clay tile
    private static readonly Color CastStone = new Color(0.37f, 0.36f, 0.34f);   // columns, trim, low walls
    private static readonly Color Brick = new Color(0.36f, 0.20f, 0.18f);       // USC red brick
    private static readonly Color Glass = new Color(0.05f, 0.07f, 0.10f);
    private static readonly Color Grass = new Color(0.15f, 0.24f, 0.16f);
    private static readonly Color Paving = new Color(0.30f, 0.29f, 0.28f);      // warm concrete
    private static readonly Color Asphalt = new Color(0.15f, 0.15f, 0.17f);
    private static readonly Color Bronze = new Color(0.30f, 0.23f, 0.15f);
    private static readonly Color Water = new Color(0.14f, 0.30f, 0.36f);
    private static readonly Color Trunk = new Color(0.25f, 0.20f, 0.15f);
    private static readonly Color Frond = new Color(0.12f, 0.22f, 0.14f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += (scene, mode) => Dress();
        Dress(); // the scene the game opened with
    }

    private static void Dress()
    {
        GameObject coliseum = GameObject.Find("Endless - The Coliseum");
        GameObject map = GameObject.Find("Map");
        if (coliseum != null && coliseum.activeInHierarchy)
        {
            DressColiseum(coliseum.transform);
        }
        else if (HardLevel.Active != null)
        {
            DressForest();
        }
        else if (map != null && map.activeInHierarchy)
        {
            DressCampus(map.transform);
        }
    }

    // ======================================================================
    // USC CAMPUS
    // ======================================================================

    private static void DressCampus(Transform map)
    {
        Transform details = new GameObject("Map Details (MapDresser)").transform;

        foreach (Renderer renderer in map.GetComponentsInChildren<Renderer>())
        {
            Transform part = renderer.transform;
            string name = part.name;
            string group = part.parent != null ? part.parent.name : "";
            Vector3 size = part.lossyScale;

            if (name == "Lawn")
            {
                Paint(renderer, Grass);
            }
            else if (name == "Paving")
            {
                Paint(renderer, Paving);
            }
            else if (name == "Ground")
            {
                Paint(renderer, Asphalt); // the streets around the block
            }
            else if (name == "Neighbour")
            {
                Paint(renderer, Brick);
                Roof(details, part, CastStone, 0.5f, 0.2f);
                Windows(details, part, 3.6f);
            }
            else if (name == "Pier")
            {
                Paint(renderer, CastStone);
                Capital(details, part);
            }
            else if (name == "Statue")
            {
                Paint(renderer, Bronze);
            }
            else if (name == "Fountain water")
            {
                Paint(renderer, Water);
            }
            else if (name == "Planter")
            {
                Paint(renderer, CastStone);
                Top(details, part, Grass, 0.05f, -0.25f); // planted
            }
            else if (name == "Seat wall" || name == "Wall" || name == "Pedestal" || name == "Round base" || name == "Lintel")
            {
                Paint(renderer, CastStone);
            }
            else if (name == "Building")
            {
                // A thin slab (an arcade's roof, a canopy) is a clay-tile roof;
                // everything else is a stucco block with a tiled top and windows.
                if (size.y <= 2.1f)
                {
                    Paint(renderer, RoofTile);
                }
                else
                {
                    Paint(renderer, BuildingColour(group));
                    Roof(details, part, RoofTile, 0.45f, 0.35f);
                    Plinth(details, part);
                    if (size.y >= 8f)
                    {
                        Windows(details, part, 3.4f);
                    }
                }
            }
        }

        Banners(details, map);
        Palms(details, map);
        CampusLight();
    }

    // Each building of the block in its own shade of the same stucco, so they read apart.
    private static Color BuildingColour(string group)
    {
        if (group.StartsWith("SCA")) return Stucco;
        if (group.StartsWith("SCI")) return StuccoLight;
        if (group.StartsWith("SCB")) return new Color(0.36f, 0.33f, 0.30f);
        if (group.StartsWith("SCC")) return StuccoDeep;           // the sound stages: plainer
        if (group.StartsWith("SCX")) return new Color(0.35f, 0.32f, 0.28f);
        if (group.StartsWith("SCE")) return new Color(0.32f, 0.29f, 0.25f);
        return Stucco;
    }

    // The scene's own cool light is kept (a warm sun washed the walls out);
    // only the sky changes, to a deep dusk blue darker than any zombie.
    private static void CampusLight()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor)
        {
            cam.backgroundColor = new Color(0.08f, 0.11f, 0.20f);
        }
    }

    // ---- Building details ----

    // A roof slab on top of a block, a little wider than it (eaves).
    private static void Roof(Transform parent, Transform block, Color color, float thickness, float overhang)
    {
        Vector3 size = block.lossyScale;
        Shapes.Block(PrimitiveType.Cube, "Roof", parent, block.position + Vector3.up * (size.y * 0.5f + thickness * 0.5f),
            new Vector3(size.x + overhang * 2f, thickness, size.z + overhang * 2f), Palette.Lit(color), block.eulerAngles);
    }

    // A thin layer on top of a low block (a planter's plants). inset < 0 makes it smaller than the block.
    private static void Top(Transform parent, Transform block, Color color, float thickness, float inset)
    {
        Vector3 size = block.lossyScale;
        Shapes.Block(PrimitiveType.Cube, "Top", parent, block.position + Vector3.up * (size.y * 0.5f + thickness * 0.5f),
            new Vector3(Mathf.Max(0.2f, size.x + inset * 2f), thickness, Mathf.Max(0.2f, size.z + inset * 2f)), Palette.Lit(color), block.eulerAngles);
    }

    // A darker base course around the foot of a block.
    private static void Plinth(Transform parent, Transform block)
    {
        Vector3 size = block.lossyScale;
        float bottom = block.position.y - size.y * 0.5f;
        if (bottom > 0.5f)
        {
            return; // an upper floor, not standing on the ground
        }
        Shapes.Block(PrimitiveType.Cube, "Plinth", parent, new Vector3(block.position.x, bottom + 0.3f, block.position.z),
            new Vector3(size.x + 0.08f, 0.6f, size.z + 0.08f), Palette.Lit(new Color(0.26f, 0.24f, 0.22f)), block.eulerAngles);
    }

    // Bands of dark glass round a block, one per floor (not on the ground floor: doors and arcades are there).
    private static void Windows(Transform parent, Transform block, float floorHeight)
    {
        Vector3 size = block.lossyScale;
        if (size.x < 5f && size.z < 5f)
        {
            return; // a chimney or a tower shaft: no windows
        }
        float bottom = block.position.y - size.y * 0.5f;
        Material glass = Palette.Lit(Glass);
        Material frame = Palette.Lit(CastStone);
        for (float y = bottom + floorHeight * 1.45f; y < bottom + size.y - 1.2f; y += floorHeight)
        {
            Vector3 at = new Vector3(block.position.x, y, block.position.z);
            Shapes.Block(PrimitiveType.Cube, "Windows", parent, at, new Vector3(size.x + 0.06f, 1.3f, size.z + 0.06f), glass, block.eulerAngles);
            // A sill under each band.
            Shapes.Block(PrimitiveType.Cube, "Sill", parent, at + Vector3.down * 0.75f, new Vector3(size.x + 0.16f, 0.12f, size.z + 0.16f), frame, block.eulerAngles);
        }
        // Solid corners, so the band reads as rows of windows and not as a split building.
        Material wall = block.GetComponent<Renderer>().sharedMaterial;
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
            Vector3 corner = block.position + block.rotation * new Vector3(sx * (size.x * 0.5f - 0.45f), 0f, sz * (size.z * 0.5f - 0.45f));
            Shapes.Block(PrimitiveType.Cube, "Corner", parent, corner, new Vector3(1.0f, size.y, 1.0f), wall, block.eulerAngles);
        }
    }

    // The wider head of a column.
    private static void Capital(Transform parent, Transform pier)
    {
        Vector3 size = pier.lossyScale;
        Material stone = Palette.Lit(CastStone);
        Shapes.Block(PrimitiveType.Cube, "Capital", parent, pier.position + Vector3.up * (size.y * 0.5f - 0.15f),
            new Vector3(size.x + 0.3f, 0.3f, size.z + 0.3f), stone, pier.eulerAngles);
        Shapes.Block(PrimitiveType.Cube, "Base", parent, new Vector3(pier.position.x, pier.position.y - size.y * 0.5f + 0.15f, pier.position.z),
            new Vector3(size.x + 0.3f, 0.3f, size.z + 0.3f), stone, pier.eulerAngles);
    }

    // USC banners: cardinal with a gold stripe, hung on every other column.
    private static void Banners(Transform parent, Transform map)
    {
        Material cloth = Palette.Lit(Cardinal);
        Material stripe = Palette.Lit(Gold);
        int count = 0;
        foreach (Transform part in map.GetComponentsInChildren<Transform>())
        {
            if (part.name != "Pier" || part.lossyScale.y < 4.4f)
            {
                continue;
            }
            count += 1;
            if (count % 2 != 0)
            {
                continue;
            }
            float top = part.position.y + part.lossyScale.y * 0.5f;
            // On both faces along x and along z: seen from wherever the ride passes.
            for (int side = 0; side < 4; side++)
            {
                Vector3 out3 = side == 0 ? Vector3.right : side == 1 ? Vector3.left : side == 2 ? Vector3.forward : Vector3.back;
                Vector3 at = new Vector3(part.position.x, top - 1.55f, part.position.z) + out3 * (part.lossyScale.x * 0.5f + 0.04f);
                Vector3 scale = side < 2 ? new Vector3(0.04f, 1.7f, 0.46f) : new Vector3(0.46f, 1.7f, 0.04f);
                Shapes.Block(PrimitiveType.Cube, "Banner", parent, at, scale, cloth);
                Vector3 band = side < 2 ? new Vector3(0.05f, 0.22f, 0.46f) : new Vector3(0.46f, 0.22f, 0.05f);
                Shapes.Block(PrimitiveType.Cube, "Banner Stripe", parent, at + Vector3.down * 0.5f, band, stripe);
            }
        }
    }

    // Palms at the corners of the bigger lawns (thin trunks, away from the middle where zombies walk).
    private static void Palms(Transform parent, Transform map)
    {
        Material trunk = Palette.Lit(Trunk);
        Material frond = Palette.Lit(Frond);
        foreach (Transform part in map.GetComponentsInChildren<Transform>())
        {
            Vector3 size = part.lossyScale;
            if (part.name != "Lawn" || size.x < 6f || size.z < 6f)
            {
                continue;
            }
            for (int corner = 0; corner < 4; corner++)
            {
                float sx = corner % 2 == 0 ? -1f : 1f, sz = corner < 2 ? -1f : 1f;
                Vector3 at = part.position + part.rotation * new Vector3(sx * (size.x * 0.5f - 1.2f), 0f, sz * (size.z * 0.5f - 1.2f));
                Palm(parent, at, 8.5f + corner * 0.9f, trunk, frond);
            }
        }
    }

    private static void Palm(Transform parent, Vector3 foot, float height, Material trunk, Material frond)
    {
        foot.y = 0f;
        Shapes.Block(PrimitiveType.Cylinder, "Palm Trunk", parent, foot + Vector3.up * height * 0.5f, new Vector3(0.32f, height * 0.5f, 0.32f), trunk);
        Vector3 crown = foot + Vector3.up * height;
        for (int i = 0; i < 7; i++)
        {
            // Fronds: long thin boxes drooping outward all round.
            Quaternion turn = Quaternion.Euler(0f, i * (360f / 7f), 0f);
            Vector3 mid = crown + turn * new Vector3(1.25f, -0.35f, 0f);
            Shapes.Block(PrimitiveType.Cube, "Palm Frond", parent, mid, new Vector3(2.6f, 0.07f, 0.5f), frond, new Vector3(0f, i * (360f / 7f), -22f));
        }
        Shapes.Block(PrimitiveType.Cube, "Palm Crown", parent, crown, Vector3.one * 0.6f, frond, new Vector3(0f, 45f, 0f));
    }

    // ======================================================================
    // THE COLISEUM
    // ======================================================================

    private static void DressColiseum(Transform coliseum)
    {
        Color concrete = new Color(0.34f, 0.33f, 0.31f);
        Color seat = new Color(0.33f, 0.07f, 0.08f);          // cardinal seats
        Color seatDark = new Color(0.27f, 0.06f, 0.07f);
        Color seatGold = new Color(0.42f, 0.33f, 0.12f);      // the gold band of seats
        Color turf = new Color(0.13f, 0.24f, 0.15f);
        Color track = new Color(0.31f, 0.22f, 0.18f);         // clay track
        Color line = new Color(0.34f, 0.40f, 0.36f);

        foreach (Renderer renderer in coliseum.GetComponentsInChildren<Renderer>())
        {
            string name = renderer.name;
            if (name.StartsWith("Stand Tier"))
            {
                // Tier 3 is the gold band; the others alternate two cardinals so the rows read.
                int tier = name[name.Length - 1] - '0';
                Paint(renderer, tier == 3 ? seatGold : tier % 2 == 0 ? seat : seatDark);
            }
            else if (name == "Field")
            {
                Paint(renderer, turf);
            }
            else if (name == "End Zone")
            {
                Paint(renderer, new Color(0.26f, 0.07f, 0.08f)); // deep cardinal, darker than the red zombie
            }
            else if (name == "Yard Line")
            {
                Paint(renderer, line);
            }
            else if (name == "Track and Apron")
            {
                Paint(renderer, track);
            }
            else if (name == "Ground")
            {
                Paint(renderer, Asphalt);
            }
            else if (name == "Field Wall")
            {
                Paint(renderer, Cardinal);
            }
            else if (name == "Field Wall Cap")
            {
                Paint(renderer, Gold);
            }
            else if (name.StartsWith("Goal"))
            {
                Paint(renderer, Gold);
            }
            else if (name == "Light Panel")
            {
                renderer.sharedMaterial = Palette.Unlit(new Color(1f, 0.97f, 0.85f)); // floodlights: always bright
            }
            else if (name == "Torch Bowl")
            {
                Paint(renderer, Bronze);
                // The flame: two unlit cylinders above the bowl (no spheres: the web build has no SphereCollider).
                Shapes.Block(PrimitiveType.Cylinder, "Torch Flame", null, renderer.transform.position + Vector3.up * 1.5f,
                    new Vector3(1.7f, 1.2f, 1.7f), Palette.Unlit(new Color(1f, 0.55f, 0.1f)));
                Shapes.Block(PrimitiveType.Cylinder, "Torch Flame Core", null, renderer.transform.position + Vector3.up * 2.4f,
                    new Vector3(0.9f, 1.0f, 0.9f), Palette.Unlit(new Color(1f, 0.9f, 0.45f)));
            }
            else if (name.StartsWith("Peristyle") || name == "Torch Pedestal" || name.StartsWith("Gate") || name.StartsWith("Tunnel")
                || name == "Parapet" || name.StartsWith("Aisle") || name == "Light Tower")
            {
                Paint(renderer, name == "Parapet" || name.StartsWith("Aisle") ? new Color(0.29f, 0.28f, 0.27f) : concrete);
            }
            else if (name.StartsWith("Team Bench") || name == "Cooler" || name == "Equipment Case")
            {
                Paint(renderer, name == "Cooler" ? Gold : new Color(0.30f, 0.31f, 0.34f));
            }
        }

        // An evening game under a deep blue sky.
        Camera cam = Camera.main;
        if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor)
        {
            cam.backgroundColor = new Color(0.06f, 0.09f, 0.18f);
        }
    }

    // ======================================================================
    // THE HOLLOW FOREST
    // ======================================================================

    private static void DressForest()
    {
        Color[] leaves =
        {
            new Color(0.07f, 0.17f, 0.115f), new Color(0.10f, 0.21f, 0.12f), new Color(0.06f, 0.14f, 0.13f), new Color(0.13f, 0.20f, 0.10f)
        };
        Color[] bark = { new Color(0.16f, 0.12f, 0.09f), new Color(0.20f, 0.15f, 0.11f), new Color(0.13f, 0.10f, 0.085f) };
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                continue;
            }
            // The same tree always gets the same shade (from where it stands).
            Vector3 at = renderer.transform.position;
            int pick = Mathf.Abs(Mathf.FloorToInt(at.x * 0.37f) * 31 + Mathf.FloorToInt(at.z * 0.41f) * 17);
            if (material.name.StartsWith("ForestLeaves"))
            {
                Paint(renderer, leaves[pick % leaves.Length]);
            }
            else if (material.name.StartsWith("ForestBark"))
            {
                Paint(renderer, bark[pick % bark.Length]);
            }
            else if (material.name.StartsWith("ForestLantern"))
            {
                renderer.sharedMaterial = Palette.Unlit(new Color(1f, 0.72f, 0.32f)); // a lantern glows whatever the light
            }
            else if (material.name.StartsWith("ForestWater"))
            {
                Paint(renderer, new Color(0.07f, 0.20f, 0.24f));
            }
        }
    }

    // ---- Helpers ----

    private static void Paint(Renderer renderer, Color color)
    {
        renderer.sharedMaterial = Palette.Lit(color);
    }

    private static Light FindSun()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }
        return null;
    }
}
