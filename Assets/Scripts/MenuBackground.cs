// MenuBackground.cs
// ---------------------------------------------------------------------------
// Decoration for the menu scene: faint words (from WordBank, the same ones the
// enemies carry) drift slowly up the screen behind the menu. Built by
// MainMenu in code; it never takes clicks and has nothing to do with gameplay.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MenuBackground : MonoBehaviour
{
    private const int WordCount = 14;
    private const float MinFontSize = 40f;
    private const float MaxFontSize = 90f;
    private const float SpeedPerFontUnit = 0.8f;   // canvas units per second per unit of font size (bigger = faster)
    private const float Margin = 120f;             // how far past the top / bottom edge a word starts and ends
    private static readonly Color WordColor = new Color(1f, 1f, 1f, 0.02f);

    private RectTransform area;
    private readonly List<TMP_Text> words = new List<TMP_Text>();
    private readonly List<float> speeds = new List<float>();
    private readonly List<char> noUsedLetters = new List<char>(); // WordBank wants the first letters in use: none here
    private bool placed;

    // Makes the layer under parent (full screen) and its words.
    public static MenuBackground Create(RectTransform parent)
    {
        GameObject holder = new GameObject("Background Words", typeof(RectTransform));
        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        MenuBackground background = holder.AddComponent<MenuBackground>();
        background.Build(rect);
        return background;
    }

    private void Build(RectTransform layer)
    {
        area = layer;
        for (int i = 0; i < WordCount; i++)
        {
            GameObject wordObject = new GameObject("Word", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = wordObject.GetComponent<RectTransform>();
            rect.SetParent(layer, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700f, 120f);

            TextMeshProUGUI label = wordObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = Random.Range(MinFontSize, MaxFontSize);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = WordColor;
            label.raycastTarget = false; // never blocks a button

            words.Add(label);
            speeds.Add(label.fontSize * SpeedPerFontUnit);
        }
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

        float top = area.rect.height * 0.5f + Margin;
        for (int i = 0; i < words.Count; i++)
        {
            RectTransform rect = words[i].rectTransform;
            Vector2 position = rect.anchoredPosition;
            position.y += speeds[i] * Time.deltaTime;
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
        words[index].rectTransform.anchoredPosition = new Vector2(x, y);
    }
}