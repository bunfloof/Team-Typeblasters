// SettingsPanel.cs
// ---------------------------------------------------------------------------
// The SETTINGS button (a gear, top-right, just under Wave / Score) and the
// panel it opens. In the level scenes the gear is the "Settings Button" prefab
// on the Start and pause panels (see SettingsButton, which calls Open); the
// Menu scene's gear and the panel itself are built here in code (AddButton,
// Build). The gear is drawn from plain rectangles (no image files).
//
//   SETTINGS
//   ADAPTIVE DIFFICULTY                  [ ON ]
//   what it does
//   Your speed: 42 WPM (measured)
//
//   SOUND EFFECTS VOLUME                 70%
//   [---------------- slider ----------------]
//                 [ DONE ]
//
// The switch is DynamicDifficulty.Enabled (saved, on by default). Changing it
// in the middle of a run (from the pause panel) is fine: the difficulty
// slides back to the level as designed over a few seconds, or starts adapting.
//
// Closing: DONE, Esc or Enter (TypingController asks
// HandleKeys first, so those keys do not also resume or start the game), and
// it closes by itself if the game state changes under it.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SettingsPanel : MonoBehaviour
{
    private static readonly Color GearColor = new Color(0.86f, 0.88f, 0.92f);
    private static readonly Color PlateColor = new Color(0.12f, 0.13f, 0.16f, 0.85f);
    private static readonly Color BoxColor = new Color(0.09f, 0.1f, 0.13f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.28f);
    private static readonly Color OnColor = new Color(0.35f, 0.8f, 0.45f);   // the same green as EASY
    private static readonly Color OffColor = new Color(0.32f, 0.33f, 0.37f);
    private const string Grey = "<color=#9AA0AA>";

    private static readonly Vector2 GearSize = new Vector2(76f, 76f);
    private static readonly Vector2 BoxSize = new Vector2(860f, 620f);

    // The panel open now, or null.
    private static SettingsPanel open;

    public static bool IsOpen
    {
        get { return open != null; }
    }

    private Image switchImage;
    private TMP_Text switchLabel;
    private TMP_Text statusText;
    private Slider volumeSlider;
    private TMP_Text volumeValueText;
    private GameState openedIn;

    // ---- The gear button ----

    // Adds the gear to 'parent' (a full-screen panel). offset: from the
    // parent's top-right corner to the gear's top-right corner.
    public static Button AddButton(RectTransform parent, Vector2 offset, UnityEngine.Events.UnityAction action = null, string label = "SETTINGS")
    {
        RectTransform rect = CreateRect("Settings Button", parent);
        Place(rect, Vector2.one, Vector2.one, offset, GearSize);
        Image plate = rect.gameObject.AddComponent<Image>();
        plate.color = PlateColor;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.55f, 0.85f, 1f); // tints the plate blue under the mouse
        colors.selectedColor = Color.white;
        button.colors = colors;
        NoNavigation(button);
        Sfx.HookButton(button);
        if (action == null) button.onClick.AddListener(() => Open(parent));
        else button.onClick.AddListener(action);

        // The gear: 4 bars crossing make the 8 teeth, two squares at 0 and
        // 45 degrees make the round body, two small dark ones the hole.
        for (int i = 0; i < 4; i++)
        {
            AddShape(rect, new Vector2(54f, 12f), i * 45f, GearColor);
        }
        AddShape(rect, new Vector2(38f, 38f), 0f, GearColor);
        AddShape(rect, new Vector2(38f, 38f), 45f, GearColor);
        AddShape(rect, new Vector2(15f, 15f), 0f, PlateColor);
        AddShape(rect, new Vector2(15f, 15f), 45f, PlateColor);

        TMP_Text caption = CreateText(rect, "Caption", label, 17f);
        Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(140f, 24f));
        caption.color = new Color(0.8f, 0.82f, 0.86f);
        return button;
    }

    // ---- The panel ----

    // Opens the panel over everything on the canvas that 'from' is on
    // (called by a gear: SettingsButton, or the one AddButton makes).
    public static void Open(Transform from)
    {
        Canvas canvas = from.GetComponentInParent<Canvas>();
        if (canvas == null || open != null)
        {
            return;
        }
        Transform root = canvas.rootCanvas.transform;
        SettingsPanel panel = root.GetComponentInChildren<SettingsPanel>(true);
        if (panel == null)
        {
            panel = Build(root);
        }
        panel.transform.SetAsLastSibling(); // drawn over every other panel
        panel.openedIn = GameManager.Instance != null ? GameManager.Instance.State : GameState.Start;
        panel.gameObject.SetActive(true);
        panel.Refresh();
        open = panel;
    }

    private void Close()
    {
        Sfx.SavePreferences();
        gameObject.SetActive(false);
        if (open == this)
        {
            open = null;
        }
    }

    // Called by TypingController before it reads Esc / Enter: true if the
    // panel was open and took them (closed by Esc or Enter).
    public static bool HandleKeys(bool escapePressed, bool enterPressed)
    {
        if (open == null)
        {
            return false;
        }
        if (escapePressed || enterPressed)
        {
            open.Close();
        }
        return true;
    }

    private void OnDisable()
    {
        if (open == this)
        {
            open = null;
        }
    }

    private void Update()
    {
        // The game moved on (a hidden browser tab, a level loading...): close.
        if (GameManager.Instance != null && GameManager.Instance.State != openedIn)
        {
            Close();
            return;
        }

#if ENABLE_INPUT_SYSTEM
        // In the menu scene there is no TypingController to pass the keys on.
        if (GameManager.Instance == null && Keyboard.current != null
            && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
        {
            Close();
            return;
        }
#endif
        Refresh();
    }

    private void Toggle()
    {
        DynamicDifficulty.Enabled = !DynamicDifficulty.Enabled;
        Refresh();
    }

    private void Refresh()
    {
        bool on = DynamicDifficulty.Enabled;
        switchImage.color = on ? OnColor : OffColor;
        switchLabel.text = on ? "ON" : "OFF";
        switchLabel.color = on ? Color.black : Color.white;

        float wpm = DynamicDifficulty.SessionWpm;
        string speed = wpm > 0f
            ? "Your measured speed: <b>" + Mathf.RoundToInt(wpm) + " WPM</b>"
            : "Your speed is measured in the first " + Mathf.RoundToInt(DynamicDifficulty.CalibrationSeconds) + " seconds of typing.";
        if (on && GameManager.Instance != null && GameManager.Instance.State == GameState.Paused)
        {
            speed += "   " + Grey + (DynamicDifficulty.Calibrated ? "(enemies tuned to it)" : "(still measuring)") + "</color>";
        }
        statusText.text = speed;

        float volume = Sfx.MasterVolume;
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(volume);
        }
        UpdateVolumeLabel(volume);
    }

    private void SetSfxVolume(float volume)
    {
        Sfx.SetMasterVolume(volume);
        UpdateVolumeLabel(volume);
    }

    private void UpdateVolumeLabel(float volume)
    {
        if (volumeValueText != null)
        {
            volumeValueText.text = Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f) + "%";
        }
    }

    private static SettingsPanel Build(Transform root)
    {
        // A dark full-screen backdrop: it catches the clicks meant for the buttons behind it.
        RectTransform backdrop = CreateRect("Settings Panel", root);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;
        Image dim = backdrop.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        SettingsPanel panel = backdrop.gameObject.AddComponent<SettingsPanel>();

        RectTransform box = CreateRect("Box", backdrop);
        Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, BoxSize);
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = BoxColor;

        TMP_Text title = CreateText(box, "Title", "SETTINGS", 46f);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(760f, 60f));
        title.fontStyle = FontStyles.Bold;

        TMP_Text name = CreateText(box, "Adaptive Label", "ADAPTIVE DIFFICULTY", 32f);
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -126f), new Vector2(560f, 60f));
        name.alignment = TextAlignmentOptions.MidlineLeft;
        name.fontStyle = FontStyles.Bold;

        Button toggle = CreateButton(box, "Adaptive Switch", "ON", panel.Toggle, out panel.switchLabel);
        RectTransform toggleRect = (RectTransform)toggle.transform;
        Place(toggleRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -124f), new Vector2(170f, 64f));
        panel.switchLabel.fontStyle = FontStyles.Bold;
        panel.switchImage = toggle.GetComponent<Image>();

        TMP_Text about = CreateText(box, "Adaptive Description",
            Grey + "Measures how fast you type during your first 10 seconds of typing, then keeps "
            + "adjusting how fast zombies walk, how often they come and how long their words are. "
            + "Slower typists get more time, faster typists a bigger fight. "
            + "Turn it off to play every level exactly as designed.</color>", 25f);
        Place(about.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(760f, 140f));
        about.alignment = TextAlignmentOptions.TopLeft;
        about.textWrappingMode = TextWrappingModes.Normal;

        panel.statusText = CreateText(box, "Adaptive Status", "", 25f);
        Place(panel.statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(760f, 36f));
        panel.statusText.alignment = TextAlignmentOptions.MidlineLeft;
        panel.statusText.color = new Color(0.35f, 0.95f, 0.85f);

        TMP_Text volumeTitle = CreateText(box, "SFX Volume Title", "SOUND EFFECTS VOLUME", 28f);
        Place(volumeTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(50f, -410f), new Vector2(560f, 42f));
        volumeTitle.alignment = TextAlignmentOptions.MidlineLeft;
        volumeTitle.fontStyle = FontStyles.Bold;

        panel.volumeValueText = CreateText(box, "SFX Volume Value", "70%", 27f);
        Place(panel.volumeValueText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-50f, -410f), new Vector2(120f, 42f));
        panel.volumeValueText.alignment = TextAlignmentOptions.MidlineRight;
        panel.volumeValueText.color = new Color(0.35f, 0.95f, 0.85f);

        panel.volumeSlider = CreateSlider(box, "SFX Volume Slider", panel.SetSfxVolume);
        Place((RectTransform)panel.volumeSlider.transform, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -465f), new Vector2(700f, 32f));
        panel.volumeSlider.SetValueWithoutNotify(Sfx.MasterVolume);
        panel.UpdateVolumeLabel(Sfx.MasterVolume);

        TMP_Text doneLabel;
        Button done = CreateButton(box, "Done Button", "DONE", panel.Close, out doneLabel);
        Place((RectTransform)done.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(260f, 70f));

        backdrop.gameObject.SetActive(false);
        return panel;
    }

    // ---- Small UI helpers (the same look as HUD's) ----

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = uiObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // A plain rectangle in the middle of parent, turned by degrees (part of the gear).
    private static void AddShape(RectTransform parent, Vector2 size, float degrees, Color color)
    {
        RectTransform rect = CreateRect("Gear Part", parent);
        Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false; // the plate behind takes the click
    }

    private static TMP_Text CreateText(Transform parent, string objectName, string text, float fontSize)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(Transform parent, string objectName, string text, UnityAction onClick, out TMP_Text label)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonColor;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Sfx.HookButton(button);
        button.onClick.AddListener(onClick);
        NoNavigation(button);
        label = CreateText(rect, "Text", text, 34f);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static Slider CreateSlider(Transform parent, string objectName, UnityAction<float> onValueChanged)
    {
        RectTransform root = CreateRect(objectName, parent);
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;

        RectTransform background = CreateRect("Track", root);
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = new Vector2(0f, 9f);
        background.offsetMax = new Vector2(0f, -9f);
        Image trackImage = background.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0.22f, 0.23f, 0.27f);

        RectTransform fillArea = CreateRect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.2f);
        fillArea.anchorMax = new Vector2(1f, 0.8f);
        fillArea.offsetMin = new Vector2(4f, 0f);
        fillArea.offsetMax = new Vector2(-4f, 0f);

        RectTransform fill = CreateRect("Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = OnColor;
        fillImage.raycastTarget = false;

        RectTransform handleArea = CreateRect("Handle Slide Area", root);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);

        RectTransform handle = CreateRect("Handle", handleArea);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(28f, 0f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = GearColor;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.onValueChanged.AddListener(onValueChanged);
        return slider;
    }

    // The keyboard is for typing only: arrow keys must not move a "selection".
    private static void NoNavigation(Button button)
    {
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }
}
