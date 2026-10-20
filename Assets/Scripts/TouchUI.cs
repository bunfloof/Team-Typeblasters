// TouchUI.cs
// ---------------------------------------------------------------------------
// Small helpers that build the touch controls' UI pieces in code (like HUD
// does): plain rectangles (an Image without a sprite draws a flat rectangle in
// its colour) and TextMeshPro texts with the default font. Sizes are in canvas
// units: the HUD canvas is always 1080 units tall (Canvas Scaler, match height).
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TouchUI
{
    // An empty RectTransform under parent.
    public static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    // A flat coloured rectangle under parent.
    public static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    // A centred, non-wrapping text under parent that fills it.
    public static TMP_Text CreateText(string objectName, Transform parent, string text, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = Color.white;
        label.raycastTarget = false; // touches go to the key behind the text
        Stretch(label.rectTransform);
        return label;
    }

    // Makes rect fill its parent.
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Puts rect at position (canvas units) from the anchor point of its parent.
    // anchor and pivot: (0,0) = bottom-left .. (1,1) = top-right.
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
