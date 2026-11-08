// EndlessWaveReward.cs
// ---------------------------------------------------------------------------
// The reward panel of ENDLESS mode. After every cleared wave the game freezes
// and this panel asks the player to CHOOSE ONE reward:
//   HEALTH       + 10% of full health (GameManager.EndlessRewardHealthFraction)
//   ENERGY       + GameManager.EndlessRewardEnergy skill energy
//   FREE WEAPON  GameManager decides which one before showing the card, 
//                and Show() prints its name on it
//                (waves 1-5: a FREE SKILL instead, a lure bomb or a freeze)
//   SKIP         + 100 score
// After a BOSS wave (every 10th) everything is bigger: +20% health, +12
// energy, BOTH weapons, +500 score (Show(..., afterBoss: true)).
// Endless mode has no other way to get health back.
//
// How it is used:
//   HUD:         creates this component in Awake and calls Initialize(canvas);
//                ShowEndlessWaveReward / HideEndlessWaveReward call Show / Hide.
//   GameManager: BeginEndlessWaveReward() sets the state to Reward and freezes
//                time, then Show() is called with a callback; the callback
//                (CompleteEndlessWaveReward) applies the choice and resumes play.
//
// The panel is built in code like the rest of the HUD (no prefab, no images):
// a dark full-screen backdrop, a box, three reward cards and a SKIP button.
// It re-lays itself out when the screen size changes: three cards side by side
// on a wide screen, stacked in a column on a narrow or portrait one.
//
// Controls: click a card, or use W A S D / the arrow keys to move the
// selection and Enter / Space to pick it. The yellow selection frame only
// appears after the first key press, so a mouse player never sees it.
// Update still runs while the game is frozen (it uses no game time).
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum EndlessWaveRewardChoice
{
    Health,
    Energy,
    FreeWeapon,
    Skip
}

[DisallowMultipleComponent]
public class EndlessWaveReward : MonoBehaviour
{
    private static readonly Color FrameColor = new Color(0.95f, 0.75f, 0.1f);

    private const float FrameThickness = 6f;
    private const float SelectedScale = 1.04f;

    private static readonly Color[] CardColors =
    {
        new Color(0.20f, 0.36f, 0.29f),
        new Color(0.12f, 0.32f, 0.39f),
        new Color(0.35f, 0.25f, 0.43f)
    };

    private Transform uiRoot;
    private GameObject panel;
    private RectTransform box;

    private TMP_Text title;
    private TMP_Text subtitle;

    private readonly List<Button> buttons = new List<Button>();
    private readonly List<RectTransform> buttonRects = new List<RectTransform>();
    private readonly List<TMP_Text> cardTitles = new List<TMP_Text>();
    private readonly List<TMP_Text> cardDetails = new List<TMP_Text>();

    private Button skipButton;

    private Action<EndlessWaveRewardChoice> onChoice;
    private int selected;
    private bool keyboardSelectionStarted;
    private Vector2 lastLayoutSize;

    public void Initialize(Transform root)
    {
        if (uiRoot != null)
            return;

        if (root == null)
        {
            Debug.LogError("EndlessWaveReward: UI root is missing.");
            return;
        }

        uiRoot = root;
        Build();
    }

    public void Show(Action<EndlessWaveRewardChoice> callback, string weaponName, bool afterBoss = false)
    {
        if (panel == null)
        {
            Debug.LogError("EndlessWaveRewardPanel: Initialize must be called first.");
            return;
        }

        onChoice = callback;
        selected = 0;
        keyboardSelectionStarted = false;

        // Display the exact weapon that this reward will grant.
        cardDetails[2].text = weaponName;
        cardTitles[2].text = weaponName == "LURE BOMB" || weaponName == "FREEZE" ? "FREE SKILL" : "FREE WEAPON"; // early waves give a skill

        // The amounts of this reward (bigger after a boss).
        title.text = afterBoss ? "BOSS DEFEATED!" : "WAVE CLEARED!";
        subtitle.text = afterBoss ? "BIG REWARD - CHOOSE ONE" : "CHOOSE ONE REWARD";
        cardDetails[0].text = "+" + Mathf.RoundToInt((afterBoss ? GameManager.EndlessBossRewardHealthFraction : GameManager.EndlessRewardHealthFraction) * 100f) + "% HEALTH";
        cardDetails[1].text = "+" + (afterBoss ? GameManager.EndlessBossRewardEnergy : GameManager.EndlessRewardEnergy) + " ENERGY";
        TMP_Text skipLabel = skipButton != null ? skipButton.GetComponentInChildren<TMP_Text>() : null;
        if (skipLabel != null)
        {
            skipLabel.text = "SKIP  +" + (afterBoss ? GameManager.EndlessBossRewardScore : GameManager.EndlessRewardScore) + " SCORE";
        }

        LayoutPanel();
        RefreshSelection();

        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
    }

    public void Hide()
    {
        onChoice = null;

        if (panel != null)
            panel.SetActive(false);

        keyboardSelectionStarted = false;
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf)
            return;

        RectTransform rootRect = uiRoot as RectTransform;
        if (rootRect != null && rootRect.rect.size != lastLayoutSize)
            LayoutPanel();

        HandleKeyboard();
    }

    // ---- Build the UI ----

    private void Build()
    {
        panel = new GameObject(
            "EndlessWaveReward",
            typeof(RectTransform),
            typeof(Image));

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(uiRoot, false);
        Stretch(panelRect);

        Image dim = panel.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        box = CreateRect("RewardBox", panelRect);
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = new Color(0.035f, 0.045f, 0.07f, 0.98f);
        boxImage.raycastTarget = true;

        title = CreateText(box, "Title", "WAVE CLEARED!", 52f);
        subtitle = CreateText(box, "Subtitle", "CHOOSE ONE REWARD", 28f);

        CreateRewardCard(
            "HealthCard",
            "HEALTH",
            "+" + Mathf.RoundToInt(GameManager.EndlessRewardHealthFraction * 100f) + "% HEALTH",
            CardColors[0],
            EndlessWaveRewardChoice.Health);

        CreateRewardCard(
            "EnergyCard",
            "ENERGY",
            "+" + GameManager.EndlessRewardEnergy + " ENERGY",
            CardColors[1],
            EndlessWaveRewardChoice.Energy);

        CreateRewardCard(
            "WeaponCard",
            "FREE WEAPON",
            "FRENZY",
            CardColors[2],
            EndlessWaveRewardChoice.FreeWeapon);

        skipButton = CreateButton(
            "SkipButton",
            "SKIP  +100 SCORE",
            new Color(0.22f, 0.24f, 0.29f),
            () => Choose(EndlessWaveRewardChoice.Skip));

        buttons.Add(skipButton);
        buttonRects.Add((RectTransform)skipButton.transform);

        panel.SetActive(false);
        LayoutPanel();
        RefreshSelection();
    }

    private void CreateRewardCard(
        string objectName,
        string heading,
        string amount,
        Color color,
        EndlessWaveRewardChoice choice)
    {
        Button button = CreateButton(
            objectName,
            "",
            color,
            () => Choose(choice));

        buttonRects.Add((RectTransform)button.transform);
        buttons.Add(button);

        TMP_Text headingText = CreateText(button.transform, "Heading", heading, 34f);
        TMP_Text amountText = CreateText(button.transform, "Amount", amount, 30f);

        headingText.fontStyle = FontStyles.Bold;
        amountText.fontStyle = FontStyles.Bold;

        cardTitles.Add(headingText);
        cardDetails.Add(amountText);
    }

    private Button CreateButton(
        string objectName,
        string label,
        Color normalColor,
        UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(box, false);

        Image image = go.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.13f);
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.2f);
        colors.selectedColor = normalColor;
        button.colors = colors;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        if (!string.IsNullOrEmpty(label))
        {
            TMP_Text text = CreateText(rect, "Text", label, 30f);
            Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
        }

        AddSelectionFrame(rect);
        return button;
    }

    // ---- Responsive layout ----

    private float GetCardWidth(bool stacked, float maxWidth)
    {
        float headingSize = stacked ? 25f : 30f;
        float amountSize = stacked ? 22f : 26f;

        float widestText = 0f;

        for (int i = 0; i < cardTitles.Count; i++)
        {
            cardTitles[i].fontSize = headingSize;
            cardDetails[i].fontSize = amountSize;

            widestText = Mathf.Max(
                widestText,
                cardTitles[i].GetPreferredValues().x,
                cardDetails[i].GetPreferredValues().x);
        }

        // Approximately one uppercase character of padding on each side.
        float characterWidth = cardTitles[2].GetPreferredValues("M").x;

        return Mathf.Min(widestText + characterWidth * 2f, maxWidth);
    }

    private void LayoutPanel()
    {
        RectTransform rootRect = uiRoot as RectTransform;
        if (rootRect == null)
            return;

        Vector2 screenSize = rootRect.rect.size;
        if (screenSize.x <= 0f || screenSize.y <= 0f)
            return;

        lastLayoutSize = screenSize;

        bool stacked = screenSize.x < screenSize.y || screenSize.x < 1200f;

        float boxWidth;
        float boxHeight;

        if (stacked)
        {
            boxWidth = Mathf.Min(920f, screenSize.x - 48f);
            boxHeight = Mathf.Min(920f, screenSize.y - 48f);
        }
        else
        {
            boxWidth = Mathf.Min(1240f, screenSize.x - 64f);
            boxHeight = Mathf.Min(620f, screenSize.y - 64f);
        }

        Place(box, Centre, Centre, Vector2.zero,
            new Vector2(boxWidth, boxHeight));

        Place(title.rectTransform, TopCentre, TopCentre,
            new Vector2(0f, -32f), new Vector2(boxWidth - 80f, 70f));

        Place(subtitle.rectTransform, TopCentre, TopCentre,
            new Vector2(0f, -100f), new Vector2(boxWidth - 80f, 45f));

        title.fontSize = stacked ? 42f : 52f;
        subtitle.fontSize = stacked ? 23f : 28f;

        if (stacked)
        {
            float baseCardHeight = Mathf.Clamp((boxHeight - 390f) / 3f, 105f, 155f);
            float cardGap = 28f;
            float originalStackHeight = baseCardHeight * 3f + 14f * 2f;
            float cardHeight = (originalStackHeight - cardGap * 2f) / 3f;
            float cardWidth = GetCardWidth(true, boxWidth - 80f);
            float top = boxHeight * 0.5f - 160f;

            for (int i = 0; i < 3; i++)
            {
                float y = top - cardHeight * 0.5f - i * (cardHeight + cardGap);

                Place(buttonRects[i], Centre, Centre,
                    new Vector2(0f, y),
                    new Vector2(cardWidth, cardHeight));

                LayoutCardText(i, cardWidth, cardHeight, true);
            }

            Place(buttonRects[3], Centre, Centre,
                new Vector2(0f, -boxHeight * 0.5f + 82f),
                new Vector2(Mathf.Min(420f, cardWidth), 62f));
        }
        else
        {
            float gap = 72f;
            float maxCardWidth = (boxWidth - 80f - gap * 2f) / 3f;
            float cardWidth = GetCardWidth(false, maxCardWidth);
            float totalWidth = cardWidth * 3f + gap * 2f;
            float cardHeight = Mathf.Min(290f, boxHeight * 0.47f);

            for (int i = 0; i < 3; i++)
            {
                float x = -totalWidth * 0.5f + cardWidth * 0.5f
                    + i * (cardWidth + gap);

                Place(buttonRects[i], Centre, Centre,
                    new Vector2(x, 0f),
                    new Vector2(cardWidth, cardHeight));

                LayoutCardText(i, cardWidth, cardHeight, false);
            }

            Place(buttonRects[3], Centre, Centre,
                new Vector2(0f, -boxHeight * 0.5f + 100f),
                new Vector2(340f, 64f));
        }
    }

    private void LayoutCardText(int index, float width, float height, bool stacked)
    {
        TMP_Text heading = cardTitles[index];
        TMP_Text amount = cardDetails[index];

        Place(heading.rectTransform, TopCentre, TopCentre,
            new Vector2(0f, -height * 0.15f),
            new Vector2(width - 28f, 42f));

        Place(amount.rectTransform, Centre, Centre,
            new Vector2(0f, -height * 0.20f),
            new Vector2(width - 32f, 44f));

        heading.fontSize = stacked ? 25f : 30f;
        amount.fontSize = stacked ? 22f : 26f;
    }

    // ---- Keyboard selection ----

    private void HandleKeyboard()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool previous =
            keyboard.upArrowKey.wasPressedThisFrame ||
            keyboard.leftArrowKey.wasPressedThisFrame ||
            keyboard.wKey.wasPressedThisFrame ||
            keyboard.aKey.wasPressedThisFrame;

        bool next =
            keyboard.downArrowKey.wasPressedThisFrame ||
            keyboard.rightArrowKey.wasPressedThisFrame ||
            keyboard.sKey.wasPressedThisFrame ||
            keyboard.dKey.wasPressedThisFrame;

        bool confirm =
            keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame ||
            keyboard.spaceKey.wasPressedThisFrame;

        int step = (next ? 1 : 0) - (previous ? 1 : 0);

        if (step != 0)
        {
            keyboardSelectionStarted = true;
            selected = (selected + step + buttons.Count) % buttons.Count;
            RefreshSelection();
        }

        if (confirm)
        {
            Button button = buttons[selected];

            if (button != null && button.isActiveAndEnabled && button.interactable)
                button.onClick.Invoke();
        }
#endif
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            SetHighlight(
                buttons[i],
                keyboardSelectionStarted && i == selected);
        }
    }

    private void Choose(EndlessWaveRewardChoice choice)
    {
        if (panel == null || !panel.activeSelf)
            return;

        Action<EndlessWaveRewardChoice> callback = onChoice;
        Hide();

        callback?.Invoke(choice);
    }

    // ---- UI helpers ----

    private static TMP_Text CreateText(
        Transform parent, string objectName, string value, float fontSize)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        return text;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Place(
        RectTransform rect,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
    private static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);

    // The same yellow frame used by MainMenu and the pause menu.
    private static void AddSelectionFrame(RectTransform button)
    {
        RectTransform frame = CreateRect("Selection Frame", button);
        Stretch(frame);

        AddFrameBar(frame,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, FrameThickness));

        AddFrameBar(frame,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, FrameThickness));

        AddFrameBar(frame,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), new Vector2(FrameThickness, 0f));

        AddFrameBar(frame,
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(1f, 0.5f), new Vector2(FrameThickness, 0f));

        frame.gameObject.SetActive(false);
    }

    private static void AddFrameBar(
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size)
    {
        GameObject go = new GameObject(
            "Bar",
            typeof(RectTransform),
            typeof(Image));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = FrameColor;
        image.raycastTarget = false;
    }

    private static void SetHighlight(Button button, bool on)
    {
        if (button == null)
            return;

        Transform frame = button.transform.Find("Selection Frame");
        if (frame != null)
            frame.gameObject.SetActive(on);

        button.transform.localScale = on
            ? new Vector3(SelectedScale, SelectedScale, 1f)
            : Vector3.one;
    }
}
