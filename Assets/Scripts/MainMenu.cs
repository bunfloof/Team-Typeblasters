// MainMenu.cs
// ---------------------------------------------------------------------------
// The menu scene ("Menu.unity"). Builds its whole UI in code (like HUD does), so
// the scene only needs: a camera, an EventSystem and ONE object with this script.
//
//   MAIN PAGE:   title, a CHARACTER slot (placeholder, not implemented yet),
//                a START button at the bottom that opens the level page and
//                the SETTINGS gear top-right (see SettingsPanel).
//   LEVEL PAGE:  one button per level (the list comes from GameManager.LevelName,
//                so there is a single list of levels) and a BACK button.
//
// Clicking a level loads that scene by name. The scene must be in Build Settings.
// ---------------------------------------------------------------------------
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameTitle = "The Typeblaster of the Dead";
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.14f, 0.24f);
    [SerializeField] private Color dimColor = new Color(0.04f, 0.06f, 0.12f, 0.72f);

    private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.28f);
    private static readonly Color HoverColor = new Color(0.12f, 0.4f, 0.5f);   // the same blue HUD uses
    private static readonly Color PressedColor = new Color(0.08f, 0.28f, 0.35f);

    private GameObject mainPage;
    private GameObject levelPage;
    private TMP_Text levelMessage;   // shows an error if a level scene is missing from Build Settings
    private bool loading;
    private RectTransform canvasRect;
    private CanvasScaler canvasScaler;
    private TMP_Text mainTitle, levelTitle;
    private RectTransform startButton, backButton;
    private RectTransform endlessButton;
    private RectTransform fullscreenButton;
    private TMP_Text fullscreenHint, fullscreenLabel;
    private readonly List<RectTransform> levelButtons = new List<RectTransform>();
    private Vector2 lastLayoutSize;
    private bool portraitLayout;
    private readonly List<RectTransform> menuItems = new List<RectTransform>(); // buttons the keys can select on the current page
    private int selected;           // index in menuItems
    private bool keyboardSelectionStarted;
    private bool settingsWasOpen;   // SettingsPanel was open last frame (it closes itself on Esc / Enter)
    private static bool openLevelSelectionOnStart;

    private const string EndlessScene = "Endless";

    private const float SelectedScale = 1.05f;
    private const float FrameThickness = 6f;
    private static readonly Color FrameColor = new Color(0.95f, 0.75f, 0.1f);

    private void Start()
    {
        Time.timeScale = 1f; // in case we arrive here from a paused game

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
        }

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            Debug.LogError("MainMenu: the scene has no EventSystem, so buttons cannot be clicked. Add one with GameObject > UI > Event System.");
        }

        Transform canvas = BuildCanvas();
        canvasRect = (RectTransform)canvas;
        canvasScaler = canvas.GetComponent<CanvasScaler>();
        GameObject dim = CreatePage("Dim", canvas);
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = dimColor;
        dimImage.raycastTarget = false;
        MenuBackground.Create((RectTransform)canvas);
        mainPage = BuildMainPage(canvas);
        levelPage = BuildLevelPage(canvas);
        Canvas.ForceUpdateCanvases();
        UpdateResponsiveLayout();
        if (openLevelSelectionOnStart)
        {
            openLevelSelectionOnStart = false;
            ShowLevels();
        }
        else
        {
            ShowMain();
        }
    }

    private void Update()
    {
        UpdateResponsiveLayout();
        UpdateFullscreenLabel();
        HandleKeys();
    }

    // The menu has no HUD, so TouchControls cannot adapt its layout for it.
    // Use the canvas aspect (also works in Device Simulator), not a platform flag.
    private void UpdateResponsiveLayout()
    {
        if (canvasRect == null || mainTitle == null) return;
        Vector2 size = canvasRect.rect.size;
        bool portrait = size.x < size.y;
        if (size == lastLayoutSize && portrait == portraitLayout) return;
        portraitLayout = portrait;
        canvasScaler.referenceResolution = portrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = portrait ? 0f : 1f;
        Canvas.ForceUpdateCanvases();
        size = canvasRect.rect.size;
        lastLayoutSize = size;
        float width = Mathf.Max(240f, Mathf.Min(portrait ? 900f : 1700f, size.x - 120f));
        LayoutTitle(mainTitle, width, portrait ? 210f : 120f, portrait ? 330f : 300f, 84f, portrait);
        LayoutTitle(levelTitle, width, 110f, portrait ? 450f : 300f, 72f, false);
        LayoutButton(startButton, new Vector2(0f, portrait ? -40f : 0f), portrait ? new Vector2(760f, 140f) : new Vector2(360f, 100f), portrait);
        if (fullscreenButton != null)
        {
            LayoutButton(fullscreenButton, new Vector2(0f, portrait ? -315f : -200f),
                portrait ? new Vector2(620f, 100f) : new Vector2(360f, 76f), portrait);
            fullscreenLabel.fontSize = portrait ? 42f : 32f;
            LayoutTitle(fullscreenHint, width, 64f, portrait ? -205f : -117f, portrait ? 34f : 28f, true);
            fullscreenHint.fontSizeMin = 18f;
        }
        int modeButtonCount = levelButtons.Count + 1;
        float rowStep = Mathf.Min(400f, (size.x - 120f) / Mathf.Max(1, modeButtonCount));
        Vector2 landscapeModeSize = new Vector2(Mathf.Min(360f, rowStep - 24f), 180f);

        for (int i = 0; i < levelButtons.Count; i++)
        {
            Vector2 position = portrait ? new Vector2(0f, 260f - i * 180f)
                : new Vector2((i - (modeButtonCount - 1) * .5f) * rowStep, 60f);
            LayoutButton(levelButtons[i], position, portrait ? new Vector2(760f, 140f) : landscapeModeSize, portrait);
        }

        // Keep the portrait layout stacked, but place ENDLESS in the same landscape row.
        float endlessY = portrait ? 260f - levelButtons.Count * 180f : 60f;
        float endlessX = portrait ? 0f : (levelButtons.Count - (modeButtonCount - 1) * .5f) * rowStep;
        LayoutButton(endlessButton, new Vector2(endlessX, endlessY),
            portrait ? new Vector2(760f, 140f) : landscapeModeSize, portrait);

        LayoutButton(backButton, new Vector2(0f, portrait ? endlessY - 250f : -300f), portrait ? new Vector2(500f, 110f) : new Vector2(360f, 100f), portrait);
        levelMessage.rectTransform.anchoredPosition = new Vector2(0f, portrait ? endlessY - 130f : -225f);
        levelMessage.rectTransform.sizeDelta = new Vector2(width, portrait ? 80f : 50f);
        levelMessage.textWrappingMode = TextWrappingModes.Normal;
    }

    private static void LayoutTitle(TMP_Text title, float width, float height, float y, float maxSize, bool wrap)
    {
        title.rectTransform.anchoredPosition = new Vector2(0f, y);
        title.rectTransform.sizeDelta = new Vector2(width, height);
        title.enableAutoSizing = true;
        title.fontSizeMin = 32f;
        title.fontSizeMax = maxSize;
        title.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
    }

    private static void LayoutButton(RectTransform button, Vector2 position, Vector2 size, bool portrait)
    {
        button.anchoredPosition = position;
        button.sizeDelta = size;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        label.rectTransform.sizeDelta = size;
        label.fontSize = portrait ? 56f : 44f;
    }

    // ---- Pages ----

    private void ShowMain()
    {
        mainPage.SetActive(true);
        levelPage.SetActive(false);
        selected = 0;
        keyboardSelectionStarted = false;
        RefreshHighlight();
    }

    private void ShowLevels()
    {
        levelMessage.text = "";
        mainPage.SetActive(false);
        levelPage.SetActive(true);
        selected = 0;
        keyboardSelectionStarted = false;
        RefreshHighlight();
    }

    private GameObject BuildMainPage(Transform canvas)
    {
        GameObject page = CreatePage("Main Page", canvas);
        mainTitle = CreateText(page.transform, "Title", gameTitle, 84f, new Vector2(0f, 300f), new Vector2(1700f, 120f));
        startButton = (RectTransform)CreateButton(page.transform, "Start Button", "START", new Vector2(0f, 0f), new Vector2(360f, 100f), ShowLevels).transform;
        BuildFullscreenControl(page.transform);
        SettingsPanel.AddButton((RectTransform)page.transform, new Vector2(-64f, -48f));
        return page;
    }

    private void BuildFullscreenControl(Transform page)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!TouchFullscreenButton.IsSupported()) return;
#endif
        fullscreenHint = CreateText(page, "Fullscreen Hint", "", 28f, Vector2.zero, new Vector2(640f, 64f));
        fullscreenHint.color = new Color(.8f, .85f, .93f);
        Button button = CreateButton(page, "Fullscreen Button", "FULL SCREEN", Vector2.zero, new Vector2(360f, 76f), null);
        fullscreenButton = (RectTransform)button.transform;
        fullscreenLabel = button.GetComponentInChildren<TMP_Text>();
#if UNITY_WEBGL && !UNITY_EDITOR
        // Pointer-down preserves the browser gesture through the existing WebGL bridge.
        button.gameObject.AddComponent<TouchFullscreenButton>();
#else
        button.onClick.AddListener(TouchFullscreenButton.ToggleFullscreen);
#endif
        UpdateFullscreenLabel();
    }

    private void UpdateFullscreenLabel()
    {
        if (fullscreenLabel == null) return;
        bool fullscreen = TouchFullscreenButton.IsFullscreen;
        fullscreenLabel.text = fullscreen ? "EXIT FULL SCREEN" : "FULL SCREEN";
        fullscreenHint.text = fullscreen ? "Full screen is on" : "For the best view, play in full screen.";
    }

    private GameObject BuildLevelPage(Transform canvas)
    {
        GameObject page = CreatePage("Level Page", canvas);
        levelTitle = CreateText(page.transform, "Title", "SELECT LEVEL", 72f, new Vector2(0f, 300f), new Vector2(1700f, 110f));

        int count = GameManager.LevelCount;
        for (int i = 0; i < count; i++)
        {
            int levelIndex = i; // copy for the lambda
            string label = (i + 1) + ". " + GameManager.LevelName(i);
            string[] maps = { "CAMPUS", "HOLLOW FOREST" };
            label += "\n<size=55%>" + (i < maps.Length ? maps[i] : "") + "</size>";
            float x = (i - (count - 1) * 0.5f) * 400f;
            levelButtons.Add((RectTransform)CreateButton(page.transform, "Level " + GameManager.LevelName(i), label,
                new Vector2(x, 20f), new Vector2(360f, 180f), () => LoadLevel(levelIndex)).transform);
        }

        endlessButton = (RectTransform)CreateButton(page.transform, "Level Endless", "3. ENDLESS\n<size=55%>SURVIVAL</size>",
            new Vector2(0f, -110f), new Vector2(360f, 120f), () => LoadScene(EndlessScene)).transform;

        levelMessage = CreateText(page.transform, "Message", "", 32f, new Vector2(0f, -225f), new Vector2(1700f, 50f));
        levelMessage.color = new Color(1f, 0.4f, 0.3f);

        backButton = (RectTransform)CreateButton(page.transform, "Back Button", "BACK", new Vector2(0f, -300f), new Vector2(360f, 100f), ShowMain).transform;
        return page;
    }

    private void LoadLevel(int index)
    {
        LoadScene(GameManager.LevelName(index));
    }

    // Any scene by name (a level, or the Endless scene).
    private void LoadScene(string sceneName)
    {
        if (loading)
        {
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("MainMenu: scene \"" + sceneName + "\" is missing from Build Settings.");
            levelMessage.text = sceneName + " is not available yet.";
            return;
        }
        loading = true;
        SceneManager.LoadScene(sceneName);
    }

    // The buttons the keyboard can select on the page that is showing, in order.
    private List<RectTransform> CurrentItems()
    {
        menuItems.Clear();
        if (mainPage.activeSelf)
        {
            menuItems.Add(startButton);
            if (fullscreenButton != null) menuItems.Add(fullscreenButton);
        }
        else
        {
            menuItems.AddRange(levelButtons);
            menuItems.Add(endlessButton);
            menuItems.Add(backButton);
        }
        return menuItems;
    }

    // W A S D / arrow keys move the selection, Enter / Space press it, Esc / Backspace go back.
    private void HandleKeys()
    {
    #if ENABLE_INPUT_SYSTEM
        // SettingsPanel closes itself on Esc / Enter: that key must not also act here.
        bool settingsBusy = SettingsPanel.IsOpen || settingsWasOpen;
        settingsWasOpen = SettingsPanel.IsOpen;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || loading || settingsBusy)
        {
            return;
        }

        bool previous = keyboard.upArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame
            || keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame;
        bool next = keyboard.downArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
            || keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame;
        bool confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
            || keyboard.spaceKey.wasPressedThisFrame;
        bool back = keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame;

        List<RectTransform> items = CurrentItems();
        int step = (next ? 1 : 0) - (previous ? 1 : 0);
        if (step != 0)
        {
            keyboardSelectionStarted = true;
            selected = (selected + step + items.Count) % items.Count;
            RefreshHighlight();
        }

        if (confirm)
        {
            // The same click the mouse does (START -> ShowLevels, a level -> LoadLevel, BACK -> ShowMain).
            RectTransform item = items[Mathf.Clamp(selected, 0, items.Count - 1)];
            if (item == fullscreenButton)
                TouchFullscreenButton.ToggleFullscreen();
            else
                item.GetComponent<Button>().onClick.Invoke();
        }
        else if (back && levelPage.activeSelf)
        {
            ShowMain();
        }
    #endif
    }

    private void RefreshHighlight()
    {
        List<RectTransform> items = CurrentItems();
        selected = Mathf.Clamp(selected, 0, items.Count - 1);

        SetHighlight(startButton, false);
        if (fullscreenButton != null) SetHighlight(fullscreenButton, false);
        foreach (RectTransform level in levelButtons)
        {
            SetHighlight(level, false);
        }
        SetHighlight(endlessButton, false);
        SetHighlight(backButton, false);
        if (keyboardSelectionStarted)
        {
            SetHighlight(items[selected], true);
        }
    }

    private static void SetHighlight(RectTransform button, bool on)
    {
        Transform frame = button.Find("Selection Frame");
        if (frame != null)
        {
            frame.gameObject.SetActive(on);
        }
        button.localScale = on ? new Vector3(SelectedScale, SelectedScale, 1f) : Vector3.one;
    }

    // ---- Small UI helpers (the same look as HUD's buttons) ----

    private static Transform BuildCanvas()
    {
        GameObject canvasObject = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); // same as the game's HUD canvas
        scaler.matchWidthOrHeight = 1f;
        return canvasObject.transform;
    }

    private static GameObject CreatePage(string pageName, Transform parent)
    {
        GameObject page = new GameObject(pageName, typeof(RectTransform));
        RectTransform rect = page.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return page;
    }

    private static TMP_Text CreateText(Transform parent, string objectName, string text, float fontSize, Vector2 position, Vector2 size)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(Transform parent, string objectName, string text, Vector2 position, Vector2 size, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = HoverColor;
        colors.pressedColor = PressedColor;
        colors.selectedColor = ButtonColor;   // stops a clicked button from staying tinted
        button.colors = colors;

        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        // Same as the game's buttons: arrow keys never move a "selection".
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        CreateText(rect, "Text", text, 44f, Vector2.zero, size);
        AddSelectionFrame(rect);
        return button;
    }

    // A yellow frame made of four thin bars INSIDE the button, hidden until the keyboard selects it.
    // (Child images are not tinted by the Button's colour block, unlike an Outline component.)
    private static void AddSelectionFrame(RectTransform button)
    {
        RectTransform frame = new GameObject("Selection Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(button, false);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = Vector2.zero;
        frame.offsetMax = Vector2.zero;

        AddFrameBar(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FrameThickness)); // top
        AddFrameBar(frame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FrameThickness)); // bottom
        AddFrameBar(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(FrameThickness, 0f)); // left
        AddFrameBar(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(FrameThickness, 0f)); // right
        frame.gameObject.SetActive(false);
    }

    private static void AddFrameBar(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = bar.GetComponent<Image>();
        image.color = FrameColor;
        image.raycastTarget = false; // clicks still reach the button
    }

    public static void RequestLevelSelection()
    {
        openLevelSelectionOnStart = true;
    }
}
