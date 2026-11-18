// HudIcons.cs
// ---------------------------------------------------------------------------
// Small white icons for the skill rows in the bottom-left corner of the HUD
// (HUD.BuildPowers / BuildShield). They are DRAWN IN CODE, once, into a small
// texture (no image files): each icon is a few simple shapes (segments,
// circles, polygons) measured as distances, so the edges are anti-aliased
// and the icon stays crisp at any size. HUD tints them with the power's
// colour (Image.color), like the key caps and names.
//
//   LURE    a magnet (it pulls the zombies in)
//   FREEZE  a snowflake
//   RPG     a rocket
//   FRENZY  a lightning bolt
//   SHIELD  a shield
//
// Usage: image.sprite = HudIcons.Get(HudIcons.Icon.Freeze);
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class HudIcons
{
    public enum Icon { Lure, Freeze, Rpg, Frenzy, Shield }

    private const int Size = 64; // texture size in pixels (shown at about 26 px)

    private static readonly Dictionary<Icon, Sprite> cache = new Dictionary<Icon, Sprite>();

    public static Sprite Get(Icon icon)
    {
        Sprite sprite;
        if (cache.TryGetValue(icon, out sprite) && sprite != null)
        {
            return sprite;
        }
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.name = "HudIcon " + icon;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[Size * Size];
        float pixel = 2f / Size; // one pixel in the icon's -1..1 space
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                float distance = Distance(icon, p);
                byte alpha = (byte)(Mathf.Clamp01(0.5f - distance / pixel) * 255f);
                pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
        sprite.name = texture.name;
        cache[icon] = sprite;
        return sprite;
    }

    // Signed distance to the icon's shape (negative inside), in -1..1 space.
    private static float Distance(Icon icon, Vector2 p)
    {
        switch (icon)
        {
            case Icon.Lure: return Magnet(p);
            case Icon.Freeze: return Snowflake(p);
            case Icon.Rpg: return Rocket(p);
            case Icon.Frenzy: return Polygon(p, Bolt);
            default: return ShieldShape(p);
        }
    }

    // A horseshoe magnet: a half ring at the bottom with two legs; the tips
    // are cut off by a thin gap so they read as the poles.
    private static float Magnet(Vector2 p)
    {
        const float radius = 0.5f, half = 0.17f;
        p.y += 0.05f;
        float arc = p.y <= 0f
            ? Mathf.Abs(p.magnitude - radius) - half
            : Mathf.Min(Box(p - new Vector2(-radius, 0.4f), new Vector2(half, 0.4f)),
                        Box(p - new Vector2(radius, 0.4f), new Vector2(half, 0.4f)));
        float gap = Mathf.Abs(p.y - 0.5f) - 0.045f; // the poles
        return Mathf.Max(arc, -gap);
    }

    // Three bars through the centre, each arm with a small V near its end.
    private static float Snowflake(Vector2 p)
    {
        const float arm = 0.88f, width = 0.075f;
        float d = float.MaxValue;
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.PI / 2f + i * Mathf.PI / 3f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new Vector2(-dir.y, dir.x);
            d = Mathf.Min(d, Segment(p, Vector2.zero, dir * arm) - width);
            Vector2 fork = dir * 0.5f;
            d = Mathf.Min(d, Segment(p, fork, fork + (dir + side) * 0.2f) - width * 0.85f);
            d = Mathf.Min(d, Segment(p, fork, fork + (dir - side) * 0.2f) - width * 0.85f);
        }
        return d;
    }

    // A rocket flying up and to the right: a rounded body with a pointed
    // nose, two fins at the tail and a small flame.
    private static float Rocket(Vector2 p)
    {
        const float zoom = 0.86f; // drawn a little larger than the others (it is thin)
        p *= zoom;
        // Work in the rocket's own frame: x along it, y across it.
        Vector2 dir = new Vector2(1f, 1f).normalized;
        Vector2 q = new Vector2(Vector2.Dot(p, dir), Vector2.Dot(p, new Vector2(-dir.y, dir.x)));
        float body = Segment(q, new Vector2(-0.42f, 0f), new Vector2(0.38f, 0f)) - 0.2f;
        float nose = Polygon(q, new[] { new Vector2(0.3f, 0.2f), new Vector2(0.86f, 0f), new Vector2(0.3f, -0.2f) });
        float finA = Polygon(q, new[] { new Vector2(-0.5f, 0.12f), new Vector2(-0.18f, 0.12f), new Vector2(-0.58f, 0.46f) });
        float finB = Polygon(q, new[] { new Vector2(-0.5f, -0.12f), new Vector2(-0.58f, -0.46f), new Vector2(-0.18f, -0.12f) });
        float flame = Polygon(q, new[] { new Vector2(-0.62f, 0.1f), new Vector2(-0.95f, 0f), new Vector2(-0.62f, -0.1f) });
        float window = Circle(q - new Vector2(0.1f, 0f), 0.085f); // a round hole
        float rocket = Mathf.Min(Mathf.Min(body, nose), Mathf.Min(finA, finB));
        return Mathf.Min(Mathf.Max(rocket, -window), flame) / zoom;
    }

    private static readonly Vector2[] Bolt =
    {
        new Vector2(0.22f, 0.92f), new Vector2(-0.5f, -0.08f), new Vector2(-0.04f, -0.08f),
        new Vector2(-0.24f, -0.92f), new Vector2(0.52f, 0.16f), new Vector2(0.06f, 0.16f)
    };

    // A heater shield: straight top, round shoulders, pointed bottom, with a
    // thin line inside so it does not read as a plain blob.
    private static float ShieldShape(Vector2 p)
    {
        Vector2[] outline =
        {
            new Vector2(-0.7f, 0.78f), new Vector2(0.7f, 0.78f), new Vector2(0.7f, 0.1f),
            new Vector2(0.38f, -0.5f), new Vector2(0f, -0.9f), new Vector2(-0.38f, -0.5f), new Vector2(-0.7f, 0.1f)
        };
        float shape = Polygon(p, outline);
        float inner = Mathf.Abs(shape + 0.17f) - 0.05f; // a ring just inside the edge
        return Mathf.Max(shape, -inner);
    }

    // ---- Distance helpers ----

    private static float Circle(Vector2 p, float radius)
    {
        return p.magnitude - radius;
    }

    private static float Box(Vector2 p, Vector2 half)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x) - half.x, Mathf.Abs(p.y) - half.y);
        Vector2 outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return (p - (a + ab * t)).magnitude;
    }

    // Signed distance to a closed polygon (any winding; negative inside).
    private static float Polygon(Vector2 p, Vector2[] points)
    {
        float d = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            Vector2 a = points[j], b = points[i];
            d = Mathf.Min(d, Segment(p, a, b));
            if ((b.y > p.y) != (a.y > p.y) && p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x)
            {
                inside = !inside;
            }
        }
        return inside ? -d : d;
    }
}
