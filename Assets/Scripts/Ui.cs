// Ui.cs
// ---------------------------------------------------------------------------
// The shared LOOK of the menus (main menu, almanac, pause panel, Endless
// reward panel): one palette and a few small builders, so every screen reads
// as the same game. Everything is plain rectangles and TextMeshPro text, plus
// two tiny textures generated in code (a gradient and a soft glow): no image
// files, no external art.
//
//   Ui.Panel(parent, "Name", color)      a rectangle
//   Ui.Label(parent, "Name", "TEXT", 28) a text (centred, no wrapping)
//   Ui.Caps(label, 6)                    letter-spaced capitals
//   Ui.Place / Ui.Stretch                anchors and sizes
//   Ui.Gradient / Ui.Glow                the two generated sprites
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class Ui
{
    // ---- Palette ----
    public static readonly Color Ink = new Color(0.027f, 0.039f, 0.067f);          // the darkest background
    public static readonly Color PanelColor = new Color(0.047f, 0.066f, 0.108f, 0.94f);
    public static readonly Color PanelLight = new Color(0.09f, 0.12f, 0.18f, 0.95f);
    public static readonly Color Line = new Color(1f, 1f, 1f, 0.09f);              // hairlines
    public static readonly Color Teal = new Color(0.25f, 0.86f, 0.80f);            // the accent
    public static readonly Color Gold = new Color(1f, 0.80f, 0.38f);               // selection, keys
    public static readonly Color Text = new Color(0.94f, 0.96f, 0.98f);
    public static readonly Color SubText = new Color(0.60f, 0.64f, 0.70f);
    public static readonly Color Faint = new Color(0.36f, 0.40f, 0.47f);
    public static readonly Color Danger = new Color(1f, 0.36f, 0.30f);

    public static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
    public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    public static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
    public static readonly Vector2 TopRight = new Vector2(1f, 1f);
    public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    public static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
    public static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    public static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
    public static readonly Vector2 RightMiddle = new Vector2(1f, 0.5f);

    // ---- Builders ----

    public static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static Image Panel(Transform parent, string name, Color color, bool raycast = false)
    {
        RectTransform rect = Rect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    public static TMP_Text Label(Transform parent, string name, string text, float size,
        FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        RectTransform rect = Rect(parent, name);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = align;
        label.color = Text;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    // Letter-spaced capitals (headings, small labels).
    public static TMP_Text Caps(TMP_Text label, float spacing)
    {
        label.characterSpacing = spacing;
        label.fontStyle |= FontStyles.UpperCase;
        return label;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    // A thin frame (four hairlines) around a rectangle.
    public static RectTransform Frame(Transform parent, Color color, float thickness = 2f)
    {
        RectTransform frame = Rect(parent, "Frame");
        Stretch(frame);
        Edge(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), color);
        Edge(frame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), color);
        Edge(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), color);
        Edge(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), color);
        return frame;
    }

    private static void Edge(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
    {
        Image bar = Panel(parent, "Edge", color);
        RectTransform rect = bar.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    public static void SetFrameColor(RectTransform frame, Color color)
    {
        foreach (Image edge in frame.GetComponentsInChildren<Image>(true))
        {
            edge.color = color;
        }
    }

    // A key cap with a key name on it ("ENTER", "5"), for footers and prompts.
    public static RectTransform KeyCap(Transform parent, string key, float height = 40f)
    {
        float width = Mathf.Max(height, 22f + key.Length * height * 0.36f);
        Image edge = Panel(parent, "Key " + key, new Color(0.36f, 0.42f, 0.5f));
        edge.rectTransform.sizeDelta = new Vector2(width, height);
        Image face = Panel(edge.transform, "Face", new Color(0.11f, 0.15f, 0.2f));
        Stretch(face.rectTransform, 2f, 4f, 2f, 2f);
        TMP_Text text = Label(face.transform, "Text", key, height * 0.46f, FontStyles.Bold);
        Stretch(text.rectTransform);
        return edge.rectTransform;
    }

    // ---- Generated sprites ----

    private static Sprite gradient, glow;

    // White at the top fading to clear at the bottom (tint it with Image.color).
    public static Sprite Gradient
    {
        get
        {
            if (gradient == null)
            {
                const int Size = 64;
                Texture2D texture = new Texture2D(1, Size, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < Size; y++)
                {
                    float t = y / (Size - 1f);
                    texture.SetPixel(0, y, new Color(1f, 1f, 1f, t * t));
                }
                texture.Apply();
                gradient = Sprite.Create(texture, new UnityEngine.Rect(0, 0, 1, Size), new Vector2(0.5f, 0.5f));
            }
            return gradient;
        }
    }

    // A soft round glow: white in the middle fading to clear at the edge.
    public static Sprite Glow
    {
        get
        {
            if (glow == null)
            {
                const int Size = 96;
                Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = (x + 0.5f) / Size * 2f - 1f;
                        float dy = (y + 0.5f) / Size * 2f - 1f;
                        float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
                    }
                }
                texture.Apply();
                glow = Sprite.Create(texture, new UnityEngine.Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            }
            return glow;
        }
    }

    // Smooth 0..1 (ease out cubic).
    public static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }
}
