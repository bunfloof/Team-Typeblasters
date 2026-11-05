// GuidedIntroView.cs
// ---------------------------------------------------------------------------
// What the guided first wave (GuidedIntro) draws, built in code like the HUD:
//   - a MESSAGE CARD: a title, one short line, and for a power its steps as
//     key caps / the arrow keys (drawn shapes, no font symbols) with one word
//     under each; "ENTER to continue" on read-only cards (a tap works too).
//     It sits next to what it explains (CardPlace);
//   - a bobbing ARROW over a zombie's word (PointAt);
//   - a "skip intro" chip at the start (ENTER, or tap it);
//   - a light dark TINT over the 3D view while the world is held.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GuidedIntroView : MonoBehaviour
{
    private static readonly Color CardColor = new Color(0.04f, 0.06f, 0.09f, 0.94f);
    private static readonly Color Teal = new Color(0.25f, 0.86f, 0.8f);
    private static readonly Color Gold = new Color(1f, 0.78f, 0.35f);
    private static readonly Color CapColor = new Color(0.1f, 0.2f, 0.24f, 1f);
    private static readonly Color Grey = new Color(0.65f, 0.68f, 0.74f);

    private const float CardWidth = 640f;
    private const float HoldTint = 0.32f;

    private RectTransform root, card, steps, pointer, skipChip;
    private Image tint;
    private TMP_Text title, body, continueChip;
    private ITypingTarget pointed;
    private float nudgeAt = -100f;
    private Vector2 cardHome;

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

        view.Build();
        return view;
    }

    private void Build()
    {
        // The root itself takes no clicks; the card and the chip do.
        card = Rect("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, 200f));
        Image plate = card.gameObject.AddComponent<Image>();
        plate.color = CardColor;
        Image accent = Rect("Accent", card, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 200f)).gameObject.AddComponent<Image>();
        accent.color = Teal;
        accent.raycastTarget = false;
        accent.rectTransform.anchorMin = new Vector2(0f, 0f);
        accent.rectTransform.anchorMax = new Vector2(0f, 1f);
        accent.rectTransform.pivot = new Vector2(0f, 0.5f);
        accent.rectTransform.sizeDelta = new Vector2(8f, 0f);
        accent.rectTransform.anchoredPosition = Vector2.zero;
        card.gameObject.AddComponent<CardClick>();

        title = Text("Title", card, "", 36f, FontStyles.Bold);
        title.color = Teal;
        body = Text("Body", card, "", 28f, FontStyles.Normal);
        body.textWrappingMode = TextWrappingModes.Normal;
        steps = Rect("Steps", card, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(CardWidth - 40f, 120f));
        steps.pivot = new Vector2(0.5f, 1f);
        continueChip = Text("Continue", card, "", 20f, FontStyles.Normal);
        continueChip.color = Grey;
        card.gameObject.SetActive(false);

        pointer = Rect("Pointer", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 60f));
        Arrow(pointer, Gold, 1f, 0f);
        pointer.gameObject.SetActive(false);

        skipChip = Rect("Skip", root, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(360f, 56f));
        Image skipPlate = skipChip.gameObject.AddComponent<Image>();
        skipPlate.color = new Color(0.04f, 0.06f, 0.09f, 0.85f);
        Button skipButton = skipChip.gameObject.AddComponent<Button>();
        skipButton.targetGraphic = skipPlate;
        skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
        skipButton.onClick.AddListener(GuidedIntro.OnEnter);
        TMP_Text skipText = Text("Label", skipChip, TouchControls.Active ? "TAP to skip the intro" : "ENTER  skip the intro", 24f, FontStyles.Normal);
        skipText.color = Grey;
        Fill(skipText.rectTransform);
        skipChip.gameObject.SetActive(false);
    }

    // ---- What GuidedIntro calls ----

    public void ShowCard(CardPlace place, string heading, string line, GuideStep[] howSteps, bool showContinue)
    {
        title.text = heading;
        title.gameObject.SetActive(heading.Length > 0);
        body.text = line;
        foreach (Transform child in steps)
        {
            Destroy(child.gameObject);
        }
        bool hasSteps = howSteps != null && howSteps.Length > 0;
        steps.gameObject.SetActive(hasSteps);
        if (hasSteps)
        {
            BuildSteps(howSteps);
        }
        continueChip.text = TouchControls.Active ? "TAP to continue" : "ENTER to continue";
        continueChip.gameObject.SetActive(showContinue);

        // Stack the parts top to bottom and size the card around them.
        float y = -22f;
        if (heading.Length > 0)
        {
            Place(title.rectTransform, y, 44f);
            y -= 48f;
        }
        float bodyHeight = Mathf.Max(36f, body.GetPreferredValues(line, CardWidth - 64f, 400f).y);
        Place(body.rectTransform, y, bodyHeight);
        y -= bodyHeight + 10f;
        if (hasSteps)
        {
            steps.anchoredPosition = new Vector2(0f, y);
            y -= 128f;
        }
        if (showContinue)
        {
            Place(continueChip.rectTransform, y, 28f);
            y -= 32f;
        }
        card.sizeDelta = new Vector2(CardWidth, -y + 14f);

        PlaceCard(place);
        card.gameObject.SetActive(true);
    }

    public void HideCard()
    {
        card.gameObject.SetActive(false);
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
        if (tint != null)
        {
            Destroy(tint.gameObject);
        }
        Destroy(gameObject);
    }

    // ---- Every frame ----

    private void LateUpdate()
    {
        // The tint fades in while the world is held.
        float alpha = Mathf.MoveTowards(tint.color.a, GuidedIntro.Holding ? HoldTint : 0f, Time.unscaledDeltaTime * 1.5f);
        tint.color = new Color(0f, 0f, 0f, alpha);

        // Letters typed while a key is wanted: the card shakes.
        float nudge = Mathf.Max(0f, 1f - (Time.unscaledTime - nudgeAt) / 0.35f);
        card.anchoredPosition = cardHome + Vector2.right * Mathf.Sin(nudge * 30f) * 14f * nudge;

        // The arrow bobs over the pointed zombie's word.
        bool show = pointed != null && pointed.IsAlive && pointed.Label != null && pointed.Label.enabled;
        pointer.gameObject.SetActive(show);
        if (show)
        {
            Vector2 at;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointed.Label.rectTransform.position, null, out at);
            pointer.anchoredPosition = at + Vector2.up * (78f + Mathf.Sin(Time.unscaledTime * 4f) * 8f);
        }
    }

    private void OnDestroy()
    {
        if (tint != null)
        {
            Destroy(tint.gameObject);
        }
        GuidedIntro.Abandon(); // the scene is going: nothing of the intro may stay behind
    }

    // ---- Layout ----

    private void PlaceCard(CardPlace place)
    {
        // On a phone the powers list is replaced by buttons: such cards go in the middle.
        if (place == CardPlace.Powers && TouchControls.Active)
        {
            place = CardPlace.Center;
        }
        switch (place)
        {
            case CardPlace.TopLeft:
                SetAnchor(new Vector2(0f, 1f), new Vector2(40f, -110f));
                break;
            case CardPlace.TopRight:
                SetAnchor(new Vector2(1f, 1f), new Vector2(-40f, -150f));
                break;
            case CardPlace.TopCenter:
                SetAnchor(new Vector2(0.5f, 1f), new Vector2(0f, -215f));
                break;
            case CardPlace.Powers:
                SetAnchor(new Vector2(0f, 0f), new Vector2(520f, 300f)); // above the word box
                break;
            default:
                SetAnchor(new Vector2(0.5f, 0.5f), new Vector2(0f, 270f)); // above where the zombies' words are
                card.pivot = new Vector2(0.5f, 0.5f);
                break;
        }
    }

    private void SetAnchor(Vector2 anchor, Vector2 position)
    {
        card.anchorMin = anchor;
        card.anchorMax = anchor;
        card.pivot = anchor;
        card.anchoredPosition = position;
        cardHome = position;
    }

    private void Place(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(32f, 0f);
        rect.offsetMax = new Vector2(-32f, 0f);
        rect.anchoredPosition = new Vector2(0f, top);
        rect.sizeDelta = new Vector2(-64f, height);
    }

    // The steps, side by side with a small arrow between them: [1] HOLD -> arrows AIM -> [1] LET GO.
    private void BuildSteps(GuideStep[] howSteps)
    {
        const float StepWidth = 150f;
        const float Gap = 46f;
        float total = howSteps.Length * StepWidth + (howSteps.Length - 1) * Gap;
        for (int i = 0; i < howSteps.Length; i++)
        {
            float x = -total * 0.5f + StepWidth * 0.5f + i * (StepWidth + Gap);
            RectTransform step = Rect("Step", steps, new Vector2(0.5f, 1f), new Vector2(x, 0f), new Vector2(StepWidth, 120f));
            step.pivot = new Vector2(0.5f, 1f);
            if (howSteps[i].IsArrows)
            {
                ArrowKeys(step);
            }
            else
            {
                KeyCap(step, howSteps[i].KeyText);
            }
            TMP_Text label = Text("Label", step, howSteps[i].Label, 22f, FontStyles.Bold);
            label.color = Gold;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            label.rectTransform.sizeDelta = new Vector2(StepWidth + 30f, 28f);

            if (i < howSteps.Length - 1)
            {
                RectTransform between = Rect("Then", steps, new Vector2(0.5f, 1f), new Vector2(x + StepWidth * 0.5f + Gap * 0.5f, -44f), new Vector2(30f, 40f));
                Arrow(between, Grey, 0.6f, 90f); // pointing right
            }
        }
    }

    // A key cap with a key on it ("1", "TAB") or a word ("TYPE").
    private void KeyCap(RectTransform step, string key)
    {
        float width = key.Length <= 1 ? 76f : Mathf.Min(150f, 30f + key.Length * 22f);
        RectTransform cap = Rect("Cap", step, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(width, 76f));
        cap.pivot = new Vector2(0.5f, 1f);
        Image face = cap.gameObject.AddComponent<Image>();
        face.color = CapColor;
        face.raycastTarget = false;
        TMP_Text text = Text("Key", cap, key, key.Length <= 3 ? 40f : 26f, FontStyles.Bold);
        Fill(text.rectTransform);
    }

    // The four arrow keys, each with an arrow drawn on it.
    private void ArrowKeys(RectTransform step)
    {
        Vector2[] spots = { new Vector2(-40f, -62f), new Vector2(40f, -62f), new Vector2(0f, -22f), new Vector2(0f, -62f) };
        float[] turns = { -90f, 90f, 180f, 0f }; // the drawn arrow points down: turned for left, right, up
        for (int i = 0; i < 4; i++)
        {
            RectTransform cap = Rect("Arrow Key", step, new Vector2(0.5f, 1f), spots[i], new Vector2(36f, 36f));
            Image face = cap.gameObject.AddComponent<Image>();
            face.color = CapColor;
            face.raycastTarget = false;
            RectTransform glyph = Rect("Arrow", cap, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 60f));
            Arrow(glyph, Color.white, 0.42f, turns[i]);
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
        RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth - 64f, size * 1.4f));
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
