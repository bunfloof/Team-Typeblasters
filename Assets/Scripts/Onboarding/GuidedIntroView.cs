// GuidedIntroView.cs
// ---------------------------------------------------------------------------
// What the guided first wave (GuidedIntro) draws, built in code like the HUD.
// Everything sits LOW on the screen, just above the bottom word box, so the
// middle of the view (the zombies, their words, where a lure bomb lands)
// always stays clear:
//   - the CARD: a title and one short line on the left, and for a power its
//     steps on the right as key caps / the arrow keys (drawn shapes, no font
//     symbols) with one word under each. It fades and rises in, and fades
//     to half while the player aims a lure bomb;
//   - a LINE: the same card with a single short sentence ("Keep shooting!"),
//     which goes away by itself;
//   - a bobbing ARROW over the word to type (a zombie, a barrel, a box);
//   - a "skip intro" chip at the start (ENTER, or tap it);
//   - a light dark TINT over the 3D view while the world is held.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GuidedIntroView : MonoBehaviour
{
    private static readonly Color CardColor = new Color(0.035f, 0.05f, 0.08f, 1f);
    private static readonly Color Teal = new Color(0.25f, 0.86f, 0.8f);
    private static readonly Color Gold = new Color(1f, 0.8f, 0.38f);
    private static readonly Color CapFace = new Color(0.11f, 0.15f, 0.2f, 1f);
    private static readonly Color CapEdge = new Color(0.36f, 0.42f, 0.5f, 1f);
    private static readonly Color Grey = new Color(0.62f, 0.66f, 0.72f);
    private static readonly Color Divider = new Color(1f, 1f, 1f, 0.08f);

    private const float CardBottom = 182f;     // canvas units above the bottom edge: just above the word box (its top is at 160)
    private const float Pad = 26f;
    private const float TextMaxWidth = 520f;   // the left text block wraps at this width when there are steps
    private const float LineMaxWidth = 980f;   // a text-only card wraps at this width
    private const float StepWidth = 128f;
    private const float StepGap = 38f;
    private const float StepsHeight = 112f;
    private const float HoldTint = 0.22f;
    private const float AimingAlpha = 0.45f;   // while 1 is held the card fades, so the landing point shows through

    private RectTransform root, card, accent, steps, divider, pointer, skipChip;
    private HUD hud;
    private float cardShift;                  // canvas units the card is moved right to keep clear of the power list
    private int cardPositionFrame = -1;
    private readonly Vector3[] cardCorners = new Vector3[4];
    private CanvasGroup cardGroup;
    private Image tint;
    private TMP_Text title, body, continueChip;
    private ITypingTarget pointed;
    private float nudgeAt = -100f;
    private float shownAt = -100f;
    private bool cardWanted;
    private float lineUntil = -1f;            // > 0: the card is a LINE and goes away at this time (unscaled)

    // True while a card or line is on screen (HUD keeps its hint box away meanwhile).
    public static bool CardVisible { get; private set; }

    public static GuidedIntroView Create(HUD hud)
    {
        Transform canvas = hud.WordLayer.parent;
        var go = new GameObject("Guided Intro", typeof(RectTransform));
        var view = go.AddComponent<GuidedIntroView>();
        view.root = (RectTransform)go.transform;
        view.root.SetParent(canvas, false);
        Stretch(view.root);
        view.root.SetAsLastSibling(); // over the HUD

        // The tint goes BEHIND the words and the HUD: it only darkens the 3D view.
        var tintGo = new GameObject("Guided Intro Tint", typeof(RectTransform), typeof(Image));
        var tintRect = (RectTransform)tintGo.transform;
        tintRect.SetParent(canvas, false);
        Stretch(tintRect);
        tintRect.SetAsFirstSibling();
        view.tint = tintGo.GetComponent<Image>();
        view.tint.color = new Color(0f, 0f, 0f, 0f);
        view.tint.raycastTarget = false;

        view.hud = hud;
        view.Build();
        return view;
    }

    private void Build()
    {
        card = Rect("Card", root, new Vector2(0.5f, 0f), new Vector2(0f, CardBottom), new Vector2(600f, 120f));
        card.pivot = new Vector2(0.5f, 0f);
        Image plate = card.gameObject.AddComponent<Image>();
        plate.color = CardColor;
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        card.gameObject.AddComponent<CardClick>();

        // A thin teal strip along the left edge (like the HUD's hint box).
        accent = Rect("Accent", card, Vector2.zero, Vector2.zero, Vector2.zero);
        accent.anchorMin = new Vector2(0f, 0f);
        accent.anchorMax = new Vector2(0f, 1f);
        accent.pivot = new Vector2(0f, 0.5f);
        accent.sizeDelta = new Vector2(6f, 0f);
        Image accentImage = accent.gameObject.AddComponent<Image>();
        accentImage.color = Teal;
        accentImage.raycastTarget = false;

        title = Text("Title", card, "", 30f, FontStyles.Bold);
        title.color = Teal;
        title.characterSpacing = 4f;
        body = Text("Body", card, "", 26f, FontStyles.Normal);
        body.textWrappingMode = TextWrappingModes.Normal;

        divider = Rect("Divider", card, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2f, 0f));
        Image dividerImage = divider.gameObject.AddComponent<Image>();
        dividerImage.color = Divider;
        dividerImage.raycastTarget = false;

        steps = Rect("Steps", card, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10f, StepsHeight));
        steps.pivot = new Vector2(0f, 0.5f);

        continueChip = Text("Continue", card, "", 18f, FontStyles.Bold);
        continueChip.color = Grey;
        continueChip.characterSpacing = 3f;
        card.gameObject.SetActive(false);

        pointer = Rect("Pointer", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 60f));
        Arrow(pointer, Gold, 0.9f, 0f);
        pointer.gameObject.SetActive(false);

        skipChip = Rect("Skip", root, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(300f, 48f));
        Image skipPlate = skipChip.gameObject.AddComponent<Image>();
        skipPlate.color = new Color(0.035f, 0.05f, 0.08f, 0.8f);
        Button skipButton = skipChip.gameObject.AddComponent<Button>();
        skipButton.targetGraphic = skipPlate;
        skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
        skipButton.onClick.AddListener(GuidedIntro.OnEnter);
        TMP_Text skipText = Text("Label", skipChip, TouchControls.Active ? "TAP TO SKIP INTRO" : "ENTER   SKIP INTRO", 20f, FontStyles.Bold);
        skipText.color = Grey;
        skipText.characterSpacing = 3f;
        Fill(skipText.rectTransform);
        skipChip.gameObject.SetActive(false);
    }

    // ---- What GuidedIntro calls ----

    // A card: heading (may be ""), one short line, and optionally the steps
    // of a power. showContinue adds "ENTER" (a read-only card).
    public void ShowCard(string heading, string line, GuideStep[] howSteps, bool showContinue)
    {
        lineUntil = -1f;
        Layout(heading, line, howSteps, showContinue);
    }

    // A single sentence that goes away by itself after 'seconds'.
    public void ShowLine(string line, float seconds)
    {
        Layout("", line, null, false);
        lineUntil = Time.unscaledTime + seconds;
    }

    public void HideCard()
    {
        cardWanted = false;
        lineUntil = -1f;
    }

    // Hides a LINE (not a card) that is still up.
    public void HideLine()
    {
        if (lineUntil > 0f)
        {
            HideCard();
        }
    }

    public void ShowSkip(bool on)
    {
        skipChip.gameObject.SetActive(on);
    }

    public void PointAt(ITypingTarget target)
    {
        pointed = target;
    }

    public void Nudge()
    {
        nudgeAt = Time.unscaledTime;
    }

    public void Close()
    {
        CardVisible = false;
        if (tint != null)
        {
            Destroy(tint.gameObject);
        }
        Destroy(gameObject);
    }

    internal bool TryGetCardBounds(RectTransform wordLayer, out Rect bounds)
    {
        bounds = default;
        if (!card.gameObject.activeInHierarchy) return false;

        PositionCard();
        card.GetWorldCorners(cardCorners);
        Vector2 min = wordLayer.InverseTransformPoint(cardCorners[0]);
        Vector2 max = min;
        for (int i = 1; i < cardCorners.Length; i++)
        {
            Vector2 corner = wordLayer.InverseTransformPoint(cardCorners[i]);
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }
        const float gap = 12f;
        bounds = UnityEngine.Rect.MinMaxRect(min.x - gap, min.y - gap, max.x + gap, max.y + gap);
        return true;
    }

    // ---- Every frame ----

    private void LateUpdate()
    {
        // The tint fades in while the world is held.
        float tintAlpha = Mathf.MoveTowards(tint.color.a, GuidedIntro.Holding ? HoldTint : 0f, Time.unscaledDeltaTime * 1.2f);
        tint.color = new Color(0f, 0f, 0f, tintAlpha);

        // A line goes away by itself.
        if (lineUntil > 0f && Time.unscaledTime >= lineUntil)
        {
            HideCard();
        }

        // Fade and rise in; fade out; half see-through while aiming.
        bool aiming = GameManager.Instance != null && GameManager.Instance.Powers != null && GameManager.Instance.Powers.IsAiming;
        float wanted = cardWanted ? (aiming ? AimingAlpha : 1f) : 0f;
        cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, wanted, Time.unscaledDeltaTime * (cardWanted ? 6f : 8f));
        if (!cardWanted && cardGroup.alpha <= 0f && card.gameObject.activeSelf)
        {
            card.gameObject.SetActive(false);
        }
        CardVisible = card.gameObject.activeSelf;

        PositionCard();

        // The arrow bobs over the pointed word.
        bool show = pointed != null && pointed.IsAlive && pointed.Label != null && pointed.Label.enabled
            && pointed.Label.gameObject.activeInHierarchy;
        pointer.gameObject.SetActive(show);
        if (show)
        {
            Vector2 at;
            RectTransform label = pointed.Label.rectTransform;
            Vector3 top = label.TransformPoint(new Vector3(label.rect.center.x, label.rect.yMax, 0f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, top, null, out at);
            pointer.anchoredPosition = at + Vector2.up * (40f + Mathf.Sin(Time.unscaledTime * 5f) * 7f);
        }
    }

    // Resolve the same position for drawing and word placement regardless of LateUpdate order.
    private void PositionCard()
    {
        if (cardPositionFrame == Time.frameCount) return;
        cardPositionFrame = Time.frameCount;

        float availableWidth = Mathf.Max(1f, root.rect.width - 48f);
        float scale = Mathf.Min(1f, availableWidth / card.rect.width);
        card.localScale = new Vector3(scale, scale, 1f);
        float halfWidth = card.rect.width * scale * 0.5f;
        float room = Mathf.Max(0f, root.rect.width * 0.5f - 24f - halfWidth);
        cardShift = Mathf.MoveTowards(cardShift, ShiftClearOfPowers(halfWidth), Time.unscaledDeltaTime * 1200f);
        cardShift = Mathf.Clamp(cardShift, 0f, room);

        float rise = 1f - Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.28f);
        rise = rise * rise * rise;
        float nudge = Mathf.Max(0f, 1f - (Time.unscaledTime - nudgeAt) / 0.35f);
        float x = Mathf.Clamp(cardShift + Mathf.Sin(nudge * 30f) * 12f * nudge, -room, room);
        card.anchoredPosition = new Vector2(x, CardBottom - 18f * rise);
    }

    // How far right the card must move so its left edge stays a little to the
    // right of the power list (bottom-left); never off the right edge.
    private float ShiftClearOfPowers(float halfWidth)
    {
        float powersRight = hud != null && !TouchControls.Active ? hud.PowersRightEdge() : float.NaN;
        if (float.IsNaN(powersRight))
        {
            return 0f;
        }
        float right = root.InverseTransformPoint(new Vector3(powersRight, 0f, 0f)).x - root.rect.center.x;
        return Mathf.Max(0f, right + 28f + halfWidth);
    }

    private void OnDestroy()
    {
        CardVisible = false;
        if (tint != null)
        {
            Destroy(tint.gameObject);
        }
        GuidedIntro.Abandon(); // the scene is going: nothing of the intro may stay behind
    }

    // ---- Layout ----

    private void Layout(string heading, string line, GuideStep[] howSteps, bool showContinue)
    {
        foreach (Transform child in steps)
        {
            Destroy(child.gameObject);
        }
        bool hasSteps = howSteps != null && howSteps.Length > 0;
        bool hasTitle = !string.IsNullOrEmpty(heading);
        title.text = hasTitle ? heading : "";
        title.gameObject.SetActive(hasTitle);
        body.text = line;
        body.fontSize = hasTitle || hasSteps ? 26f : 30f;
        continueChip.text = TouchControls.Active ? "TAP TO CONTINUE" : "ENTER  TO CONTINUE";
        continueChip.gameObject.SetActive(showContinue);
        steps.gameObject.SetActive(hasSteps);
        divider.gameObject.SetActive(hasSteps);

        // The text block: as wide as its longest line (up to a limit).
        float maxText = hasSteps ? TextMaxWidth : LineMaxWidth;
        Vector2 bodySize = body.GetPreferredValues(line, maxText, 1000f);
        float textWidth = Mathf.Min(maxText, bodySize.x + 2f);
        if (hasTitle)
        {
            textWidth = Mathf.Max(textWidth, title.GetPreferredValues(heading).x + 2f);
        }
        if (showContinue)
        {
            textWidth = Mathf.Max(textWidth, continueChip.GetPreferredValues(continueChip.text).x + 2f);
        }
        bodySize = body.GetPreferredValues(line, textWidth, 1000f);
        float titleHeight = hasTitle ? 38f : 0f;
        float continueHeight = showContinue ? 30f : 0f;
        float textHeight = titleHeight + bodySize.y + continueHeight;

        float stepsWidth = hasSteps ? howSteps.Length * StepWidth + (howSteps.Length - 1) * StepGap : 0f;
        float width = Pad * 2f + textWidth + (hasSteps ? Pad * 2f + stepsWidth : 0f);
        float height = Mathf.Max(textHeight, hasSteps ? StepsHeight : 0f) + Pad * 1.4f;
        card.sizeDelta = new Vector2(Mathf.Max(width, 300f), height);
        cardPositionFrame = -1;
        float left = (card.sizeDelta.x - width) * 0.5f + Pad; // a text-only card centres its text

        TextAlignmentOptions align = hasSteps ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        title.alignment = align;
        body.alignment = align;
        continueChip.alignment = align;

        float y = textHeight * 0.5f; // from the card's middle, going down
        if (hasTitle)
        {
            Place(title.rectTransform, left, y, textWidth, titleHeight);
            y -= titleHeight;
        }
        Place(body.rectTransform, left, y, textWidth, bodySize.y);
        y -= bodySize.y;
        if (showContinue)
        {
            Place(continueChip.rectTransform, left, y - 4f, textWidth, continueHeight);
        }

        if (hasSteps)
        {
            float dividerX = left + textWidth + Pad;
            divider.anchoredPosition = new Vector2(dividerX, 0f);
            divider.sizeDelta = new Vector2(2f, height - Pad);
            steps.anchoredPosition = new Vector2(dividerX + Pad, 0f);
            steps.sizeDelta = new Vector2(stepsWidth, StepsHeight);
            BuildSteps(howSteps);
        }

        if (!cardWanted || !card.gameObject.activeSelf)
        {
            shownAt = Time.unscaledTime;
            cardGroup.alpha = 0f;
        }
        cardWanted = true;
        card.gameObject.SetActive(true);
        CardVisible = true;
    }

    // A text at 'left' from the card's left edge, its top 'top' above the card's middle.
    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, top);
        rect.sizeDelta = new Vector2(width, height);
    }

    // The steps, side by side with a small arrow between them: [1] HOLD -> arrows AIM -> [1] LET GO.
    private void BuildSteps(GuideStep[] howSteps)
    {
        for (int i = 0; i < howSteps.Length; i++)
        {
            float x = StepWidth * 0.5f + i * (StepWidth + StepGap);
            RectTransform step = Rect("Step", steps, new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(StepWidth, StepsHeight));
            step.pivot = new Vector2(0.5f, 1f);
            if (howSteps[i].IsArrows)
            {
                ArrowKeys(step);
            }
            else
            {
                KeyCap(step, howSteps[i].KeyText);
            }
            TMP_Text label = Text("Label", step, howSteps[i].Label, 18f, FontStyles.Bold);
            label.color = Gold;
            label.characterSpacing = 3f;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            label.rectTransform.sizeDelta = new Vector2(StepWidth + StepGap, 24f);

            if (i < howSteps.Length - 1)
            {
                RectTransform between = Rect("Then", steps, new Vector2(0f, 1f), new Vector2(x + StepWidth * 0.5f + StepGap * 0.5f, -40f), new Vector2(30f, 40f));
                Arrow(between, Grey, 0.45f, 90f); // pointing right
            }
        }
    }

    // A key cap: a lighter edge, a dark face, the key ("1", "TAB") or a word ("TYPE").
    private static void KeyCap(RectTransform step, string key)
    {
        float width = key.Length <= 1 ? 68f : Mathf.Min(StepWidth, 30f + key.Length * 17f);
        RectTransform edge = Rect("Cap", step, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(width, 68f));
        edge.pivot = new Vector2(0.5f, 1f);
        Image edgeImage = edge.gameObject.AddComponent<Image>();
        edgeImage.color = CapEdge;
        edgeImage.raycastTarget = false;
        RectTransform face = Rect("Face", edge, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(width - 4f, 62f));
        Image faceImage = face.gameObject.AddComponent<Image>();
        faceImage.color = CapFace;
        faceImage.raycastTarget = false;
        TMP_Text text = Text("Key", face, key, key.Length <= 3 ? 34f : 20f, FontStyles.Bold);
        Fill(text.rectTransform);
    }

    // The four arrow keys in their usual T, each with an arrow drawn on it.
    private static void ArrowKeys(RectTransform step)
    {
        Vector2[] spots = { new Vector2(-36f, -56f), new Vector2(36f, -56f), new Vector2(0f, -20f), new Vector2(0f, -56f) };
        float[] turns = { -90f, 90f, 180f, 0f }; // the drawn arrow points down: turned for left, right, up
        for (int i = 0; i < 4; i++)
        {
            RectTransform edge = Rect("Arrow Key", step, new Vector2(0.5f, 1f), spots[i], new Vector2(32f, 32f));
            Image edgeImage = edge.gameObject.AddComponent<Image>();
            edgeImage.color = CapEdge;
            edgeImage.raycastTarget = false;
            RectTransform face = Rect("Face", edge, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(29f, 29f));
            Image faceImage = face.gameObject.AddComponent<Image>();
            faceImage.color = CapFace;
            faceImage.raycastTarget = false;
            RectTransform glyph = Rect("Arrow", face, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 60f));
            Arrow(glyph, Color.white, 0.36f, turns[i]);
        }
    }

    // A down-pointing arrow made of three bars, scaled and turned.
    private static void Arrow(RectTransform parent, Color color, float scale, float degrees)
    {
        parent.localScale = Vector3.one * scale;
        parent.localRotation = Quaternion.Euler(0f, 0f, degrees);
        Bar(parent, new Vector2(0f, 10f), new Vector2(6f, 33f), 0f, color);
        Bar(parent, new Vector2(-6f, -6f), new Vector2(6f, 21f), 45f, color);
        Bar(parent, new Vector2(6f, -6f), new Vector2(6f, 21f), -45f, color);
    }

    private static void Bar(RectTransform parent, Vector2 position, Vector2 size, float degrees, Color color)
    {
        RectTransform bar = Rect("Bar", parent, new Vector2(0.5f, 0.5f), position, size);
        bar.localRotation = Quaternion.Euler(0f, 0f, degrees);
        Image image = bar.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    // ---- Small UI helpers ----

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text Text(string name, Transform parent, string text, float size, FontStyles style)
    {
        RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, size * 1.4f));
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Fill(RectTransform rect)
    {
        Stretch(rect);
    }
}

// A click or tap on the card continues it (the card's own Image catches it).
public class CardClick : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        GuidedIntro.OnEnter();
    }
}
