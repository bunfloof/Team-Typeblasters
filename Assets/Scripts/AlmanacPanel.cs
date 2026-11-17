// AlmanacPanel.cs
// ---------------------------------------------------------------------------
// The ALMANAC screen (opened from the main menu): the book of every enemy.
//
//   left:   a grid of cards, one per entry. A discovered card shows the
//           enemy's picture and name; a locked one a dark silhouette and "???".
//   right:  the selected entry in full: big picture, name, threat level, what
//           it does, how to kill it and where it is first met. A locked entry
//           only says where to find it.
//   top:    "ALMANAC" and how many entries are discovered, with a bar.
//
// Keys: W A S D / arrows move the selection, Esc / Backspace / Enter close.
// A card can also be clicked. Built in code (Ui), re-laid out when the window
// changes; on a tall (phone) screen the grid sits above the details.
// The data and the pictures come from Almanac.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class AlmanacPanel : MonoBehaviour
{
    private RectTransform root, header, grid, detail, footer;
    private TMP_Text countText, nameText, taglineText, threatLabel, bodyText, lockedText;
    private RectTransform progressFill;
    private Image portrait, portraitGlow, nameStripe;
    private readonly List<RectTransform> cards = new List<RectTransform>();
    private readonly List<RectTransform> cardFrames = new List<RectTransform>();
    private readonly List<Image> threatPips = new List<Image>();
    private int selected;
    private int columns = 5;
    private Vector2 lastSize;
    private float openedAt;
    private System.Action onClose;

    public bool IsOpen
    {
        get { return root != null && root.gameObject.activeSelf; }
    }

    public static AlmanacPanel Create(RectTransform canvas)
    {
        RectTransform rect = Ui.Rect(canvas, "Almanac");
        Ui.Stretch(rect);
        AlmanacPanel panel = rect.gameObject.AddComponent<AlmanacPanel>();
        panel.root = rect;
        panel.Build();
        rect.gameObject.SetActive(false);
        return panel;
    }

    public void Open(System.Action closed)
    {
        onClose = closed;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        openedAt = Time.unscaledTime;
        RefreshCards();
        // Start on the first discovered entry (or the first one).
        selected = 0;
        for (int i = 0; i < Almanac.Entries.Count; i++)
        {
            if (Almanac.IsDiscovered(Almanac.Entries[i]))
            {
                selected = i;
                break;
            }
        }
        lastSize = Vector2.zero;
        Layout();
        Select(selected);
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }
        root.gameObject.SetActive(false);
        System.Action closed = onClose;
        onClose = null;
        closed?.Invoke();
    }

    // ---- Build ----

    private void Build()
    {
        Image backdrop = Ui.Panel(root, "Backdrop", new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 1f), true);
        Ui.Stretch(backdrop.rectTransform);

        header = Ui.Rect(root, "Header");
        TMP_Text title = Ui.Caps(Ui.Label(header, "Title", "ALMANAC", 64f, FontStyles.Bold, TextAlignmentOptions.Left), 14f);
        Ui.Place(title.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(0f, 0f), new Vector2(700f, 76f));
        TMP_Text sub = Ui.Caps(Ui.Label(header, "Sub", "FIELD NOTES ON THE DEAD", 22f, FontStyles.Normal, TextAlignmentOptions.Left), 8f);
        sub.color = Ui.Teal;
        Ui.Place(sub.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(4f, -78f), new Vector2(700f, 30f));

        countText = Ui.Caps(Ui.Label(header, "Count", "", 26f, FontStyles.Bold, TextAlignmentOptions.Right), 4f);
        Ui.Place(countText.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(0f, -20f), new Vector2(520f, 36f));
        Image bar = Ui.Panel(header, "Progress", new Color(1f, 1f, 1f, 0.08f));
        Ui.Place(bar.rectTransform, Ui.TopRight, Ui.TopRight, new Vector2(0f, -66f), new Vector2(360f, 8f));
        Image fill = Ui.Panel(bar.transform, "Fill", Ui.Teal);
        progressFill = fill.rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
        Image rule = Ui.Panel(header, "Rule", Ui.Line);
        rule.rectTransform.anchorMin = new Vector2(0f, 0f);
        rule.rectTransform.anchorMax = new Vector2(1f, 0f);
        rule.rectTransform.sizeDelta = new Vector2(0f, 2f);
        rule.rectTransform.anchoredPosition = Vector2.zero;

        grid = Ui.Rect(root, "Grid");
        for (int i = 0; i < Almanac.Entries.Count; i++)
        {
            BuildCard(i);
        }

        BuildDetail();

        footer = Ui.Rect(root, "Footer");
        BuildFooter();
    }

    private void BuildCard(int index)
    {
        Image card = Ui.Panel(grid, "Card " + Almanac.Entries[index].Id, Ui.PanelColor, true);
        Button button = card.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        int captured = index;
        button.onClick.AddListener(() => Select(captured));

        Image picture = Ui.Panel(card.transform, "Picture", Color.white);
        picture.preserveAspect = true;
        TMP_Text label = Ui.Caps(Ui.Label(card.transform, "Name", "", 20f, FontStyles.Bold), 3f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 20f;
        Image stripe = Ui.Panel(card.transform, "Stripe", Color.white);
        stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
        stripe.rectTransform.anchorMax = new Vector2(1f, 0f);
        stripe.rectTransform.pivot = new Vector2(0.5f, 0f);
        stripe.rectTransform.sizeDelta = new Vector2(0f, 4f);
        stripe.rectTransform.anchoredPosition = Vector2.zero;

        cards.Add(card.rectTransform);
        cardFrames.Add(Ui.Frame(card.transform, Ui.Line));
    }

    private void BuildDetail()
    {
        Image panel = Ui.Panel(root, "Detail", Ui.PanelColor);
        detail = panel.rectTransform;
        Ui.Frame(detail, Ui.Line);

        portraitGlow = Ui.Panel(detail, "Glow", Color.white);
        portraitGlow.sprite = Ui.Glow;
        portrait = Ui.Panel(detail, "Portrait", Color.white);
        portrait.preserveAspect = true;

        nameStripe = Ui.Panel(detail, "Name Stripe", Ui.Teal);
        nameText = Ui.Caps(Ui.Label(detail, "Name", "", 52f, FontStyles.Bold, TextAlignmentOptions.Left), 6f);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 28f;
        nameText.fontSizeMax = 52f;
        taglineText = Ui.Label(detail, "Tagline", "", 26f, FontStyles.Italic, TextAlignmentOptions.Left);
        taglineText.color = Ui.SubText;
        taglineText.textWrappingMode = TextWrappingModes.Normal;

        threatLabel = Ui.Caps(Ui.Label(detail, "Threat", "THREAT", 20f, FontStyles.Bold, TextAlignmentOptions.Left), 6f);
        threatLabel.color = Ui.SubText;
        for (int i = 0; i < 5; i++)
        {
            threatPips.Add(Ui.Panel(detail, "Pip " + i, Ui.Faint));
        }

        bodyText = Ui.Label(detail, "Body", "", 26f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.lineSpacing = 6f;
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 17f;
        bodyText.fontSizeMax = 26f;

        lockedText = Ui.Label(detail, "Locked", "", 28f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        lockedText.textWrappingMode = TextWrappingModes.Normal;
        lockedText.color = Ui.SubText;
        lockedText.lineSpacing = 8f;
    }

    private void BuildFooter()
    {
        bool touch = TouchControls.Active;
        float x = 0f;
        if (!touch)
        {
            x = Hint(x, "W A S D", "BROWSE");
            x = Hint(x, "ESC", "BACK");
        }
        // A BACK button for the mouse and for phones.
        Image back = Ui.Panel(footer, "Back", Ui.PanelLight, true);
        Ui.Place(back.rectTransform, Ui.RightMiddle, Ui.RightMiddle, Vector2.zero, new Vector2(220f, 56f));
        Ui.Frame(back.transform, Ui.Line);
        TMP_Text backLabel = Ui.Caps(Ui.Label(back.transform, "Text", "BACK", 24f, FontStyles.Bold), 8f);
        Ui.Stretch(backLabel.rectTransform);
        Button button = back.gameObject.AddComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(Close);
    }

    private float Hint(float x, string key, string what)
    {
        RectTransform cap = Ui.KeyCap(footer, key, 38f);
        Ui.Place(cap, Ui.LeftMiddle, Ui.LeftMiddle, new Vector2(x, 0f), cap.sizeDelta);
        x += cap.sizeDelta.x + 12f;
        TMP_Text label = Ui.Caps(Ui.Label(footer, "Hint " + what, what, 20f, FontStyles.Normal, TextAlignmentOptions.Left), 5f);
        label.color = Ui.SubText;
        float width = label.GetPreferredValues(what).x + 60f;
        Ui.Place(label.rectTransform, Ui.LeftMiddle, Ui.LeftMiddle, new Vector2(x, 0f), new Vector2(width, 38f));
        return x + width;
    }

    // ---- Content ----

    private void RefreshCards()
    {
        List<AlmanacEntry> entries = Almanac.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            bool found = Almanac.IsDiscovered(entries[i]);
            Transform card = cards[i];
            card.Find("Picture").GetComponent<Image>().sprite = Almanac.Portrait(entries[i], !found);
            TMP_Text label = card.Find("Name").GetComponent<TMP_Text>();
            label.text = found ? entries[i].Name : "? ? ?";
            label.color = found ? Ui.Text : Ui.Faint;
            card.Find("Stripe").GetComponent<Image>().color = found ? entries[i].Color : new Color(1f, 1f, 1f, 0.06f);
        }
        int count = Almanac.DiscoveredCount;
        countText.text = "<color=#40DBCC>" + count + "</color> / " + entries.Count + "  DISCOVERED";
        progressFill.anchorMax = new Vector2(entries.Count > 0 ? (float)count / entries.Count : 0f, 1f);
    }

    private void Select(int index)
    {
        List<AlmanacEntry> entries = Almanac.Entries;
        selected = Mathf.Clamp(index, 0, entries.Count - 1);
        AlmanacEntry entry = entries[selected];
        bool found = Almanac.IsDiscovered(entry);

        for (int i = 0; i < cards.Count; i++)
        {
            bool on = i == selected;
            Ui.SetFrameColor(cardFrames[i], on ? Ui.Gold : Ui.Line);
            cards[i].GetComponent<Image>().color = on ? Ui.PanelLight : Ui.PanelColor;
            cards[i].localScale = on ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
        }
        cards[selected].SetAsLastSibling(); // the grown card is drawn over its neighbours

        portrait.sprite = Almanac.Portrait(entry, !found);
        Color glow = found ? entry.Color : Ui.Faint;
        portraitGlow.color = new Color(glow.r, glow.g, glow.b, found ? 0.28f : 0.1f);
        nameStripe.color = found ? entry.Color : Ui.Faint;
        nameText.text = found ? entry.Name : "? ? ?";
        nameText.color = found ? Ui.Text : Ui.Faint;
        taglineText.text = found ? entry.Tagline : "Not discovered yet.";

        threatLabel.gameObject.SetActive(found);
        for (int i = 0; i < threatPips.Count; i++)
        {
            threatPips[i].gameObject.SetActive(found);
            threatPips[i].color = i < entry.Threat ? Color.Lerp(Ui.Gold, Ui.Danger, entry.Threat / 5f) : new Color(1f, 1f, 1f, 0.1f);
        }

        bodyText.gameObject.SetActive(found);
        lockedText.gameObject.SetActive(!found);
        if (found)
        {
            string word = "#" + ColorUtility.ToHtmlStringRGB(entry.WordColor);
            bodyText.text =
                Heading("WHAT IT DOES") + entry.Power + "\n\n" +
                Heading("HOW TO KILL IT") + entry.HowToKill + "\n\n" +
                Heading("ITS WORD") + "<color=" + word + "><b>looks like this</b></color>\n\n" +
                Heading("FIRST MET") + entry.Where;
        }
        else
        {
            lockedText.text = Heading("LOCKED") + "Meet this enemy once and its page fills in.\n\n" + Heading("WHERE TO LOOK") + entry.Where;
        }
    }

    private static string Heading(string text)
    {
        return "<size=70%><color=#40DBCC><b><cspace=0.25em>" + text + "</cspace></b></color></size>\n";
    }

    // ---- Layout ----

    private void Layout()
    {
        Vector2 size = root.rect.size;
        if (size == lastSize || size.x <= 0f)
        {
            return;
        }
        lastSize = size;
        lastBob = 0f; // everything is placed afresh below
        bool tall = size.x < size.y;
        float margin = tall ? 40f : 90f;

        Ui.Place(header, Ui.TopLeft, Ui.TopLeft, new Vector2(margin, -(tall ? 60f : 50f)), new Vector2(size.x - margin * 2f, 120f));
        Ui.Place(footer, Ui.BottomLeft, Ui.BottomLeft, new Vector2(margin, 34f), new Vector2(size.x - margin * 2f, 60f));

        float top = (tall ? 60f : 50f) + 120f + 28f;
        float bottom = 34f + 60f + 24f;
        float areaHeight = size.y - top - bottom;
        float areaWidth = size.x - margin * 2f;

        float gridWidth, gridHeight, detailWidth, detailHeight;
        Vector2 gridAt, detailAt;
        if (tall)
        {
            columns = 4;
            gridWidth = areaWidth;
            gridHeight = areaHeight * 0.5f;
            detailWidth = areaWidth;
            detailHeight = areaHeight - gridHeight - 24f;
            gridAt = new Vector2(margin, -top);
            detailAt = new Vector2(margin, -top - gridHeight - 24f);
        }
        else
        {
            columns = 5;
            detailWidth = Mathf.Clamp(areaWidth * 0.36f, 440f, 640f);
            gridWidth = areaWidth - detailWidth - 36f;
            gridHeight = detailHeight = areaHeight;
            gridAt = new Vector2(margin, -top);
            detailAt = new Vector2(margin + gridWidth + 36f, -top);
        }
        Ui.Place(grid, Ui.TopLeft, Ui.TopLeft, gridAt, new Vector2(gridWidth, gridHeight));
        Ui.Place(detail, Ui.TopLeft, Ui.TopLeft, detailAt, new Vector2(detailWidth, detailHeight));

        // The cards.
        int rows = Mathf.CeilToInt(cards.Count / (float)columns);
        const float Gap = 16f;
        float cardWidth = (gridWidth - Gap * (columns - 1)) / columns;
        float cardHeight = Mathf.Min((gridHeight - Gap * (rows - 1)) / rows, cardWidth * 1.25f);
        for (int i = 0; i < cards.Count; i++)
        {
            int col = i % columns, row = i / columns;
            RectTransform card = cards[i];
            Ui.Place(card, Ui.TopLeft, Ui.Centre,
                new Vector2(col * (cardWidth + Gap) + cardWidth * 0.5f, -(row * (cardHeight + Gap) + cardHeight * 0.5f)),
                new Vector2(cardWidth, cardHeight));
            float labelHeight = Mathf.Clamp(cardHeight * 0.2f, 24f, 40f);
            RectTransform picture = (RectTransform)card.Find("Picture");
            picture.anchorMin = Vector2.zero;
            picture.anchorMax = Vector2.one;
            picture.offsetMin = new Vector2(10f, labelHeight + 8f);
            picture.offsetMax = new Vector2(-10f, -8f);
            RectTransform label = (RectTransform)card.Find("Name");
            label.anchorMin = new Vector2(0f, 0f);
            label.anchorMax = new Vector2(1f, 0f);
            label.pivot = new Vector2(0.5f, 0f);
            label.anchoredPosition = new Vector2(0f, 8f);
            label.sizeDelta = new Vector2(-12f, labelHeight);
        }

        // The detail panel.
        const float Pad = 34f;
        float inner = detailWidth - Pad * 2f;
        float pictureSize = tall ? Mathf.Min(detailHeight - Pad * 2f, inner * 0.36f) : Mathf.Min(inner, detailHeight * 0.36f);
        if (tall)
        {
            // Picture on the left, text on the right.
            Ui.Place(portrait.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(Pad, -Pad), new Vector2(pictureSize, pictureSize));
            Ui.Place(portraitGlow.rectTransform, Ui.TopLeft, Ui.Centre, new Vector2(Pad + pictureSize * 0.5f, -Pad - pictureSize * 0.5f), Vector2.one * pictureSize * 1.3f);
            float x = Pad + pictureSize + 28f;
            float w = detailWidth - x - Pad;
            PlaceText(x, w, Pad, detailHeight);
        }
        else
        {
            Ui.Place(portrait.rectTransform, Ui.TopCentre, Ui.TopCentre, new Vector2(0f, -Pad * 0.5f), new Vector2(pictureSize, pictureSize));
            Ui.Place(portraitGlow.rectTransform, Ui.TopCentre, Ui.Centre, new Vector2(0f, -Pad * 0.5f - pictureSize * 0.5f), Vector2.one * pictureSize * 1.35f);
            PlaceText(Pad, inner, Pad * 0.5f + pictureSize + 14f, detailHeight);
        }
    }

    // Name, tagline, threat and the body text, stacked from 'top' down.
    private void PlaceText(float x, float width, float top, float panelHeight)
    {
        Ui.Place(nameStripe.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -top - 6f), new Vector2(6f, 48f));
        Ui.Place(nameText.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x + 20f, -top), new Vector2(width - 20f, 60f));
        Ui.Place(taglineText.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -top - 66f), new Vector2(width, 36f));
        float y = top + 66f + 44f;
        Ui.Place(threatLabel.rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x, -y), new Vector2(120f, 26f));
        for (int i = 0; i < threatPips.Count; i++)
        {
            Ui.Place(threatPips[i].rectTransform, Ui.TopLeft, Ui.TopLeft, new Vector2(x + 126f + i * 34f, -y - 7f), new Vector2(26f, 12f));
        }
        y += 44f;
        Vector2 at = new Vector2(x, -y);
        Vector2 box = new Vector2(width, Mathf.Max(80f, panelHeight - y - 24f));
        Ui.Place(bodyText.rectTransform, Ui.TopLeft, Ui.TopLeft, at, box);
        Ui.Place(lockedText.rectTransform, Ui.TopLeft, Ui.TopLeft, at, box);
    }

    // ---- Every frame ----

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }
        Layout();

        // The picture floats a little; the screen fades in.
        float bob = Mathf.Sin(Time.unscaledTime * 1.6f) * 6f;
        portrait.rectTransform.localPosition += Vector3.up * (bob - lastBob);
        lastBob = bob;
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }
        group.alpha = Ui.EaseOut((Time.unscaledTime - openedAt) / 0.2f);

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || Time.unscaledTime - openedAt < 0.15f)
        {
            return;
        }
        int dx = (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame ? 1 : 0)
            - (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame ? 1 : 0);
        int dy = (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame ? 1 : 0)
            - (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame ? 1 : 0);
        if (dx != 0 || dy != 0)
        {
            int next = selected + dx + dy * columns;
            if (next >= 0 && next < cards.Count)
            {
                Select(next);
            }
        }
        if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame
            || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            Close();
        }
#endif
    }

    private float lastBob;
}
