// MenuBackground.cs
// ---------------------------------------------------------------------------
// The scenery of the menu scene: a dark room lit red from above. Slow rays
// turn behind the title, faint words (from WordBank, the same ones the enemies
// carry) drift up the screen, thin monitor lines cover everything and the
// corners are shaded. Built by MainMenu in code; it never takes clicks and has
// nothing to do with gameplay.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuBackground : MonoBehaviour
{
    private const int WordCount = 14;
    private const float MinFontSize = 50f;
    private const float MaxFontSize = 130f;
    private const float SpeedPerFontUnit = 0.45f;   // canvas units per second per unit of font size (bigger = faster)
    private const float Margin = 160f;              // how far past the top / bottom edge a word starts and ends
    private const float RaysTurnPerSecond = 1.6f;   // degrees
    private static readonly Color SpaceColor = new Color(0.035f, 0.03f, 0.04f);
    private static readonly Color WordColor = new Color(1f, 1f, 1f, 0.025f);

    private RectTransform area;
    private RectTransform rays;
    private readonly List<TMP_Text> words = new List<TMP_Text>();
    private readonly List<float> speeds = new List<float>();
    private readonly List<char> noUsedLetters = new List<char>(); // WordBank wants the first letters in use: none here
    private bool placed;

    // Makes the layer under parent (full screen) and everything on it.
    public static MenuBackground Create(RectTransform parent)
    {
        RectTransform rect = Ui.Rect(parent, "Background");
        Ui.Stretch(rect);
        MenuBackground background = rect.gameObject.AddComponent<MenuBackground>();
        background.Build(rect);
        return background;
    }

    private void Build(RectTransform layer)
    {
        area = layer;
        Image sky = Ui.Panel(layer, "Room", SpaceColor);
        Ui.Stretch(sky.rectTransform);

        // a red light from above, and the rays turning behind the title
        Image light = Ui.Panel(layer, "Red Light", new Color(0.78f, 0.12f, 0.14f, 0.10f));
        light.sprite = Ui.Glow;
        Ui.Place(light.rectTransform, Ui.TopCentre, Ui.TopCentre, new Vector2(0f, 500f), new Vector2(2600f, 1700f));

        rays = Ui.Rect(layer, "Rays");
        Image sunburst = rays.gameObject.AddComponent<Image>();
        sunburst.sprite = MenuArt.Sunburst;
        sunburst.color = new Color(0.85f, 0.14f, 0.14f, 0.035f);
        sunburst.raycastTarget = false;
        Ui.Place(rays, Ui.TopCentre, Ui.Centre, new Vector2(0f, -330f), new Vector2(3400f, 3400f));

        for (int i = 0; i < WordCount; i++)
        {
            Image holder = Ui.Panel(layer, "Word", Color.clear);
            RectTransform rect = holder.rectTransform;
            rect.anchorMin = Ui.Centre;
            rect.anchorMax = Ui.Centre;
            rect.sizeDelta = new Vector2(900f, 180f);

            TMP_Text label = Ui.Label(rect, "Text", "", Random.Range(MinFontSize, MaxFontSize), FontStyles.Bold);
            MenuArt.Style(label, 0f, Color.black);
            label.color = WordColor;
            Ui.Stretch(label.rectTransform);
            words.Add(label);
            speeds.Add(label.fontSize * SpeedPerFontUnit);
        }

        // monitor lines
        Image scan = Ui.Panel(layer, "Scanlines", new Color(1f, 1f, 1f, 0.04f));
        scan.sprite = MenuArt.Scanlines;
        scan.type = Image.Type.Tiled;
        scan.pixelsPerUnitMultiplier = 0.7f;
        Ui.Stretch(scan.rectTransform);

        // dark edges keep the corners calm and the buttons readable
        AddShade(layer, "Top Shade", false, 260f, 0.85f);
        AddShade(layer, "Bottom Shade", true, 300f, 0.85f);
    }

    private static void AddShade(RectTransform layer, string objectName, bool bottom, float height, float alpha)
    {
        Image shade = Ui.Panel(layer, objectName, new Color(0.02f, 0.01f, 0.04f, alpha));
        shade.sprite = Ui.Gradient;     // opaque at the top, clear at the bottom
        RectTransform rect = shade.rectTransform;
        rect.anchorMin = bottom ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
        rect.anchorMax = bottom ? new Vector2(1f, 0f) : new Vector2(1f, 1f);
        rect.pivot = bottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, height);
        rect.localScale = bottom ? new Vector3(1f, -1f, 1f) : Vector3.one;
    }

    private void Update()
    {
        // The canvas has no size on its first frame: place the words once it has.
        if (!placed)
        {
            if (area.rect.width < 1f)
            {
                return;
            }
            for (int i = 0; i < words.Count; i++)
            {
                Respawn(i, true);
            }
            placed = true;
        }

        rays.localEulerAngles = new Vector3(0f, 0f, rays.localEulerAngles.z + RaysTurnPerSecond * Time.unscaledDeltaTime);

        float top = area.rect.height * 0.5f + Margin;
        for (int i = 0; i < words.Count; i++)
        {
            RectTransform rect = (RectTransform)words[i].transform.parent;
            Vector2 position = rect.anchoredPosition;
            position.y += speeds[i] * Time.unscaledDeltaTime;
            if (position.y > top)
            {
                Respawn(i, false);
            }
            else
            {
                rect.anchoredPosition = position;
            }
        }
    }

    // A new word at a random x: anywhere on screen at the start, below the bottom edge afterwards.
    private void Respawn(int index, bool anywhere)
    {
        Rect screen = area.rect;
        words[index].text = WordBank.PickWord(noUsedLetters, ZombieKind.Normal).ToLowerInvariant();
        float x = Random.Range(-screen.width * 0.5f, screen.width * 0.5f);
        float y = anywhere ? Random.Range(-screen.height * 0.5f, screen.height * 0.5f) : -screen.height * 0.5f - Margin;
        ((RectTransform)words[index].transform.parent).anchoredPosition = new Vector2(x, y);
    }
}
