// Almanac.cs
// ---------------------------------------------------------------------------
// The ALMANAC: one entry per kind of enemy (every ZombieKind, and the boss).
// An entry is LOCKED until the player has met that enemy once in any level;
// from then on it shows its name, its colour, what it does and how to kill
// it, with a picture. What has been discovered is saved in PlayerPrefs, so it
// survives closing the game.
//
//   Almanac.Discover(kind)     WaveSpawner, whenever a zombie appears
//   Almanac.Discover("boss")   Boss, when it is built
//   Almanac.Entries            the list, in the order the book shows it
//   Almanac.Portrait(entry)    its picture (see below)
//
// THE PICTURES are drawn in code, pixel by pixel (no image files): the same
// capsule the zombie is in the game, in its colour and proportions, lit from
// the top left, with what makes the kind special drawn on top (a red zombie's
// ring, armour plates, a ghost fading out, the boss's body...). A locked
// entry shows the same shape as a dark silhouette.
//
// The book itself is AlmanacPanel (opened from the main menu).
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class AlmanacEntry
{
    public string Id;          // "runner", "boss"... (the PlayerPrefs key)
    public string Name;
    public string Tagline;     // one line under the name
    public Color Color;        // body colour
    public Color WordColor;    // the colour of its word
    public string Power;       // what it does
    public string HowToKill;
    public string Where;       // where it is first met
    public int Threat;         // 1..5
    public Vector2 Shape;      // body width, height (1, 1 = a normal zombie)
}

public static class Almanac
{
    private const string PrefPrefix = "Almanac.";

    private static List<AlmanacEntry> entries;
    private static readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();

    // An entry has just been discovered for the first time (HUD shows a small notice).
    public static event System.Action<AlmanacEntry> Discovered;

    public static List<AlmanacEntry> Entries
    {
        get
        {
            if (entries == null)
            {
                Build();
            }
            return entries;
        }
    }

    public static int DiscoveredCount
    {
        get
        {
            int count = 0;
            foreach (AlmanacEntry entry in Entries)
            {
                if (IsDiscovered(entry))
                {
                    count += 1;
                }
            }
            return count;
        }
    }

    public static bool IsDiscovered(AlmanacEntry entry)
    {
        return PlayerPrefs.GetInt(PrefPrefix + entry.Id, 0) == 1;
    }

    public static void Discover(ZombieKind kind)
    {
        Discover(IdOf(kind));
    }

    public static void Discover(string id)
    {
        if (PlayerPrefs.GetInt(PrefPrefix + id, 0) == 1)
        {
            return;
        }
        AlmanacEntry entry = Find(id);
        if (entry == null)
        {
            return;
        }
        PlayerPrefs.SetInt(PrefPrefix + id, 1);
        PlayerPrefs.Save();
        Discovered?.Invoke(entry);
    }

    public static AlmanacEntry Find(string id)
    {
        foreach (AlmanacEntry entry in Entries)
        {
            if (entry.Id == id)
            {
                return entry;
            }
        }
        return null;
    }

    // A splitter's halves belong to the splitter's entry.
    private static string IdOf(ZombieKind kind)
    {
        return kind == ZombieKind.Splitling ? "splitter" : kind.ToString().ToLowerInvariant();
    }

    private static void Build()
    {
        entries = new List<AlmanacEntry>
        {
            Entry("normal", "WALKER", "The common dead.", Palette.ZombieGrey, Color.white, 1, 1f, 1f,
                "None. It walks at you and bites.",
                "Type its word.",
                "Every level, from the first wave"),
            Entry("runner", "RUNNER", "Small, fast, impatient.", Palette.RunnerYellow, Palette.WordRunner, 2, 0.7f, 0.9f,
                "Runs much faster than the others. Short word.",
                "Shoot it before the slow ones: it reaches you first.",
                "Campus wave 3 / Endless wave 2"),
            Entry("armored", "ARMORED", "Two words thick.", Palette.ArmorSteel, Palette.WordArmor, 3, 1.15f, 1f,
                "Wears armour: the first word only breaks it.",
                "Type two words, or catch it in any blast: explosions kill it at once.",
                "Campus wave 3 / Endless wave 3"),
            Entry("explosive", "RED ZOMBIE", "The longer its word, the bigger the blast.", Palette.ExplosiveRed, Palette.WordExplosive, 3, 1.25f, 1.2f,
                "Its ring shows its blast: 0.75 m for every letter of its long word.",
                "Wait until others walk into the ring, then finish its word: everything inside dies with it.",
                "Campus wave 1 / Endless wave 7"),
            Entry("splitter", "SPLITTER", "Kill one, get two.", Palette.SplitterTeal, Palette.WordSplitter, 3, 1.3f, 1.05f,
                "Shot dead, it splits into two small fast halves. Each hides half of the old word in a new one.",
                "Blast it and it dies whole. Otherwise be ready for the two halves.",
                "Campus wave 2 / Endless wave 4"),
            Entry("ghost", "GHOST", "Read it while you can.", Palette.GhostPale, Palette.WordGhost, 3, 0.85f, 1.05f,
                "Its long word fades away after a moment, even while you type it.",
                "Memorize the word, then type it from memory.",
                "Campus wave 2 / Endless wave 8"),
            Entry("brute", "BRUTE", "Slow. Huge. Hits twice as hard.", Palette.BruteDark, Palette.WordBrute, 4, 1.6f, 1.35f,
                "Carries a whole phrase. Its bite costs 20 health instead of 10.",
                "Type the full phrase, spaces included. Save your shield for it.",
                "Campus wave 3 / Endless wave 11"),
            Entry("spitter", "SPITTER", "It never comes close.", Palette.SpitterAcid, Palette.WordSpitter, 3, 1f, 0.95f,
                "Stops far away and spits acid globs at you.",
                "Type a glob's short word to pop it, then shoot the spitter.",
                "Campus wave 3 / Endless wave 9"),
            Entry("shifter", "SHIFTER", "Its word will not sit still.", Palette.ShifterPurple, Palette.WordShifter, 3, 0.85f, 1.15f,
                "Its word changes every few seconds.",
                "Type its first letter: that locks the word.",
                "Hard and Extreme typing only"),
            Entry("sniper", "SNIPER", "Up in the stands.", Palette.SniperKhaki, Palette.WordSniper, 4, 0.8f, 0.95f,
                "Never walks. Aims a red laser and fires bullets at you from the stands.",
                "Type the bullet's word before it hits, then the sniper's own word.",
                "Endless wave 6"),
            Entry("boss", "THE BOSS", "Subject Zero. The Hollow Warden.", new Color(0.55f, 0.58f, 0.62f), Palette.WordBoss, 5, 2.2f, 2.4f,
                "Words on its head and limbs. Throws energy orbs and stomps up zombies.",
                "Every word hurts it. Type the orbs before they land, and answer its quiz for a critical hit.",
                "The last stop of Campus and Forest / every 10th Endless wave"),
            Entry("hydra", "THE HYDRA", "Cut it, then burn it.", new Color(0.32f, 0.80f, 0.56f), Palette.WordBoss, 5, 2.6f, 2.0f,
                "Three heads on long necks. A head rears up, its scanner burning white, then strikes. Miss a stump and two heads grow back.",
                "Type a head's word to CUT it, then the burning stump's short word to SEAR it in time. Cut a rearing head first. With every head gone, type the phrase on its body.",
                "Endless wave 15, then every 10 waves"),
        };
    }

    private static AlmanacEntry Entry(string id, string name, string tagline, Color color, Color wordColor, int threat,
        float width, float height, string power, string howToKill, string where)
    {
        return new AlmanacEntry
        {
            Id = id, Name = name, Tagline = tagline, Color = color, WordColor = wordColor, Threat = threat,
            Shape = new Vector2(width, height), Power = power, HowToKill = howToKill, Where = where
        };
    }

    // ---- Pictures ----

    private const int Size = 256;

    // The entry's picture (a dark silhouette while it is locked). Made once, then kept.
    public static Sprite Portrait(AlmanacEntry entry, bool locked)
    {
        string key = entry.Id + (locked ? "?" : "");
        Sprite sprite;
        if (portraits.TryGetValue(key, out sprite) && sprite != null)
        {
            return sprite;
        }

        Color32[] pixels = new Color32[Size * Size];
        if (entry.Id == "hydra")
        {
            DrawHydra(pixels, locked);
        }
        else if (entry.Id == "boss")
        {
            DrawBoss(pixels, entry, locked);
        }
        else
        {
            DrawZombie(pixels, entry, locked);
        }

        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
        portraits[key] = sprite;
        return sprite;
    }

    private static readonly Color Silhouette = new Color32(77, 95, 112, 255);

    private static void DrawZombie(Color32[] pixels, AlmanacEntry entry, bool locked)
    {
        float groundY = 46f;
        float unit = 78f;                                   // pixels for a normal zombie's width
        float width = Mathf.Min(entry.Shape.x * unit, 170f);
        float height = Mathf.Min(entry.Shape.y * unit * 2f, 190f);
        Vector2 feet = new Vector2(Size * 0.5f, groundY);
        Color body = locked ? Silhouette : entry.Color;

        // What is drawn on the ground first: a soft shadow, and the kind's ring.
        Ellipse(pixels, feet, width * 0.75f, width * 0.2f, new Color(0f, 0f, 0f, 0.45f), true, 0f);
        if (!locked && entry.Id == "explosive")
        {
            Ellipse(pixels, feet, 118f, 32f, new Color(1f, 0.55f, 0.1f, 0.95f), false, 4f);
        }
        if (!locked && entry.Id == "sniper")
        {
            // Its laser: a thin red line from its head off the picture.
            Line(pixels, new Vector2(feet.x, groundY + height * 0.8f), new Vector2(8f, 22f), new Color(1f, 0.15f, 0.12f, 0.9f), 2.5f);
        }
        if (!locked && entry.Id == "splitter")
        {
            // The two halves it becomes, small, behind it on both sides.
            Capsule(pixels, new Vector2(feet.x - 86f, groundY), 40f, 86f, Palette.SplitlingTeal, 1f, false);
            Capsule(pixels, new Vector2(feet.x + 86f, groundY), 40f, 86f, Palette.SplitlingTeal, 1f, false);
        }

        float alpha = !locked && entry.Id == "ghost" ? 0.62f : 1f;
        Capsule(pixels, feet, width, height, body, alpha, !locked && entry.Id == "ghost");
        if (locked)
        {
            Signature(pixels, feet, width, height, body, true); // the silhouette still has its visor, antenna and feet
            return;
        }

        // What makes the kind special, drawn on the body.
        if (entry.Id == "armored")
        {
            Color plate = Color.Lerp(entry.Color, Color.white, 0.45f);
            Band(pixels, feet, width, height, 0.30f, 0.38f, plate);
            Band(pixels, feet, width, height, 0.46f, 0.54f, plate);
        }
        else if (entry.Id == "splitter")
        {
            Line(pixels, new Vector2(feet.x, groundY + 6f), new Vector2(feet.x, groundY + height - 6f), new Color(0.2f, 0.05f, 0.12f, 0.9f), 3f);
        }
        else if (entry.Id == "runner")
        {
            // Speed lines behind it.
            for (int i = 0; i < 3; i++)
            {
                float y = groundY + height * (0.3f + i * 0.2f);
                Line(pixels, new Vector2(feet.x + width * 0.75f, y), new Vector2(feet.x + width * 0.75f + 46f - i * 8f, y), WithAlpha(entry.Color, 0.6f), 3f);
            }
        }
        else if (entry.Id == "spitter")
        {
            // An acid glob in the air.
            Ellipse(pixels, new Vector2(feet.x - 84f, groundY + height * 0.9f), 13f, 13f, WithAlpha(Palette.SpitterAcid, 0.95f), true, 0f);
            Ellipse(pixels, new Vector2(feet.x - 60f, groundY + height * 0.78f), 7f, 7f, WithAlpha(Palette.SpitterAcid, 0.7f), true, 0f);
        }
        Signature(pixels, feet, width, height, body, false);
    }

    // ---- The signature look (the same as in the game, see Zombie.BuildFace) ----
    // A narrow dark slit visor with a red scanner light, a short antenna with a
    // red light on top, and two stubby feet. A locked entry keeps the shapes
    // (that is what a silhouette is) but no lights.
    private static readonly Color VisorGlass = new Color(0.05f, 0.06f, 0.09f);
    private static readonly Color ScanRed = new Color(1.00f, 0.22f, 0.14f);

    private static void Signature(Color32[] pixels, Vector2 feet, float width, float height, Color body, bool locked)
    {
        Color dark = locked ? Color.Lerp(Silhouette, Color.black, 0.45f) : VisorGlass;
        // The feet: two dark ovals peeking out at the bottom.
        Color foot = locked ? Color.Lerp(Silhouette, Color.black, 0.25f) : new Color(body.r * 0.62f, body.g * 0.62f, body.b * 0.66f);
        Ellipse(pixels, new Vector2(feet.x - width * 0.22f, feet.y + 3f), width * 0.19f, 7f, foot, true, 0f);
        Ellipse(pixels, new Vector2(feet.x + width * 0.22f, feet.y + 3f), width * 0.19f, 7f, foot, true, 0f);

        // The antenna: a slightly leaning stalk from the top of the head, a light on top.
        Vector2 root = new Vector2(feet.x + width * 0.12f, feet.y + height - width * 0.06f);
        Vector2 tip = root + new Vector2(width * 0.06f, height * 0.13f);
        Line(pixels, root, tip, dark, Mathf.Max(2.5f, width * 0.045f));
        if (!locked)
        {
            Ellipse(pixels, tip, 11f, 11f, WithAlpha(ScanRed, 0.25f), true, 0f);   // the glow
        }
        Ellipse(pixels, tip, 5.5f, 5.5f, locked ? dark : ScanRed, true, 0f);

        // The slit visor across the face, with a thin shine along its top.
        float visorY = feet.y + height * 0.71f;
        float half = width * 0.44f;
        float thick = Mathf.Max(7f, height * 0.055f);
        Stadium(pixels, new Vector2(feet.x - half, visorY), new Vector2(feet.x + half, visorY), thick, dark);
        if (!locked)
        {
            Line(pixels, new Vector2(feet.x - half * 0.7f, visorY + thick * 0.45f), new Vector2(feet.x + half * 0.2f, visorY + thick * 0.45f),
                new Color(1f, 1f, 1f, 0.18f), 1.5f);
            // The scanner light, a little off centre (it sweeps), with its glow.
            Vector2 light = new Vector2(feet.x - half * 0.25f, visorY);
            Ellipse(pixels, light, thick * 2.2f, thick * 1.3f, WithAlpha(ScanRed, 0.22f), true, 0f);
            Stadium(pixels, light - new Vector2(thick * 0.9f, 0f), light + new Vector2(thick * 0.9f, 0f), thick * 0.42f, ScanRed);
        }
    }

    // A horizontal rounded bar (a capsule lying down) from a to b, 'radius' thick each side.
    private static void Stadium(Color32[] pixels, Vector2 a, Vector2 b, float radius, Color color)
    {
        for (int y = Mathf.Max(0, (int)(a.y - radius - 2f)); y < Mathf.Min(Size, a.y + radius + 3f); y++)
        {
            for (int x = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - radius - 2f)); x < Mathf.Min(Size, Mathf.Max(a.x, b.x) + radius + 3f); x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                float d = Vector2.Distance(p, a + ab * t);
                float edge = Mathf.Clamp01(radius - d + 0.5f);
                if (edge > 0f)
                {
                    Blend(pixels, x, y, new Color(color.r, color.g, color.b, color.a * edge));
                }
            }
        }
    }

    // The Hydra: a big bean with three heads on curved necks, all with the slit visor.
    private static void DrawHydra(Color32[] pixels, bool locked)
    {
        Color skin = locked ? Silhouette : new Color(0.32f, 0.80f, 0.56f);
        Vector2 feet = new Vector2(Size * 0.5f, 34f);
        Ellipse(pixels, feet, 70f, 16f, new Color(0f, 0f, 0f, 0.45f), true, 0f);
        Vector2[] heads = { new Vector2(feet.x - 74f, 196f), new Vector2(feet.x, 214f), new Vector2(feet.x + 74f, 196f) };
        foreach (Vector2 head in heads)
        {
            Vector2 root = new Vector2(feet.x + (head.x - feet.x) * 0.3f, feet.y + 92f);
            Vector2 mid = new Vector2((root.x + head.x) * 0.5f + (head.x - feet.x) * 0.15f, (root.y + head.y) * 0.5f + 12f);
            Line(pixels, root, mid, skin, 13f);
            Line(pixels, mid, head, skin, 11f);
        }
        Capsule(pixels, feet, 104f, 124f, skin, 1f, false);
        Signature(pixels, feet, 104f, 124f, skin, locked);
        foreach (Vector2 head in heads)
        {
            Vector2 headFeet = new Vector2(head.x, head.y - 18f);
            Capsule(pixels, headFeet, 40f, 34f, skin, 1f, false);
            Stadium(pixels, new Vector2(head.x - 14f, head.y - 4f), new Vector2(head.x + 14f, head.y - 4f), 4.5f,
                locked ? Color.Lerp(Silhouette, Color.black, 0.45f) : VisorGlass);
            if (!locked)
            {
                Ellipse(pixels, new Vector2(head.x - 4f, head.y - 4f), 5f, 2.4f, ScanRed, true, 0f);
            }
        }
    }

    // The boss: the same body as in the game (torso, head, two arms, two legs).
    private static void DrawBoss(Color32[] pixels, AlmanacEntry entry, bool locked)
    {
        Color body = locked ? Silhouette : entry.Color;
        float ground = 30f;
        Vector2 middle = new Vector2(Size * 0.5f, ground);
        Ellipse(pixels, middle, 92f, 20f, new Color(0f, 0f, 0f, 0.45f), true, 0f);
        Capsule(pixels, new Vector2(middle.x - 20f, ground), 28f, 84f, body, 1f, false);          // legs
        Capsule(pixels, new Vector2(middle.x + 20f, ground), 28f, 84f, body, 1f, false);
        Capsule(pixels, new Vector2(middle.x - 56f, ground + 62f), 25f, 88f, body, 1f, false);    // arms
        Capsule(pixels, new Vector2(middle.x + 56f, ground + 62f), 25f, 88f, body, 1f, false);
        Capsule(pixels, new Vector2(middle.x, ground + 74f), 78f, 98f, body, 1f, false);          // torso
        Capsule(pixels, new Vector2(middle.x, ground + 168f), 52f, 52f, body, 1f, false);         // head
        if (!locked)
        {
            // The orb it throws, charging over a hand.
            Ellipse(pixels, new Vector2(middle.x + 56f, ground + 172f), 15f, 15f, new Color(1f, 0.15f, 0.55f, 0.95f), true, 0f);
        }
    }

    // ---- Tiny software renderer ----

    // An upright capsule standing on 'feet', shaded like a lit 3D capsule.
    private static void Capsule(Color32[] pixels, Vector2 feet, float width, float height, Color color, float alpha, bool fadeDown)
    {
        float r = width * 0.5f;
        height = Mathf.Max(height, width);
        float bottom = feet.y + r;            // centre of the lower cap
        float top = feet.y + height - r;      // centre of the upper cap
        Vector3 light = new Vector3(-0.45f, 0.55f, 0.7f).normalized;
        for (int y = Mathf.Max(0, (int)feet.y); y < Mathf.Min(Size, feet.y + height + 1f); y++)
        {
            for (int x = Mathf.Max(0, (int)(feet.x - r - 1f)); x < Mathf.Min(Size, feet.x + r + 2f); x++)
            {
                float dx = x + 0.5f - feet.x;
                float cy = Mathf.Clamp(y + 0.5f, bottom, top);
                float dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > r)
                {
                    continue;
                }
                // The surface normal of a capsule at this pixel.
                float nx = dx / r;
                float ny = dy / r;
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                float lit = Mathf.Clamp01(nx * light.x + ny * light.y + nz * light.z);
                float shade = 0.38f + 0.72f * lit;
                float edge = Mathf.Clamp01((r - d) / 1.5f); // soft edge
                float a = alpha * edge;
                if (fadeDown)
                {
                    a *= Mathf.Lerp(0.15f, 1f, Mathf.Clamp01((y - feet.y) / (height * 0.7f))); // a ghost fades toward the ground
                }
                Blend(pixels, x, y, new Color(color.r * shade, color.g * shade, color.b * shade, a));
            }
        }
    }

    // A horizontal band across a capsule's body, between two heights (0 = feet, 1 = head).
    private static void Band(Color32[] pixels, Vector2 feet, float width, float height, float from, float to, Color color)
    {
        float r = width * 0.5f;
        for (int y = (int)(feet.y + height * from); y < (int)(feet.y + height * to); y++)
        {
            for (int x = (int)(feet.x - r); x < (int)(feet.x + r); x++)
            {
                float nx = (x + 0.5f - feet.x) / r;
                if (Mathf.Abs(nx) > 0.97f)
                {
                    continue;
                }
                float shade = 0.55f + 0.45f * Mathf.Sqrt(1f - nx * nx) - 0.25f * nx;
                Blend(pixels, x, y, new Color(color.r * shade, color.g * shade, color.b * shade, 0.92f));
            }
        }
    }

    // A filled ellipse, or (filled = false) its outline 'thickness' pixels wide.
    private static void Ellipse(Color32[] pixels, Vector2 centre, float rx, float ry, Color color, bool filled, float thickness)
    {
        for (int y = Mathf.Max(0, (int)(centre.y - ry - 2f)); y < Mathf.Min(Size, centre.y + ry + 3f); y++)
        {
            for (int x = Mathf.Max(0, (int)(centre.x - rx - 2f)); x < Mathf.Min(Size, centre.x + rx + 3f); x++)
            {
                float dx = (x + 0.5f - centre.x) / rx;
                float dy = (y + 0.5f - centre.y) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a;
                if (filled)
                {
                    a = Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) / 2f);
                }
                else
                {
                    float off = Mathf.Abs(d - 1f) * Mathf.Min(rx, ry);
                    a = Mathf.Clamp01(thickness * 0.5f + 0.5f - off);
                }
                if (a > 0f)
                {
                    Blend(pixels, x, y, new Color(color.r, color.g, color.b, color.a * a));
                }
            }
        }
    }

    private static void Line(Color32[] pixels, Vector2 from, Vector2 to, Color color, float thickness)
    {
        Vector2 along = to - from;
        float length = along.magnitude;
        int xMin = Mathf.Max(0, (int)(Mathf.Min(from.x, to.x) - thickness)), xMax = Mathf.Min(Size, (int)(Mathf.Max(from.x, to.x) + thickness + 1f));
        int yMin = Mathf.Max(0, (int)(Mathf.Min(from.y, to.y) - thickness)), yMax = Mathf.Min(Size, (int)(Mathf.Max(from.y, to.y) + thickness + 1f));
        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - from;
                float t = length > 0.001f ? Mathf.Clamp01(Vector2.Dot(p, along) / (length * length)) : 0f;
                float distance = (p - along * t).magnitude;
                float a = Mathf.Clamp01(thickness * 0.5f + 0.5f - distance);
                if (a > 0f)
                {
                    Blend(pixels, x, y, new Color(color.r, color.g, color.b, color.a * a));
                }
            }
        }
    }

    private static void Blend(Color32[] pixels, int x, int y, Color source)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size || source.a <= 0f)
        {
            return;
        }
        int index = y * Size + x;
        Color under = pixels[index];
        float outA = source.a + under.a * (1f - source.a);
        if (outA <= 0f)
        {
            return;
        }
        Color result = (source * source.a + under * under.a * (1f - source.a)) / outA;
        result.a = outA;
        pixels[index] = result;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
