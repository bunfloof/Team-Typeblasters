// MenuArt.cs
// ---------------------------------------------------------------------------
// The pictures of the menu scene. Like HudIcons, nothing here is an image file:
// every sprite is DRAWN IN CODE once into a texture (circles, capsules, polygons
// measured as distances, so the edges are anti-aliased) and cached.
//
//   RoundedBox / RoundedOutline  9-sliced rounded rectangles for buttons and cards
//   Zombie(variant)              a capsule zombie like the game's, with a simple face (colours = the game's zombie kinds)
//   Map(index)                   the level pictures: 0 campus, 1 forest, 2 coliseum
//   Tape / ComicButton / Burst   the title screen's red tape, yellow START block and BOOM! sticker
//   FloorRing / Sunburst / Scanlines   the ring under the crowd, the rays and the monitor lines
//   Sparkle / Dot                small shapes
//   Style(label, ...)            the menu font with a thick dark outline
//
// MenuBackground and MainMenu use these. Map() is the slow one (a few
// milliseconds per picture), so MainMenu asks for one picture per frame.
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class MenuArt
{
    private static readonly Color Ink = new Color(0.03f, 0.04f, 0.08f, 1f);

    public const int ZombieVariants = 7;
    public const int MapCount = 3;

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private static TMP_FontAsset font;
    private static bool fontLoaded;

    private static Color Cc(float r, float g, float b, float a = 1f)
    {
        return new Color(r, g, b, a);
    }

    // =======================================================================
    // Public sprites
    // =======================================================================

    // A white rounded rectangle that stretches (Image.type = Sliced). Tint it with Image.color.
    public static Sprite RoundedBox(int radius)
    {
        string key = "box" + radius;
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
        int size = radius * 2 + 6;
        Painter p = new Painter(size, size);
        p.Rect(size * 0.5f, size * 0.5f, size * 0.5f, size * 0.5f, radius, Color.white, 0f, 0f);
        sprite = p.ToSprite(100f, radius + 2f);
        cache[key] = sprite;
        return sprite;
    }

    // The same shape as a thin hollow line (selection frames, card borders).
    public static Sprite RoundedOutline(int radius, int thickness)
    {
        string key = "outline" + radius + "_" + thickness;
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
        int size = radius * 2 + 6;
        Painter p = new Painter(size, size);
        p.Rect(size * 0.5f, size * 0.5f, size * 0.5f, size * 0.5f, radius, Color.white, 0f, thickness);
        sprite = p.ToSprite(100f, radius + 2f);
        cache[key] = sprite;
        return sprite;
    }

    public static Sprite Dot
    {
        get
        {
            Sprite sprite;
            if (cache.TryGetValue("dot", out sprite) && sprite != null) return sprite;
            Painter p = new Painter(32, 32);
            p.Circle(16f, 16f, 14f, Color.white);
            sprite = p.ToSprite(100f, 0f);
            cache["dot"] = sprite;
            return sprite;
        }
    }

    // A four-pointed twinkle.
    public static Sprite Sparkle
    {
        get
        {
            Sprite sprite;
            if (cache.TryGetValue("sparkle", out sprite) && sprite != null) return sprite;
            Painter p = new Painter(48, 48);
            p.Poly(new[] { new Vector2(24f, 2f), new Vector2(28f, 20f), new Vector2(46f, 24f), new Vector2(28f, 28f),
                new Vector2(24f, 46f), new Vector2(20f, 28f), new Vector2(2f, 24f), new Vector2(20f, 20f) }, Color.white);
            p.Glow(24f, 24f, 14f, Color.white, 0.8f);
            sprite = p.ToSprite(100f, 0f);
            cache["sparkle"] = sprite;
            return sprite;
        }
    }

    // A capsule zombie with a dark outline and a face, 160 x 160. variant 0 = plain grey,
    // 1..6 = the colours of the game's red / runner / splitter / ghost / spitter / armored zombies.
    public static Sprite Zombie(int variant)
    {
        variant = ((variant % ZombieVariants) + ZombieVariants) % ZombieVariants;
        string key = "zombie" + variant;
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
        sprite = MakeZombie(variant).ToSprite(100f, 0f);
        cache[key] = sprite;
        return sprite;
    }

    // The level pictures: 0 campus (dusk), 1 hollow forest (night), 2 coliseum (stadium). 512 x 384.
    public static Sprite Map(int index)
    {
        string key = "map" + index;
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
        Painter p = index == 0 ? PaintCampus() : (index == 1 ? PaintForest() : PaintColiseum());
        sprite = p.ToSprite(100f, 0f);
        cache[key] = sprite;
        return sprite;
    }

    // =======================================================================
    // Comic and terminal pieces for the title screen
    // =======================================================================

    // The tilted red tape under the keycap title: torn ends, dark outline and a hard shadow. 640 x 120.
    public static Sprite Tape
    {
        get
        {
            Sprite sprite;
            if (cache.TryGetValue("tape", out sprite) && sprite != null) return sprite;
            Painter p = new Painter(640, 120);
            const float x0 = 28f, x1 = 612f, y0 = 24f, y1 = 100f;
            Vector2[] body = ZigZagBand(x0, x1, y0, y1, 7, 10f);
            Vector2 center = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
            p.Poly(Offset(body, 8f, -8f), Ink);                                          // hard shadow
            p.Poly(Scale(body, center, (x1 - x0 + 14f) / (x1 - x0), (y1 - y0 + 12f) / (y1 - y0)), Ink); // outline
            p.Poly(body, Cc(0.87f, 0.15f, 0.16f));
            p.Poly(new[] { new Vector2(x0 + 6f, y1 - 6f), new Vector2(x1 - 6f, y1 - 6f), new Vector2(x1 - 6f, y1 - 20f), new Vector2(x0 + 6f, y1 - 20f) },
                Cc(1f, 1f, 1f, 0.12f));
            sprite = p.ToSprite(100f, 0f);
            cache["tape"] = sprite;
            return sprite;
        }
    }

    // A band with torn (zig-zag) left and right ends, clockwise.
    private static Vector2[] ZigZagBand(float x0, float x1, float y0, float y1, int teeth, float depth)
    {
        List<Vector2> points = new List<Vector2>();
        for (int i = 0; i <= teeth; i++)
        {
            points.Add(new Vector2(x0 - (i % 2 == 1 ? depth : 0f), y1 - (y1 - y0) * i / teeth));
        }
        for (int i = teeth; i >= 0; i--)
        {
            points.Add(new Vector2(x1 + (i % 2 == 0 ? depth : 0f), y1 - (y1 - y0) * i / teeth));
        }
        // the first loop ran top to bottom on the left, the second runs bottom to top on the right: a closed loop
        points.Reverse();
        return points.ToArray();
    }

    private static Vector2[] Offset(Vector2[] points, float dx, float dy)
    {
        Vector2[] result = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) result[i] = new Vector2(points[i].x + dx, points[i].y + dy);
        return result;
    }

    private static Vector2[] Scale(Vector2[] points, Vector2 center, float sx, float sy)
    {
        Vector2[] result = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
            result[i] = new Vector2(center.x + (points[i].x - center.x) * sx, center.y + (points[i].y - center.y) * sy);
        return result;
    }

    // The START button: a white slanted block with a dark outline and a hard shadow.
    // White so the Button's colour block can make it yellow. 560 x 150.
    public static Sprite ComicButton
    {
        get
        {
            Sprite sprite;
            if (cache.TryGetValue("comic", out sprite) && sprite != null) return sprite;
            Painter p = new Painter(560, 150);
            const float x0 = 20f, x1 = 536f, y0 = 28f, y1 = 134f, k = 22f;
            Vector2[] body = { new Vector2(x0, y0), new Vector2(x1 - k, y0), new Vector2(x1, y1), new Vector2(x0 + k, y1) };
            Vector2 center = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
            p.Poly(Offset(body, 10f, -10f), Ink);
            p.Poly(Scale(body, center, (x1 - x0 + 14f) / (x1 - x0), (y1 - y0 + 14f) / (y1 - y0)), Ink);
            p.Poly(body, Color.white);
            sprite = p.ToSprite(100f, 0f);
            cache["comic"] = sprite;
            return sprite;
        }
    }

    // A yellow comic burst for the BOOM! sticker. 200 x 200.
    public static Sprite Burst
    {
        get
        {
            Sprite sprite;
            if (cache.TryGetValue("burst", out sprite) && sprite != null) return sprite;
            Painter p = new Painter(200, 200);
            Vector2[] star = new Vector2[18];
            for (int i = 0; i < star.Length; i++)
            {
                float radius = i % 2 == 0 ? 82f : 51f;
                float a = Mathf.PI * i / 9f;
                star[i] = new Vector2(96f + radius * Mathf.Cos(a), 104f + radius * Mathf.Sin(a));
            }
            p.Poly(Offset(star, 7f, -7f), Ink);
            p.Poly(Scale(star, new Vector2(96f, 104f), 1.09f, 1.09f), Ink);
            p.Poly(star, Cc(1f, 0.83f, 0.08f));
            sprite = p.ToSprite(100f, 0f);
            cache["burst"] = sprite;
            return sprite;
        }
    }

    // The blast ring on the floor under the crowd: a flat red fill and a yellow line. 640 x 160.
    public static Sprite FloorRing
    {
        get
        {
            Sprite sprite;
            if (cache.TryGetValue("ring", out sprite) && sprite != null) return sprite;
            Painter p = new Painter(640, 160);
            p.Ellipse(320f, 80f, 312f, 72f, Cc(0.95f, 0.25f, 0.20f, 0.06f), 0f);
            p.Ellipse(320f, 80f, 312f, 72f, Cc(0.93f, 0.91f, 0.87f, 0.35f), 4f);
            sprite = p.ToSprite(100f, 0f);
            cache["ring"] = sprite;
            return sprite;
        }
    }

    // Rays from the middle that fade out: white and clear wedges (tint with Image.color). 512 x 512.
    public static Sprite Sunburst
    {
        get
        {
            Sprite sprite;
            if (cache.TryGetValue("sunburst", out sprite) && sprite != null) return sprite;
            const int Size = 512;
            const int Rays = 44;
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[Size * Size];
            float wedge = 2f * Mathf.PI / Rays;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - Size * 0.5f;
                    float dy = y + 0.5f - Size * 0.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    if (angle < 0f) angle += 2f * Mathf.PI;
                    float t = angle / wedge;
                    int index = Mathf.FloorToInt(t);
                    float frac = t - index;
                    float edge = Mathf.Min(frac, 1f - frac) * wedge * radius;   // pixels to the nearest ray edge
                    float coverage = Mathf.Clamp01(0.5f + (index % 2 == 0 ? edge : -edge));
                    float fade = Mathf.Clamp01(1f - radius / (Size * 0.5f));
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(coverage * fade * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            cache["sunburst"] = sprite;
            return sprite;
        }
    }

    // Thin dark lines, to repeat (Image.Type.Tiled) over a picture or the whole screen like a monitor.
    public static Sprite Scanlines
    {
        get
        {
            Sprite sprite;
            if (cache.TryGetValue("scan", out sprite) && sprite != null) return sprite;
            Texture2D texture = new Texture2D(2, 4, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 2; x++)
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, y == 0 ? 1f : 0f));
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 4f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            cache["scan"] = sprite;
            return sprite;
        }
    }

    // =======================================================================
    // Fonts
    // =======================================================================

    // The compact HUD font (Resources/Fonts/HudCompact SDF), or TextMeshPro's default.
    public static TMP_FontAsset Font
    {
        get
        {
            if (!fontLoaded)
            {
                font = Resources.Load<TMP_FontAsset>("Fonts/HudCompact SDF");
                fontLoaded = true;
            }
            return font;
        }
    }

    // The menu look: the HUD font, optionally with a thick dark outline (0 = none, 0.2 to 0.4 = chunky).
    public static void Style(TMP_Text label, float outline, Color outlineColor)
    {
        TMP_FontAsset asset = Font;
        if (asset == null)
        {
            asset = TMP_Settings.defaultFontAsset;
        }
        if (asset == null) return;
        label.font = asset;
        if (outline <= 0f) return;
        string key = asset.GetInstanceID() + "_" + outline + "_" + outlineColor;
        Material material;
        if (!materials.TryGetValue(key, out material) || material == null)
        {
            material = new Material(asset.material);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            materials[key] = material;
        }
        label.fontSharedMaterial = material;
    }

    // =======================================================================
    // The zombie: the game's capsule body, with a simple face
    // =======================================================================

    private enum Eyes { Dots, Angry, Wide, Happy, Sleepy, Crossed, Visor }
    private enum Mouth { Flat, Grin, Open, Smile, Tiny, Tongue, Zigzag }

    private static Painter MakeZombie(int variant)
    {
        // the colours of the game's zombies (Palette): plain, explosive, runner, splitter, ghost, spitter, armored
        Color[] colors =
        {
            Palette.ZombieGrey, Palette.ExplosiveRed, Palette.RunnerYellow, Palette.SplitterTeal,
            Palette.GhostPale, Palette.SpitterAcid, Palette.ArmorSteel
        };
        Eyes[] eyes = { Eyes.Dots, Eyes.Angry, Eyes.Wide, Eyes.Happy, Eyes.Sleepy, Eyes.Crossed, Eyes.Visor };
        Mouth[] mouths = { Mouth.Flat, Mouth.Grin, Mouth.Open, Mouth.Smile, Mouth.Tiny, Mouth.Tongue, Mouth.Zigzag };
        return DrawZombie(colors[variant], eyes[variant], mouths[variant], variant == 6);
    }

    // A thick arc made of short capsules (smiles, closed eyes). Angles in degrees.
    private static void Arc(Painter p, float cx, float cy, float radius, float from, float to, float thickness, Color color)
    {
        const int Steps = 14;
        Vector2 previous = Vector2.zero;
        for (int i = 0; i <= Steps; i++)
        {
            float a = Mathf.Deg2Rad * Mathf.Lerp(from, to, i / (float)Steps);
            Vector2 point = new Vector2(cx + radius * Mathf.Cos(a), cy + radius * Mathf.Sin(a));
            if (i > 0) p.Capsule(previous.x, previous.y, point.x, point.y, thickness, color);
            previous = point;
        }
    }

    private static Painter DrawZombie(Color body, Eyes eyes, Mouth mouth, bool armored)
    {
        Painter p = new Painter(160, 160);
        Color white = Cc(0.98f, 0.98f, 0.95f);

        p.Ellipse(80f, 12f, 44f, 8f, Cc(0f, 0f, 0f, 0.35f), 0f);     // shadow on the ground

        // the body: a dark outline first (a bit bigger), then the colour
        p.Capsule(80f, 46f, 80f, 100f, 40f, Ink);
        p.Capsule(80f, 46f, 80f, 100f, 34f, body);
        p.Ellipse(80f, 20f, 34f, 10f, Cc(body.r * 0.72f, body.g * 0.72f, body.b * 0.78f), 0f);   // darker foot
        p.Capsule(62f, 56f, 62f, 100f, 7f, Cc(1f, 1f, 1f, 0.28f));                                // shine
        p.Capsule(100f, 58f, 100f, 90f, 4f, Cc(0f, 0f, 0f, 0.10f));
        if (armored)
        {
            Color steel = Cc(0.2f, 0.25f, 0.35f);
            p.Rect(80f, 64f, 34f, 5f, 0f, steel, 0f, 0f);
            p.Circle(56f, 64f, 2.4f, Cc(0.85f, 0.9f, 1f));
            p.Circle(104f, 64f, 2.4f, Cc(0.85f, 0.9f, 1f));
        }

        // the eyes
        const float L = 64f, R = 96f, Y = 98f;
        switch (eyes)
        {
            case Eyes.Dots:
                p.Circle(L, Y, 5f, Ink);
                p.Circle(R, Y, 5f, Ink);
                break;
            case Eyes.Angry:
                p.Circle(L, Y, 11f, white);
                p.Circle(R, Y, 11f, white);
                p.Circle(L + 2f, Y - 1f, 5f, Ink);
                p.Circle(R - 2f, Y - 1f, 5f, Ink);
                p.Circle(L + 3f, Y, 1.6f, white);
                p.Circle(R - 1f, Y, 1.6f, white);
                p.Capsule(L - 12f, Y + 19f, L + 10f, Y + 11f, 3.5f, Ink);
                p.Capsule(R + 12f, Y + 19f, R - 10f, Y + 11f, 3.5f, Ink);
                break;
            case Eyes.Wide:
                p.Circle(L, Y, 12f, white);
                p.Circle(R, Y, 12f, white);
                p.Circle(L, Y, 4.5f, Ink);
                p.Circle(R, Y, 4.5f, Ink);
                p.Circle(L + 1.5f, Y + 2f, 1.6f, white);
                p.Circle(R + 1.5f, Y + 2f, 1.6f, white);
                break;
            case Eyes.Happy:
                Arc(p, L, Y - 4f, 8f, 25f, 155f, 3.2f, Ink);
                Arc(p, R, Y - 4f, 8f, 25f, 155f, 3.2f, Ink);
                break;
            case Eyes.Sleepy:
                Color lid = Cc(0.80f, 0.76f, 0.95f);
                p.Circle(L, Y, 11f, white);
                p.Circle(R, Y, 11f, white);
                p.Rect(L, Y + 6f, 13f, 7f, 0f, lid, 0f, 0f);
                p.Rect(R, Y + 6f, 13f, 7f, 0f, lid, 0f, 0f);
                p.Circle(L, Y - 2f, 4.5f, Ink);
                p.Circle(R, Y - 2f, 4.5f, Ink);
                p.Capsule(L - 11f, Y + 5f, L + 11f, Y + 5f, 2.6f, Ink);
                p.Capsule(R - 11f, Y + 5f, R + 11f, Y + 5f, 2.6f, Ink);
                break;
            case Eyes.Crossed:
                p.Capsule(L - 7f, Y - 7f, L + 7f, Y + 7f, 3f, Ink);
                p.Capsule(L - 7f, Y + 7f, L + 7f, Y - 7f, 3f, Ink);
                p.Capsule(R - 7f, Y - 7f, R + 7f, Y + 7f, 3f, Ink);
                p.Capsule(R - 7f, Y + 7f, R + 7f, Y - 7f, 3f, Ink);
                break;
            case Eyes.Visor:
                Color glass = Cc(0.5f, 0.9f, 1f, 0.9f);
                p.Rect(80f, Y, 30f, 8f, 4f, Ink, 0f, 0f);
                p.Rect(68f, Y + 1.5f, 6f, 2.4f, 1f, glass, 0f, 0f);
                p.Rect(92f, Y + 1.5f, 6f, 2.4f, 1f, glass, 0f, 0f);
                break;
        }

        // the mouth
        const float M = 68f;
        switch (mouth)
        {
            case Mouth.Flat:
                p.Capsule(70f, M, 90f, M, 2.8f, Ink);
                break;
            case Mouth.Grin:
                p.Ellipse(80f, M, 20f, 13f, Ink, 0f);
                p.Rect(80f, M + 8f, 16f, 4f, 1f, white, 0f, 0f);
                p.Rect(80f, M - 9f, 16f, 4f, 1f, white, 0f, 0f);
                break;
            case Mouth.Open:
                p.Ellipse(80f, M, 8f, 10f, Ink, 0f);
                break;
            case Mouth.Smile:
                Arc(p, 80f, M + 10f, 17f, 215f, 325f, 3.2f, Ink);
                break;
            case Mouth.Tiny:
                p.Ellipse(80f, M, 5f, 6f, Ink, 0f);
                break;
            case Mouth.Tongue:
                p.Ellipse(80f, M + 2f, 18f, 10f, Ink, 0f);
                p.Ellipse(86f, M - 6f, 8f, 9f, Cc(0.95f, 0.35f, 0.45f), 0f);
                break;
            case Mouth.Zigzag:
                Vector2[] zig = { new Vector2(64f, M), new Vector2(72f, M - 6f), new Vector2(80f, M + 2f), new Vector2(88f, M - 6f), new Vector2(96f, M) };
                for (int i = 0; i < zig.Length - 1; i++) p.Capsule(zig[i].x, zig[i].y, zig[i + 1].x, zig[i + 1].y, 2.8f, Ink);
                break;
        }
        return p;
    }

    // =======================================================================
    // The three level pictures
    // =======================================================================

    private static void Stars(Painter p, int count, int seed, float yMin, float yMax)
    {
        System.Random r = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            float x = (float)(r.NextDouble() * p.W);
            float y = yMin + (float)(r.NextDouble() * (yMax - yMin));
            float a = 0.3f + (float)r.NextDouble() * 0.7f;
            p.Circle(x, y, 0.6f + (float)r.NextDouble() * 0.8f, Cc(1f, 1f, 1f, a));
        }
    }

    // A zombie of the picture: stamped small, standing at (x, baseY).
    private static void Zomb(Painter p, Painter zombie, float x, float y, float scale, bool flip)
    {
        p.Stamp(zombie, x, y + scale * 40f, scale * 0.8f, flip);
    }

    // The red blast ring on the ground: the game's twist, in every picture.
    private static void BlastRing(Painter p, float cx, float cy, float rx, float ry)
    {
        p.Ellipse(cx, cy, rx, ry, Cc(0.95f, 0.25f, 0.20f, 0.14f), 0f);
        p.Ellipse(cx, cy, rx, ry, Cc(1f, 0.35f, 0.25f, 0.95f), 4f);
    }

    private static Painter PaintCampus()
    {
        Painter p = new Painter(512, 384);
        float h = 200f;
        p.VGradient(h, 383f, Cc(0.97f, 0.50f, 0.36f), Cc(0.10f, 0.07f, 0.27f));
        Stars(p, 50, 1, 260f, 384f);
        p.Glow(380f, h + 8f, 150f, Cc(1f, 0.75f, 0.4f), 0.9f);
        p.Circle(380f, h + 34f, 26f, Cc(1f, 0.86f, 0.55f));
        p.Glow(380f, h + 34f, 60f, Cc(1f, 0.8f, 0.5f), 0.6f);

        System.Random r = new System.Random(5);
        float x = 0f;
        while (x < 512f)
        {
            float w = r.Next(28, 53);
            float hh = r.Next(40, 101);
            p.Rect(x + w / 2f, h + hh / 2f - 2f, w / 2f, hh / 2f, 0f, Cc(0.30f, 0.17f, 0.34f), 0f, 0f);
            x += w - 2f;
        }
        r = new System.Random(9);
        x = -10f;
        while (x < 512f)
        {
            float w = r.Next(50, 81);
            float hh = r.Next(60, 121);
            p.Rect(x + w / 2f, h + hh / 2f - 2f, w / 2f, hh / 2f, 0f, Cc(0.17f, 0.10f, 0.24f), 0f, 0f);
            for (float wy = h + 10f; wy < h + hh - 8f; wy += 16f)
            {
                for (float wx = x + 8f; wx < x + w - 8f; wx += 14f)
                {
                    if (r.NextDouble() < 0.45)
                        p.Rect(wx, wy, 3.5f, 5f, 1f, Cc(1f, 0.82f, 0.45f, 0.9f), 0f, 0f);
                }
            }
            x += w + r.Next(-4, 11);
        }
        // the tower with a cardinal-red roof
        p.Rect(256f, h + 70f, 20f, 70f, 0f, Cc(0.42f, 0.14f, 0.18f), 0f, 0f);
        p.Poly(new[] { new Vector2(232f, h + 138f), new Vector2(280f, h + 138f), new Vector2(256f, h + 182f) }, Cc(0.62f, 0.10f, 0.16f));
        p.Rect(256f, h + 60f, 5f, 9f, 2f, Cc(1f, 0.8f, 0.4f), 0f, 0f);
        p.Circle(256f, h + 100f, 9f, Cc(1f, 0.85f, 0.5f));
        p.Capsule(256f, h + 182f, 256f, h + 205f, 1.5f, Cc(0.9f, 0.9f, 0.9f));

        // ground and road
        p.Rect(256f, h / 2f - 4f, 300f, h / 2f + 6f, 0f, Cc(0.10f, 0.07f, 0.16f), 0f, 0f);
        p.Poly(new[] { new Vector2(0f, h), new Vector2(512f, h), new Vector2(512f, h - 10f), new Vector2(0f, h - 10f) }, Cc(0.20f, 0.12f, 0.20f));
        p.Poly(new[] { new Vector2(238f, h), new Vector2(274f, h), new Vector2(520f, 0f), new Vector2(-8f, 0f) }, Cc(0.17f, 0.15f, 0.22f));
        p.Poly(new[] { new Vector2(238f, h), new Vector2(222f, h), new Vector2(-60f, 0f), new Vector2(-8f, 0f) }, Cc(0.30f, 0.22f, 0.30f));
        p.Poly(new[] { new Vector2(274f, h), new Vector2(290f, h), new Vector2(572f, 0f), new Vector2(520f, 0f) }, Cc(0.30f, 0.22f, 0.30f));
        for (int i = 0; i < 6; i++)
        {
            float t0 = i / 6f;
            float t1 = t0 + 0.08f;
            float y0 = h - h * Mathf.Pow(t0, 1.6f);
            float y1 = h - h * Mathf.Pow(t1, 1.6f);
            float w0 = 2f + (1f - y0 / h) * 7f;
            float w1 = 2f + (1f - y1 / h) * 7f;
            p.Poly(new[] { new Vector2(256f - w1, y1), new Vector2(256f + w1, y1), new Vector2(256f + w0, y0), new Vector2(256f - w0, y0) },
                Cc(1f, 0.82f, 0.35f, 0.9f));
        }
        // palms
        Palm(p, 56f, 24f, 120f, 12f);
        Palm(p, 450f, 24f, 130f, -14f);
        Palm(p, 130f, 70f, 80f, 8f);
        Palm(p, 390f, 70f, 84f, -8f);
        // street lamps
        float[] lampX = { 190f, 322f };
        for (int i = 0; i < lampX.Length; i++)
        {
            p.Capsule(lampX[i], h - 6f, lampX[i], h + 54f, 2f, Cc(0.10f, 0.10f, 0.14f));
            p.Glow(lampX[i], h + 56f, 34f, Cc(1f, 0.8f, 0.4f), 0.9f);
            p.Circle(lampX[i], h + 56f, 5f, Cc(1f, 0.92f, 0.7f));
        }
        // the horde inside a blast ring
        BlastRing(p, 256f, 70f, 150f, 38f);
        float[,] pos = { { 230f, 128f, 0.40f }, { 300f, 126f, 0.36f }, { 160f, 70f, 0.62f }, { 352f, 66f, 0.66f }, { 256f, 42f, 0.90f } };
        int[] kinds = { 3, 4, 5, 2, 1 };
        for (int i = 0; i < 5; i++)
        {
            Zomb(p, DrawZombieForScene(kinds[i]), pos[i, 0], pos[i, 1], pos[i, 2], i % 2 == 1);
        }
        return p;
    }

    private static void Palm(Painter p, float x, float baseY, float height, float lean)
    {
        float tx = x + lean;
        float ty = baseY + height;
        p.Capsule(x, baseY, tx, ty, 4f, Cc(0.14f, 0.09f, 0.12f));
        for (int ang = -70; ang < 260; ang += 40)
        {
            float a = ang * Mathf.Deg2Rad;
            p.Capsule(tx, ty, tx + Mathf.Cos(a) * 34f, ty + Mathf.Sin(a) * 20f - 4f, 3.2f, Cc(0.10f, 0.30f, 0.22f));
        }
    }

    private static readonly Dictionary<int, Painter> sceneZombies = new Dictionary<int, Painter>();

    private static Painter DrawZombieForScene(int variant)
    {
        Painter z;
        if (!sceneZombies.TryGetValue(variant, out z))
        {
            z = MakeZombie(variant);
            sceneZombies[variant] = z;
        }
        return z;
    }

    private static void Pine(Painter p, float x, float baseY, float height, float width, Color color)
    {
        for (int i = 0; i < 5; i++)
        {
            float t = i / 5f;
            float ww = width * (1f - t * 0.62f);
            float y = baseY + height * (0.18f + 0.2f * i);
            p.Poly(new[] { new Vector2(x - ww, y), new Vector2(x + ww, y), new Vector2(x, y + height * 0.34f) }, color);
        }
        p.Rect(x, baseY + height * 0.1f, width * 0.09f, height * 0.12f, 0f, color, 0f, 0f);
    }

    private static Painter PaintForest()
    {
        Painter p = new Painter(512, 384);
        p.VGradient(0f, 383f, Cc(0.20f, 0.33f, 0.30f), Cc(0.02f, 0.06f, 0.10f));
        Stars(p, 70, 3, 200f, 384f);
        p.Glow(110f, 320f, 120f, Cc(0.7f, 0.95f, 0.85f), 0.6f);
        p.Circle(110f, 320f, 22f, Cc(0.92f, 1f, 0.95f));
        p.Circle(103f, 326f, 5f, Cc(0.7f, 0.8f, 0.78f, 0.6f));

        System.Random r = new System.Random(2);
        for (int x = -10; x < 540; x += 34)
            Pine(p, x + r.Next(-8, 9), 150f + r.Next(0, 26), 130f, 26f, Cc(0.10f, 0.20f, 0.22f));
        p.Rect(256f, 150f, 300f, 40f, 0f, Cc(0.30f, 0.46f, 0.42f, 0.35f), 0f, 0f);
        for (int x = 0; x < 540; x += 52)
            Pine(p, x + r.Next(-10, 11), 110f + r.Next(0, 26), 170f, 34f, Cc(0.05f, 0.12f, 0.13f));
        p.Ellipse(256f, 140f, 300f, 22f, Cc(0.55f, 0.75f, 0.70f, 0.22f), 0f);
        p.Rect(256f, 50f, 300f, 60f, 0f, Cc(0.07f, 0.06f, 0.07f), 0f, 0f);
        // the dirt trail
        p.Poly(new[] { new Vector2(244f, 140f), new Vector2(268f, 140f), new Vector2(560f, 0f), new Vector2(-48f, 0f) }, Cc(0.28f, 0.20f, 0.15f));
        p.Poly(new[] { new Vector2(244f, 140f), new Vector2(268f, 140f), new Vector2(330f, 0f), new Vector2(190f, 0f) }, Cc(0.36f, 0.26f, 0.18f, 0.6f));
        // near trees at the edges
        Color dark = Cc(0.02f, 0.05f, 0.06f);
        Pine(p, 18f, 0f, 300f, 70f, dark);
        Pine(p, 488f, 0f, 320f, 76f, dark);
        Pine(p, 86f, 20f, 240f, 50f, Cc(0.03f, 0.07f, 0.08f));
        Pine(p, 430f, 20f, 250f, 54f, Cc(0.03f, 0.07f, 0.08f));
        // lanterns
        float[,] lanterns = { { 170f, 84f, 1f }, { 344f, 84f, 1f }, { 212f, 122f, 0.6f }, { 302f, 122f, 0.6f } };
        for (int i = 0; i < 4; i++)
        {
            float lx = lanterns[i, 0], ly = lanterns[i, 1], sc = lanterns[i, 2];
            p.Capsule(lx, ly, lx, ly + 52f * sc, 2.2f * sc, Cc(0.05f, 0.04f, 0.04f));
            p.Glow(lx, ly + 54f * sc, 56f * sc, Cc(1f, 0.62f, 0.2f), 0.95f);
            p.Rect(lx, ly + 54f * sc, 6f * sc, 8f * sc, 2f, Cc(1f, 0.85f, 0.45f), 0f, 0f);
        }
        BlastRing(p, 256f, 66f, 160f, 40f);
        float[,] pos = { { 222f, 120f, 0.42f }, { 296f, 118f, 0.40f }, { 176f, 76f, 0.62f }, { 342f, 72f, 0.66f }, { 256f, 40f, 0.95f } };
        int[] kinds = { 4, 5, 3, 6, 1 };
        for (int i = 0; i < 5; i++)
        {
            Zomb(p, DrawZombieForScene(kinds[i]), pos[i, 0], pos[i, 1], pos[i, 2], i % 2 == 1);
        }
        // fog in front and fireflies
        p.Ellipse(120f, 12f, 200f, 30f, Cc(0.7f, 0.85f, 0.82f, 0.18f), 0f);
        p.Ellipse(420f, 22f, 200f, 26f, Cc(0.7f, 0.85f, 0.82f, 0.16f), 0f);
        for (int i = 0; i < 26; i++)
        {
            p.Glow((float)(r.NextDouble() * 512.0), 40f + (float)(r.NextDouble() * 190.0), 7f, Cc(0.8f, 1f, 0.4f), 0.9f);
        }
        return p;
    }

    private static Painter PaintColiseum()
    {
        Painter p = new Painter(512, 384);
        p.VGradient(0f, 383f, Cc(0.20f, 0.12f, 0.30f), Cc(0.03f, 0.04f, 0.12f));
        Stars(p, 40, 4, 300f, 384f);
        // the stands: tiers of seats with a crowd of coloured dots
        p.Rect(256f, 250f, 290f, 66f, 0f, Cc(0.26f, 0.20f, 0.30f), 0f, 0f);
        for (int i = 0; i < 6; i++)
        {
            float y = 190f + i * 22f;
            p.Rect(256f, y + 11f, 270f + i * 4f, 11f, 0f, Cc(0.20f + 0.05f * i, 0.15f + 0.04f * i, 0.26f + 0.04f * i), 0f, 0f);
            System.Random sr = new System.Random(i + 11);
            for (int x = -250; x < 260; x += 13)
            {
                p.Circle(256f + x + sr.Next(-3, 4), y + sr.Next(4, 17), 2.6f,
                    Cc(0.6f + (float)sr.NextDouble() * 0.4f, 0.3f + (float)sr.NextDouble() * 0.5f, 0.3f + (float)sr.NextDouble() * 0.5f, 0.55f));
            }
        }
        // the peristyle: a row of arches
        Color cream = Cc(0.80f, 0.70f, 0.52f);
        Color archDark = Cc(0.10f, 0.06f, 0.10f);
        p.Rect(256f, 330f, 250f, 34f, 0f, cream, 0f, 0f);
        for (int x = 26; x < 500; x += 40)
        {
            p.Rect(x, 326f, 12f, 22f, 0f, archDark, 0f, 0f);
            p.Circle(x, 346f, 12f, archDark);
        }
        p.Rect(256f, 364f, 250f, 6f, 0f, Cc(0.92f, 0.84f, 0.66f), 0f, 0f);
        p.Rect(256f, 299f, 260f, 5f, 0f, Cc(0.92f, 0.84f, 0.66f), 0f, 0f);
        // the torch
        p.Rect(256f, 376f, 14f, 6f, 2f, Cc(0.25f, 0.20f, 0.20f), 0f, 0f);
        p.Glow(256f, 388f, 60f, Cc(1f, 0.6f, 0.2f), 1f);
        p.Circle(256f, 386f, 8f, Cc(1f, 0.85f, 0.4f));
        // floodlights with beams
        float[] lightX = { 60f, 452f };
        float[] lean = { 0.28f, -0.28f };
        for (int i = 0; i < 2; i++)
        {
            float lx = lightX[i];
            p.Glow(lx, 340f, 70f, Cc(1f, 0.95f, 0.8f), 0.9f);
            p.Circle(lx, 340f, 6f, Cc(1f, 1f, 0.9f));
            p.Poly(new[] { new Vector2(lx, 340f), new Vector2(lx + lean[i] * 300f - 60f, 40f), new Vector2(lx + lean[i] * 300f + 60f, 40f) },
                Cc(1f, 0.95f, 0.8f, 0.07f));
        }
        // the field
        p.VGradient(0f, 196f, Cc(0.06f, 0.22f, 0.14f), Cc(0.18f, 0.42f, 0.26f));
        p.Ellipse(256f, 70f, 200f, 50f, Cc(1f, 1f, 1f, 0.55f), 3f);
        p.Capsule(256f, 196f, 256f, 0f, 1.5f, Cc(1f, 1f, 1f, 0.4f));
        // the gates the zombies come from
        float[] gateX = { 70f, 190f, 322f, 442f };
        for (int i = 0; i < gateX.Length; i++)
        {
            p.Rect(gateX[i], 196f, 26f, 22f, 0f, Cc(0.04f, 0.03f, 0.06f), 0f, 0f);
            p.Circle(gateX[i], 196f, 26f, Cc(0.04f, 0.03f, 0.06f));
        }
        // the horde
        BlastRing(p, 256f, 78f, 170f, 42f);
        // back row, middle row, front row: spaced so that no two bodies touch
        float[,] pos =
        {
            { 150f, 140f, 0.34f }, { 200f, 142f, 0.34f }, { 312f, 142f, 0.34f }, { 362f, 140f, 0.34f },
            { 118f, 98f, 0.50f }, { 205f, 96f, 0.50f }, { 307f, 96f, 0.50f }, { 394f, 98f, 0.50f },
            { 160f, 56f, 0.66f }, { 352f, 56f, 0.66f }, { 256f, 40f, 0.95f }
        };
        int[] kinds = { 3, 5, 4, 2, 5, 2, 3, 6, 4, 5, 1 };
        for (int i = 0; i < 11; i++)
        {
            Zomb(p, DrawZombieForScene(kinds[i]), pos[i, 0], pos[i, 1], pos[i, 2], i % 2 == 1);
        }
        return p;
    }

    // =======================================================================
    // The little painter: anti-aliased shapes into a Color[] (origin bottom left)
    // =======================================================================

    private sealed class Painter
    {
        public readonly int W;
        public readonly int H;
        private readonly Color[] px;

        public Painter(int width, int height)
        {
            W = width;
            H = height;
            px = new Color[width * height];
        }

        // c over what is there, with coverage cov (0..1 at the edge).
        private void Over(int x, int y, Color c, float cov)
        {
            if (cov <= 0f) return;
            float a = c.a * (cov > 1f ? 1f : cov);
            if (a <= 0f) return;
            int i = y * W + x;
            Color d = px[i];
            float outA = a + d.a * (1f - a);
            if (outA <= 0.00001f) return;
            float k = d.a * (1f - a);
            px[i] = new Color((c.r * a + d.r * k) / outA, (c.g * a + d.g * k) / outA, (c.b * a + d.b * k) / outA, outA);
        }

        public void VGradient(float y0, float y1, Color bottom, Color top)
        {
            int from = Mathf.Max(0, Mathf.FloorToInt(y0));
            int to = Mathf.Min(H - 1, Mathf.FloorToInt(y1));
            for (int y = from; y <= to; y++)
            {
                float t = (y - y0) / Mathf.Max(1f, y1 - y0);
                Color c = Color.Lerp(bottom, top, Mathf.Clamp01(t));
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
        }

        // Fills every pixel near the shape whose signed distance f is below half a pixel.
        private void Shape(float minX, float minY, float maxX, float maxY, Func<float, float, float> f, Color c, float shell)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX) - 1);
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(maxX) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY) - 1);
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(maxY) + 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = f(x + 0.5f, y + 0.5f);
                    if (shell > 0f) d = Mathf.Abs(d + shell * 0.5f) - shell * 0.5f;
                    Over(x, y, c, 0.5f - d);
                }
            }
        }

        public void Circle(float cx, float cy, float r, Color c)
        {
            Shape(cx - r, cy - r, cx + r, cy + r, (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r, c, 0f);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c, float shell)
        {
            float m = Mathf.Min(rx, ry);
            Shape(cx - rx, cy - ry, cx + rx, cy + ry, (x, y) =>
            {
                float u = (x - cx) / rx;
                float v = (y - cy) / ry;
                return (Mathf.Sqrt(u * u + v * v) - 1f) * m;
            }, c, shell);
        }

        // A rounded rectangle (half sizes hw, hh; corner radius), turned by angle degrees; shell > 0 = only a line that thick.
        public void Rect(float cx, float cy, float hw, float hh, float radius, Color c, float angle, float shell)
        {
            radius = Mathf.Min(radius, Mathf.Min(hw, hh));
            float a = angle * Mathf.Deg2Rad;
            float ca = Mathf.Cos(a);
            float sa = Mathf.Sin(a);
            float reach = Mathf.Sqrt(hw * hw + hh * hh);
            Shape(cx - reach, cy - reach, cx + reach, cy + reach, (x, y) =>
            {
                float dx = x - cx;
                float dy = y - cy;
                float qx = Mathf.Abs(dx * ca + dy * sa) - (hw - radius);
                float qy = Mathf.Abs(-dx * sa + dy * ca) - (hh - radius);
                float ox = Mathf.Max(qx, 0f);
                float oy = Mathf.Max(qy, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            }, c, shell);
        }

        public void Capsule(float x0, float y0, float x1, float y1, float r, Color c)
        {
            float vx = x1 - x0;
            float vy = y1 - y0;
            float len = vx * vx + vy * vy + 1e-9f;
            Shape(Mathf.Min(x0, x1) - r, Mathf.Min(y0, y1) - r, Mathf.Max(x0, x1) + r, Mathf.Max(y0, y1) + r, (x, y) =>
            {
                float t = Mathf.Clamp01(((x - x0) * vx + (y - y0) * vy) / len);
                float dx = x - (x0 + vx * t);
                float dy = y - (y0 + vy * t);
                return Mathf.Sqrt(dx * dx + dy * dy) - r;
            }, c, 0f);
        }

        public void Poly(Vector2[] pts, Color c)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                minX = Mathf.Min(minX, pts[i].x);
                maxX = Mathf.Max(maxX, pts[i].x);
                minY = Mathf.Min(minY, pts[i].y);
                maxY = Mathf.Max(maxY, pts[i].y);
            }
            Shape(minX, minY, maxX, maxY, (x, y) => PolyDistance(pts, x, y), c, 0f);
        }

        // Distance to the polygon's edge: negative inside.
        private static float PolyDistance(Vector2[] pts, float x, float y)
        {
            float best = 1e9f;
            bool inside = false;
            int n = pts.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % n];
                float ex = b.x - a.x;
                float ey = b.y - a.y;
                float len = ex * ex + ey * ey + 1e-9f;
                float t = Mathf.Clamp01(((x - a.x) * ex + (y - a.y) * ey) / len);
                float dx = x - (a.x + ex * t);
                float dy = y - (a.y + ey * t);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y + 1e-9f) + a.x) inside = !inside;
            }
            return inside ? -best : best;
        }

        // A soft round glow (strongest in the middle).
        public void Glow(float cx, float cy, float r, Color c, float strength)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r));
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r));
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + r));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / r);
                    Over(x, y, c, d * d * strength);
                }
            }
        }

        // Another painting drawn onto this one, centred at (cx, cy) and scaled (4 samples per pixel).
        public void Stamp(Painter src, float cx, float cy, float scale, bool flip)
        {
            float hw = src.W * scale * 0.5f;
            float hh = src.H * scale * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - hw));
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + hw));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - hh));
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + hh));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int s = 0; s < 4; s++)
                    {
                        float ox = (s & 1) == 0 ? 0.25f : 0.75f;
                        float oy = (s & 2) == 0 ? 0.25f : 0.75f;
                        float u = (x + ox - (cx - hw)) / scale;
                        float v = (y + oy - (cy - hh)) / scale;
                        if (flip) u = src.W - u;
                        int ui = (int)u;
                        int vi = (int)v;
                        if (u < 0f || v < 0f || ui >= src.W || vi >= src.H) continue;
                        Color sample = src.px[vi * src.W + ui];
                        r += sample.r * sample.a;
                        g += sample.g * sample.a;
                        b += sample.b * sample.a;
                        a += sample.a;
                    }
                    if (a <= 0.0001f) continue;
                    Over(x, y, new Color(r / a, g / a, b / a, 1f), a / 4f);
                }
            }
        }

        // The texture and a sprite for it. border: the 9-slice border in pixels (0 = none).
        public Sprite ToSprite(float pixelsPerUnit, float border)
        {
            Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(px);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
