// TouchControls.cs
// ---------------------------------------------------------------------------
// Makes the game playable on phones and tablets (the Web build in a mobile
// browser), held upright or sideways. On a touch device it adds, while the
// game runs, WITHOUT changing any scene:
//   - typing with the PHONE'S OWN KEYBOARD: while playing, a tap on the game
//     opens it (Plugins/WebGL/TouchControlsWeb.jslib) and what is typed goes
//     to the game as if typed on a computer keyboard (capitals, symbols, the
//     delete key drops the word, the space bar raises the shield; the number
//     keys 1-4 do nothing: the powers have their own buttons)
//   - a row of buttons just above the keyboard: pause, the four powers
//     (TouchPowerButton) and TAP TO TYPE / HIDE KEYBOARD (TouchTypeButton)
//   - the 3D view and the HUD fit into the part of the screen the keyboard
//     leaves free, so it never hides a zombie or its word; when that part is
//     short (sideways) the HUD is drawn smaller instead of squeezed; the view
//     keeps a sensible width (FitFieldOfView); held upright the HUD is laid
//     out for a narrow screen, with the words, hints and menus drawn bigger
//   - a FULL SCREEN button (bottom-left) on the Start, pause and end screens,
//     where the browser allows it (Android, iPad; not iPhone)
//   - bigger touch areas for every menu button (without changing their look)
//   - the texts that talk about keys ("press Enter", "Esc: pause") say what
//     to tap instead
// Every touch control feeds the keyboard code (TouchInput), so the game itself
// works exactly the same as with a keyboard.
//
// UseOwnKeyboard = true switches back to the game's own on-screen keyboard
// (TouchKeyboard / TouchKey) in a bar at the bottom, held sideways.
//
// WHEN: in the Web build, if the browser says this is a touch device (a phone,
// a tablet, an iPad) - see Plugins/WebGL/TouchControlsWeb.jslib. Add ?touch=1
// to the page address to try it on a computer (?touch=0 turns it off).
// In the Editor: Window > General > Device Simulator, with a phone selected.
// On a computer nothing changes.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Runs after the game's own scripts each frame, so a text they set this frame
// (a tip that just opened) is already changed before it is drawn.
[DefaultExecutionOrder(1000)]
public class TouchControls : MonoBehaviour
{
    // True on a touch device (the touch controls are on).
    public static bool Active { get; private set; }

    // Which keyboard: false = the phone's own keyboard; true = the game's own
    // on-screen keyboard (TouchKeyboard), kept in case we go back to it.
    private static readonly bool UseOwnKeyboard = false;

    // True while the phone's own keyboard is up.
    public static bool KeyboardOpen { get; private set; }

    // Layout in canvas units (the HUD canvas is 1080 units tall when sideways).
    private const float MaxTouchPadding = 24f;     // menu buttons react this far outside their edges
    private const float CornerButtonWidth = 120f;  // the FULL SCREEN button (and the own keyboard's pause button)

    // The button row above the phone's keyboard.
    private const float FingerSize = 50f;          // CSS pixels: buttons at least this tall
    private const float RowGap = 8f;
    private const float PauseWeight = 0.75f;       // widths: the pause button is narrower...
    private const float TypeWeight = 1.35f;        // ...and TAP TO TYPE wider than a power button

    // The game's own keyboard bar (UseOwnKeyboard).
    private const float BarHeight = 390f;          // a bit over 1/3 of the screen
    private const float BarPadding = 10f;
    private const float MaxSideWidth = 420f;       // a column of power buttons...
    private const float SideWidthFraction = 0.2f;  // ...at most this much of the screen width
    private const float PowerButtonGap = 10f;

    // Held upright the HUD is laid out for a screen this many units wide (the
    // HUD was made for wide screens; at this width nothing overlaps). That
    // makes everything smaller than when held sideways, so what the player
    // reads and taps (the words, floating texts, hints, menus) is drawn this
    // many times bigger again. (The tip box, 1100 units wide, still fits; a
    // menu text that would not fit is made narrower, see FitMenuTexts.)
    private const float UprightWidth = 1700f;
    private const float UprightUiScale = 1.45f;
    private const float MenuTextMargin = 40f;

    // The play area (the HUD while playing) is never less than this many units
    // tall: the HUD keeps the enemy words this far from the top (more in a boss
    // fight) and puts its banners and quiz box there. A shorter free part of
    // the screen draws the whole play area smaller instead (down to
    // MinPlayScale). ComboBand: the combo plate at the top of the play area.
    private const float MinPlayHeight = 700f;
    private const float MinPlayScale = 0.5f;
    private const float ComboBand = 140f;
    private const float BossBand = 230f;           // ...and the boss's health bar below it, in a boss fight

    // The enemy words are never drawn smaller than readable: the smallest
    // word (WaveSpawner's minWordFontSize, 34 units) at least this many CSS
    // pixels tall. In a short play area their layer also reaches up to
    // WordTopReach units above it, so WaveSpawner's top margin (150 units
    // below its top) does not push every word to the middle.
    private const float SmallestWordUnits = 34f;
    private const float MinWordCssPixels = 9f;
    private const float WordTopReach = 130f;

    // Sideways with the keyboard up, a view shorter than this (CSS pixels) is
    // too short to play well: a hint says so every ShortHintEvery seconds.
    private const float ShortStripCss = 120f;
    private const float ShortHintEvery = 30f;

    // WaveSpawner keeps the words this far below the top of their layer
    // (labelTopMargin, and bossLabelTopMargin in a boss fight).
    private const float WordTopMargin = 150f;
    private const float BossWordTopMargin = 235f;

    // The hint box sits this far up its layer (HUD's HintBottom plus the box):
    // the hint layer is never drawn so big that it does not fit.
    private const float HintRoom = 250f;

    // The field of view keeps the left-to-right view sensible: on a view wider
    // than WideAspect (width / height) it stops getting wider, on one narrower
    // than NarrowAspect (held upright) it stops getting narrower, up to
    // MaxVerticalFieldOfView degrees from top to bottom.
    private const float WideAspect = 2f;
    private const float NarrowAspect = 1.6f;
    private const float MaxVerticalFieldOfView = 100f;

    private static readonly Color BarColor = new Color(0.05f, 0.06f, 0.08f, 1f);
    private static readonly Color RowColor = new Color(0.05f, 0.06f, 0.08f, 0.85f);
    private static readonly Color CornerButtonColor = new Color(0f, 0f, 0f, 0.55f);

    // What the key that drops a word is called on screen.
    private static readonly string DeleteKey = UseOwnKeyboard ? "DEL" : "Delete";

    // Texts that talk about keys, and what they say on a touch screen instead.
    // Pairs: { text to find, text to put instead } (upper / lower case ignored).
    // A replacement inside a hint or a tip is not longer than what it replaces:
    // HUD sizes those boxes for the text before it is replaced.
    private static readonly string[] KeyTexts =
    {
        "Press Esc to resume", "Tap RESUME to continue",
        "(or press Enter)", "",
        "Enter: replay this level", "",
        "Press ENTER to continue", "Tap OK to continue",
        "Click the game window, then press Start", "Tap START to play",
        "Backspace: change target", DeleteKey + ": change target",
        "(or Backspace)", "(or " + DeleteKey + ")",
        "Press SPACE", "Tap SPACE",
        "Esc: pause", "II: pause",
        "(Esc = cancel)", "(" + DeleteKey + " = cancel)",
        "Use the ARROW KEYS to aim", "DRAG your finger to aim",
        "Release 1 to throw", "Let go to throw",
        "Press 2 to freeze", "Tap 2 to freeze",
        "ARROW KEYS steer it", "DRAG to steer it",
        " (or click its slot)", "",
        " (or click it)", "",
        "kept bottom-left", "kept on its button",
        "(a bar fills under its slots)", "(its button fills up)",
    };

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int TouchControlsWeb_IsTouchDevice();

    [DllImport("__Internal")]
    private static extern void TouchControlsWeb_SetUpNativeKeyboard(string objectName);

    [DllImport("__Internal")]
    private static extern void TouchControlsWeb_SetKeyboardWanted(int wanted);

    [DllImport("__Internal")]
    private static extern int TouchControlsWeb_IsKeyboardOpen();

    [DllImport("__Internal")]
    private static extern float TouchControlsWeb_CoveredBottom();

    [DllImport("__Internal")]
    private static extern float TouchControlsWeb_CoveredTop();

    [DllImport("__Internal")]
    private static extern float TouchControlsWeb_CanvasCssHeight();

    [DllImport("__Internal")]
    private static extern void TouchControlsWeb_SetTypeButtonRect(float left, float bottom, float right, float top);
#endif

    // ---- One scene's pieces (built again after every scene load) ----
    private HUD hud;
    private RectTransform canvasRect;
    private CanvasScaler scaler;
    private Vector2 sidewaysReference;   // the HUD's Canvas Scaler settings as made (sideways)
    private float sidewaysMatch;
    private RectTransform playArea;      // the HUD's gameplay pieces: shrinks to the free part of the screen while playing
    private RectTransform buttonRow;     // above the phone's keyboard: pause, powers, TAP TO TYPE
    private RectTransform keyboardBackdrop; // black, behind the phone's keyboard
    private RectTransform typeButton;    // TAP TO TYPE / HIDE KEYBOARD (the .jslib handles its taps)
#if UNITY_WEBGL && !UNITY_EDITOR
    private Rect sentTypeButton;         // where the .jslib was last told it is
    private readonly Vector3[] corners = new Vector3[4];
#endif
    // HUD layers drawn bigger when held upright (enemy words, floating texts,
    // hints), and the menu panels (Start, end screens, pause, the tip box).
    private readonly List<RectTransform> uprightLayers = new List<RectTransform>();
    private readonly List<RectTransform> menuPanels = new List<RectTransform>();
    private bool tipPanelFound;
    private RectTransform wordLayer;     // the enemy words' layer (one of uprightLayers)
    private RectTransform hintLayer;     // the hints' layer (one of uprightLayers)
    private GameState lastState;         // (a new state or new panel: look at the texts at once)
    private int lastPanelCount;
    private Transform bossBar;           // the boss's health bar, once HUD has made it
    // The menu panels' fixed-size texts, with the width and font size they
    // were made with (held upright, the ones too wide for the screen are made
    // narrower, see FitMenuTexts).
    private readonly List<TMP_Text> menuTexts = new List<TMP_Text>();
    private readonly List<float> menuTextWidths = new List<float>();
    private readonly List<float> menuTextSizes = new List<float>();
    private readonly List<Vector4> menuTextMargins = new List<Vector4>();
    private float menuTextsFitTo = -1f;  // the width they were last fitted to (-1: not yet)
    private float nextShortHint;
    private RectTransform bar;           // UseOwnKeyboard: the keyboard bar
    private RectTransform keysArea;
    private RectTransform leftColumn;
    private RectTransform rightColumn;
    private GameObject pauseButton;      // UseOwnKeyboard: the pause button (top-left)
    private GameObject pausePanel;       // the HUD's pause panel (null if the scene names it differently)
    private GameObject fullscreenButton; // null where the browser has no full screen
    private Camera gameCamera;
    private Transform hudPowersPanel;    // the HUD's own powers list (bottom-left), hidden: the buttons show it
    private bool playLayout;             // playing: keyboard and buttons, the view above them
    private bool layoutKnown;
    // Play-area pieces placed from the middle of the screen (the wave banner,
    // the big popup, the countdown) and how far above the middle they were placed.
    private readonly Dictionary<RectTransform, float> centredPieces = new Dictionary<RectTransform, float>();

    private float nextHudSearch;
    private TMP_Text[] texts = new TMP_Text[0]; // every text in the scene, hidden ones too (refreshed often)
    private readonly Dictionary<TMP_Text, string> checkedTexts = new Dictionary<TMP_Text, string>(); // the text each one had when last checked
    private float nextTextScan;
    private float nextButtonScan;
    private int lastScene = -1;

    // Runs once when the game starts, before the first scene loads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateOnTouchDevices()
    {
        Active = IsTouchDevice();
        if (!Active)
        {
            return;
        }
        GameObject controls = new GameObject("Touch Controls");
        DontDestroyOnLoad(controls); // one for the whole game; it sets up each scene itself
        controls.AddComponent<TouchControls>();
        Debug.Log("TouchControls: touch device, touch controls on.");
    }

    private static bool IsTouchDevice()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return TouchControlsWeb_IsTouchDevice() == 1;
#else
        // Editor and other builds: phones and tablets (the Device Simulator counts as one).
        return UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld;
#endif
    }

    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!UseOwnKeyboard)
        {
            // The invisible text field that brings up the phone's keyboard; it
            // sends what is typed to OnNativeText / OnNativeKey on this object.
            TouchControlsWeb_SetUpNativeKeyboard(gameObject.name);
        }
#endif
    }

    // Called by CameraDirector for the field of view (vertical, in degrees):
    // on a very wide view (above the keyboard, held sideways) it is narrowed,
    // on a narrow one (held upright) widened, so the left-to-right view stays
    // sensible. Unchanged on a computer.
    public static float FitFieldOfView(float verticalFieldOfView, float aspect)
    {
        if (!Active || (aspect <= WideAspect && aspect >= NarrowAspect))
        {
            return verticalFieldOfView;
        }
        float halfTan = Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
        if (aspect > WideAspect)
        {
            halfTan = halfTan * WideAspect / aspect;
        }
        else
        {
            float maxHalfTan = Mathf.Tan(MaxVerticalFieldOfView * 0.5f * Mathf.Deg2Rad);
            halfTan = Mathf.Min(halfTan * NarrowAspect / Mathf.Max(0.1f, aspect), maxHalfTan);
        }
        return 2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg;
    }

    // ---- What the phone's keyboard typed (called by the .jslib) ----

    // Typed characters. A space is the Space key (it raises the shield, see
    // EasyShield); everything else is typed as it is, except 1-4: on a
    // computer those keys are the powers, but a phone keyboard types them
    // when a letter is held a little long (q w e r), and the powers have
    // their own buttons. (No word has those digits.)
    public void OnNativeText(string text)
    {
        foreach (char character in text)
        {
            if (character == ' ')
            {
                TapKey(Key.Space);
            }
            else if (!char.IsControl(character) && (character < '1' || character > '4'))
            {
                TouchInput.Type(character);
            }
        }
    }

    private static void TapKey(Key key)
    {
        TouchInput.Press(key);
        TouchInput.Release(key); // (TouchInput lets go one frame later, so the game sees the press)
    }

    // A key: "tap:Backspace" (down and up), "down:LeftArrow", "up:LeftArrow"...
    // The name after the colon is a Key from the Input System.
    public void OnNativeKey(string message)
    {
        int colon = message.IndexOf(':');
        if (colon < 0)
        {
            return;
        }
        string action = message.Substring(0, colon);
        Key key;
        if (!System.Enum.TryParse(message.Substring(colon + 1), out key))
        {
            return;
        }
        if (action == "tap" || action == "down")
        {
            TouchInput.Press(key);
        }
        if (action == "tap" || action == "up")
        {
            TouchInput.Release(key);
        }
    }

    private void Update()
    {
        TouchInput.Update();

        // A new scene (the game, or a Restart): set it up once.
        if (hud == null)
        {
            if (Time.unscaledTime < nextHudSearch)
            {
                return;
            }
            nextHudSearch = Time.unscaledTime + 0.25f;
            hud = FindFirstObjectByType<HUD>();
            if (hud == null)
            {
                return; // a scene without the game (a menu): only the menu buttons get bigger touch areas
            }
            SetUpScene();
        }
        UpdateLayout();
    }

    private void LateUpdate()
    {
        // A new scene: forget the old scene's texts (they are gone).
        int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if (scene != lastScene)
        {
            lastScene = scene;
            checkedTexts.Clear();
            nextTextScan = 0f;
            nextButtonScan = 0f;
        }

        // After every script has set its texts this frame, so a text that talks
        // about keys is not drawn. Hidden panels are included, so their texts
        // are already changed when they open; a panel made just now (the first
        // tip box) is looked for at once.
        GameState state = GameManager.Instance != null ? GameManager.Instance.State : GameState.Start;
        int panelCount = canvasRect != null ? canvasRect.childCount : 0;
        if (state != lastState || panelCount != lastPanelCount)
        {
            lastState = state;
            lastPanelCount = panelCount;
            nextTextScan = 0f;
        }
        if (Time.unscaledTime >= nextTextScan)
        {
            nextTextScan = Time.unscaledTime + 0.1f;
            texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
        PatchKeyTexts();

        if (Time.unscaledTime >= nextButtonScan)
        {
            nextButtonScan = Time.unscaledTime + 1f;
            EnlargeButtonTouchAreas();
        }
    }

    // ---- Setting up a scene ----

    private void SetUpScene()
    {
        Canvas canvas = hud.GetComponentInParent<Canvas>();
        canvasRect = (RectTransform)canvas.rootCanvas.transform;
        scaler = canvasRect.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            sidewaysReference = scaler.referenceResolution;
            sidewaysMatch = scaler.matchWidthOrHeight;
        }

        CameraDirector director = FindFirstObjectByType<CameraDirector>();
        gameCamera = director != null ? director.GetComponent<Camera>() : Camera.main;

        Transform pause = canvasRect.Find("Pause Panel");
        pausePanel = pause != null ? pause.gameObject : null;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (!UseOwnKeyboard)
        {
            // Unity must not take every key press of the page (StartupSettings
            // turns this on): the phone's keyboard types into the text field,
            // which hands the keys over (OnNativeText / OnNativeKey).
            WebGLInput.captureAllKeyboardInput = false;
        }
#endif

        BuildPlayArea();
        FindMenuPanels();
        if (UseOwnKeyboard)
        {
            BuildKeyboardBar();
            BuildPauseButton();
        }
        else
        {
            BuildButtonRow();
        }
        BuildFullscreenButton();

        hudPowersPanel = null;
        layoutKnown = false;
        TouchInput.ReleaseAll();
    }

    // Every HUD piece in front of the first menu panel is a gameplay piece:
    // health, score, the typed word, the enemies' words... They move into one
    // rectangle, the play area, that shrinks to the free part of the screen
    // while playing. The menu panels stay full screen (and the keyboard is
    // closed while they show).
    private void BuildPlayArea()
    {
        List<Transform> pieces = new List<Transform>();
        foreach (Transform child in canvasRect)
        {
            if (IsMenuPanel(child))
            {
                break;
            }
            pieces.Add(child);
        }

        playArea = TouchUI.CreateRect("Touch Play Area", canvasRect);
        TouchUI.Stretch(playArea);
        playArea.SetSiblingIndex(pieces.Count > 0 ? pieces[0].GetSiblingIndex() : 0);
        foreach (Transform piece in pieces)
        {
            piece.SetParent(playArea, false); // same place: the play area fills the screen for now
        }

        // The 3-2-1 countdown (after the menu panels) belongs in the middle of
        // the view too. It only shows while every panel is closed.
        Transform countdown = canvasRect.Find("Countdown");
        if (countdown != null)
        {
            countdown.SetParent(playArea, false);
            pieces.Add(countdown);
        }

        if (UseOwnKeyboard)
        {
            // Room for the pause button in the top-left corner.
            MoveRight(playArea.Find("Health Label"), CornerButtonWidth + 16f);
            MoveRight(playArea.Find("Health Bar"), CornerButtonWidth + 16f);
        }

        // The layers drawn bigger when held upright (see UpdateLayout).
        uprightLayers.Clear();
        AddUprightLayer(playArea.Find("Enemy Words"));
        AddUprightLayer(playArea.Find("Markers"));
        AddUprightLayer(playArea.Find("Hint"));
        wordLayer = null;
        hintLayer = null;
        foreach (RectTransform layer in uprightLayers)
        {
            if (layer.name == "Enemy Words")
            {
                wordLayer = layer;
            }
            else if (layer.name == "Hint")
            {
                hintLayer = layer;
            }
        }
        bossBar = null;

        centredPieces.Clear();
        foreach (Transform piece in pieces)
        {
            RectTransform rect = piece as RectTransform;
            if (rect != null && rect.anchorMin == new Vector2(0.5f, 0.5f) && rect.anchorMax == new Vector2(0.5f, 0.5f))
            {
                centredPieces[rect] = rect.anchoredPosition.y;
            }
        }
    }

    // Only a layer that fills its parent can be scaled with ScaleInPlace.
    private void AddUprightLayer(Transform layer)
    {
        RectTransform rect = layer as RectTransform;
        if (rect != null && FillsParent(rect))
        {
            rect.pivot = new Vector2(0.5f, 0.5f); // grows from the middle
            uprightLayers.Add(rect);
        }
    }

    // The menu panels (they fill the screen, after the play area). The tip box
    // panel is added when HUD makes it (FindTipPanel).
    private void FindMenuPanels()
    {
        menuPanels.Clear();
        menuTexts.Clear();
        menuTextWidths.Clear();
        menuTextSizes.Clear();
        menuTextMargins.Clear();
        menuTextsFitTo = -1f;
        tipPanelFound = false;
        foreach (Transform child in canvasRect)
        {
            RectTransform rect = child as RectTransform;
            if (rect != null && IsMenuPanel(child) && FillsParent(rect))
            {
                rect.pivot = new Vector2(0.5f, 0.5f);
                menuPanels.Add(rect);

                // Its texts with a fixed size (not stretched over the panel),
                // hidden ones too, that do not size themselves already.
                TMP_Text[] panelTexts = child.GetComponentsInChildren<TMP_Text>(true);
                foreach (TMP_Text text in panelTexts)
                {
                    RectTransform textRect = text.rectTransform;
                    if (textRect.anchorMin == textRect.anchorMax && !text.enableAutoSizing)
                    {
                        menuTexts.Add(text);
                        menuTextWidths.Add(textRect.sizeDelta.x);
                        menuTextSizes.Add(text.fontSize);
                        menuTextMargins.Add(text.margin);
                    }
                }
            }
        }
    }

    private void FindTipPanel()
    {
        if (tipPanelFound)
        {
            return;
        }
        RectTransform tipPanel = canvasRect.Find("TipPanel") as RectTransform;
        if (tipPanel != null)
        {
            tipPanelFound = true;
            if (FillsParent(tipPanel))
            {
                tipPanel.pivot = new Vector2(0.5f, 0.5f);
                menuPanels.Add(tipPanel);
            }
        }
    }

    // True for a rectangle stretched over all of its parent.
    private static bool FillsParent(RectTransform rect)
    {
        return rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one
            && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero;
    }

    // A menu panel (Start, You survived, You died, pause) has a button or a
    // toggle among its own children. (The HUD's powers list has buttons too,
    // but deeper down, inside the Readouts layer.)
    private static bool IsMenuPanel(Transform child)
    {
        foreach (Transform grandchild in child)
        {
            if (grandchild.GetComponent<Button>() != null || grandchild.GetComponent<Toggle>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private static void MoveRight(Transform piece, float distance)
    {
        RectTransform rect = piece as RectTransform;
        if (rect != null)
        {
            rect.anchoredPosition += new Vector2(distance, 0f);
        }
    }

    // The row above the phone's keyboard, left to right: pause, the four
    // powers, TAP TO TYPE. It is drawn after the play area and before the menu
    // panels, so a panel covers it (and blocks its buttons).
    private void BuildButtonRow()
    {
        // Behind the phone's keyboard nothing is drawn by the game (the view is
        // above it): black, so nothing old shows while the keyboard slides.
        Image backdrop = TouchUI.CreateImage("Touch Keyboard Backdrop", canvasRect, Color.black);
        backdrop.raycastTarget = false;
        keyboardBackdrop = backdrop.rectTransform;
        keyboardBackdrop.SetSiblingIndex(playArea.GetSiblingIndex() + 1);
        keyboardBackdrop.anchorMin = new Vector2(0f, 0f);
        keyboardBackdrop.anchorMax = new Vector2(1f, 0f);
        keyboardBackdrop.pivot = new Vector2(0.5f, 0f);
        keyboardBackdrop.anchoredPosition = Vector2.zero;

        buttonRow = TouchUI.CreateImage("Touch Button Row", canvasRect, RowColor).rectTransform;
        buttonRow.SetSiblingIndex(keyboardBackdrop.GetSiblingIndex() + 1);
        buttonRow.anchorMin = new Vector2(0f, 0f);
        buttonRow.anchorMax = new Vector2(1f, 0f);
        buttonRow.pivot = new Vector2(0.5f, 0f);

        float total = PauseWeight + 4f + TypeWeight;
        float left = 0f;

        // Pause: the same pause as Esc (analytics reason "button").
        RectTransform pauseSlot = CreateRowSlot("Touch Pause Button", left, PauseWeight, total);
        Image pauseImage = pauseSlot.gameObject.AddComponent<Image>();
        pauseImage.color = new Color(0.14f, 0.15f, 0.19f);
        TMP_Text pauseLabel = TouchUI.CreateText("Label", pauseSlot, "II", 50f);
        pauseLabel.fontStyle = FontStyles.Bold;
        FitText(pauseLabel, 30f, 110f);
        pauseSlot.gameObject.AddComponent<Button>().onClick.AddListener(OnPauseButton);
        left += PauseWeight;

        CreateRowPower(ref left, total, TouchPowerButton.Power.Lure, "1", "LURE", Palette.LureCrate);
        CreateRowPower(ref left, total, TouchPowerButton.Power.Freeze, "2", "FREEZE", Palette.FreezeCrate);
        CreateRowPower(ref left, total, TouchPowerButton.Power.Frenzy, "3", "FRENZY", Palette.WeaponFrenzy);
        CreateRowPower(ref left, total, TouchPowerButton.Power.Rocket, "4", "RPG", Palette.BlastOrange);

        RectTransform typeSlot = CreateRowSlot("Touch Type Button", left, TypeWeight, total);
        Image typeImage = typeSlot.gameObject.AddComponent<Image>();
        TMP_Text typeLabel = TouchUI.CreateText("Label", typeSlot, "TAP TO\nTYPE", 30f);
        typeLabel.fontStyle = FontStyles.Bold;
        typeLabel.lineSpacing = -15f;
        typeLabel.margin = new Vector4(8f, 4f, 8f, 4f);
        FitText(typeLabel, 20f, 64f);
        typeSlot.gameObject.AddComponent<TouchTypeButton>().Setup(typeImage, typeLabel);
        typeButton = typeSlot;
    }

    // The row is about a finger tall in CSS pixels, so in canvas units it is
    // taller held upright: its texts grow (between minSize and maxSize) to
    // fill their buttons.
    private static void FitText(TMP_Text label, float minSize, float maxSize)
    {
        label.enableAutoSizing = true;
        label.fontSizeMin = minSize;
        label.fontSizeMax = maxSize;
    }

    // A slot of the button row: 'weight' parts wide, starting 'left' parts from
    // the left edge, out of 'total' parts.
    private RectTransform CreateRowSlot(string objectName, float left, float weight, float total)
    {
        RectTransform slot = TouchUI.CreateRect(objectName, buttonRow);
        slot.anchorMin = new Vector2(left / total, 0f);
        slot.anchorMax = new Vector2((left + weight) / total, 1f);
        slot.offsetMin = new Vector2(RowGap * 0.5f, RowGap);
        slot.offsetMax = new Vector2(-RowGap * 0.5f, -RowGap);
        return slot;
    }

    private void CreateRowPower(ref float left, float total, TouchPowerButton.Power power, string key, string powerName, Color color)
    {
        RectTransform slot = CreateRowSlot("Power " + powerName, left, 1f, total);
        slot.gameObject.AddComponent<TouchPowerButton>().Setup(power, key, powerName, color);
        left += 1f;
    }

    // How tall the button row is: about a finger (FingerSize CSS pixels), but
    // never more than a fifth of the screen.
    private float ButtonRowHeight()
    {
        float height = canvasRect.rect.height;
        float unitsPerCssPixel = height / CanvasCssHeight();
        return Mathf.Clamp(FingerSize * unitsPerCssPixel, 100f, height * 0.2f);
    }

    // The game's height on the page in CSS pixels (what finger and text sizes
    // on a phone are measured in). In the Editor: screen pixels.
    private static float CanvasCssHeight()
    {
        float cssHeight = Screen.height;
#if UNITY_WEBGL && !UNITY_EDITOR
        cssHeight = TouchControlsWeb_CanvasCssHeight();
#endif
        return Mathf.Max(1f, cssHeight);
    }

    // UseOwnKeyboard: the bar at the bottom, power buttons 1 and 2 on the left,
    // the keyboard in the middle, 3 and 4 on the right.
    private void BuildKeyboardBar()
    {
        bar = TouchUI.CreateImage("Touch Keyboard Bar", canvasRect, BarColor).rectTransform;
        bar.SetSiblingIndex(playArea.GetSiblingIndex() + 1);
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(0f, BarHeight);

        leftColumn = TouchUI.CreateRect("Left Powers", bar);
        rightColumn = TouchUI.CreateRect("Right Powers", bar);
        keysArea = TouchUI.CreateRect("Keys", bar);
        UpdateBarLayout();

        TouchKeyboard keyboard = keysArea.gameObject.AddComponent<TouchKeyboard>();
        keyboard.Build();

        CreateColumnPower(leftColumn, true, TouchPowerButton.Power.Lure, "1", "LURE BOMB", Palette.LureCrate);
        CreateColumnPower(leftColumn, false, TouchPowerButton.Power.Freeze, "2", "FREEZE", Palette.FreezeCrate);
        CreateColumnPower(rightColumn, true, TouchPowerButton.Power.Frenzy, "3", "FRENZY", Palette.WeaponFrenzy);
        CreateColumnPower(rightColumn, false, TouchPowerButton.Power.Rocket, "4", "RPG", Palette.BlastOrange);
    }

    // UseOwnKeyboard: the columns' width depends on the screen width (it
    // changes when the phone turns or the window is resized), so this runs
    // every frame.
    private void UpdateBarLayout()
    {
        float side = Mathf.Min(MaxSideWidth, canvasRect.rect.width * SideWidthFraction);

        leftColumn.anchorMin = new Vector2(0f, 0f);
        leftColumn.anchorMax = new Vector2(0f, 1f);
        leftColumn.offsetMin = new Vector2(BarPadding, BarPadding);
        leftColumn.offsetMax = new Vector2(BarPadding + side, -BarPadding);

        rightColumn.anchorMin = new Vector2(1f, 0f);
        rightColumn.anchorMax = new Vector2(1f, 1f);
        rightColumn.offsetMin = new Vector2(-BarPadding - side, BarPadding);
        rightColumn.offsetMax = new Vector2(-BarPadding, -BarPadding);

        keysArea.anchorMin = Vector2.zero;
        keysArea.anchorMax = Vector2.one;
        keysArea.offsetMin = new Vector2(2f * BarPadding + side, BarPadding * 0.5f);
        keysArea.offsetMax = new Vector2(-2f * BarPadding - side, -BarPadding * 0.5f);
    }

    // UseOwnKeyboard: one power button, the top or bottom half of a column.
    private static void CreateColumnPower(RectTransform column, bool top, TouchPowerButton.Power power,
        string key, string powerName, Color color)
    {
        RectTransform rect = TouchUI.CreateRect("Power " + powerName, column);
        rect.anchorMin = new Vector2(0f, top ? 0.5f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0.5f);
        rect.offsetMin = new Vector2(0f, top ? PowerButtonGap * 0.5f : 0f);
        rect.offsetMax = new Vector2(0f, top ? 0f : -PowerButtonGap * 0.5f);
        rect.gameObject.AddComponent<TouchPowerButton>().Setup(power, key, powerName, color);
    }

    // UseOwnKeyboard: the pause button sits in the play area's top-left corner
    // (last, so it is drawn over the other gameplay pieces).
    private void BuildPauseButton()
    {
        Image image = TouchUI.CreateImage("Touch Pause Button", playArea, CornerButtonColor);
        TouchUI.Place(image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -14f),
            new Vector2(CornerButtonWidth, 96f));
        TMP_Text label = TouchUI.CreateText("Label", image.rectTransform, "II", 50f);
        label.fontStyle = FontStyles.Bold;
        image.gameObject.AddComponent<Button>().onClick.AddListener(OnPauseButton);
        pauseButton = image.gameObject;
    }

    private void OnPauseButton()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame(); // the same pause as Esc (analytics reason "button")
        }
    }

    // FULL SCREEN hides the browser's address bar. Only where the browser can
    // do it (an iPhone cannot). It is drawn last, over the menu panels, in the
    // bottom-left corner (empty on every menu screen).
    private void BuildFullscreenButton()
    {
        fullscreenButton = null;
        if (!TouchFullscreenButton.IsSupported())
        {
            return;
        }
        Image image = TouchUI.CreateImage("Touch Fullscreen Button", canvasRect, CornerButtonColor);
        TouchUI.Place(image.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f),
            new Vector2(CornerButtonWidth, 96f));
        TMP_Text label = TouchUI.CreateText("Label", image.rectTransform, "FULL\nSCREEN", 24f);
        label.lineSpacing = -20f;
        image.gameObject.AddComponent<TouchFullscreenButton>().Setup(label);
        fullscreenButton = image.gameObject;
    }

    // ---- Every frame ----

    // While playing (and during the 3-2-1 countdown): the keyboard and the
    // buttons, with the view in the space left above them. Full screen while a
    // panel or a tip box is open (Start, pause, tips, end screens): those are
    // laid out for the whole screen and need no keyboard.
    private void UpdateLayout()
    {
        GameState state = GameManager.Instance != null ? GameManager.Instance.State : GameState.Start;
        // Paused with the pause panel closed = the 3-2-1 countdown: the keyboard
        // is back, so the player is ready when play resumes.
        bool countdown = state == GameState.Paused && (pausePanel == null || !pausePanel.activeInHierarchy);
        bool wantPlayLayout = state == GameState.Playing || countdown;
        if (!layoutKnown || wantPlayLayout != playLayout)
        {
            playLayout = wantPlayLayout;
            layoutKnown = true;
            if (UseOwnKeyboard)
            {
                bar.gameObject.SetActive(playLayout);
            }
            else
            {
                buttonRow.gameObject.SetActive(playLayout);
#if UNITY_WEBGL && !UNITY_EDITOR
                TouchControlsWeb_SetKeyboardWanted(playLayout ? 1 : 0);
#endif
            }
            if (!playLayout)
            {
                TouchInput.ReleaseAll();
            }
            else
            {
                nextShortHint = Mathf.Max(nextShortHint, Time.unscaledTime + 2f); // not in the first moment (a tip may open)
            }
        }

        bool upright = FitHudToScreenShape();
        float height = Mathf.Max(1f, canvasRect.rect.height);

        // What covers the screen: from the bottom (keyboard, buttons) and from
        // the top (the browser scrolled the page up for its keyboard).
        float bottom = 0f;
        float top = 0f;
        if (UseOwnKeyboard)
        {
            if (playLayout)
            {
                UpdateBarLayout();
                bottom = BarHeight;
            }
            pauseButton.SetActive(state == GameState.Playing);
        }
        else
        {
            float keyboardBottom = 0f;
            KeyboardOpen = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            KeyboardOpen = TouchControlsWeb_IsKeyboardOpen() == 1;
            keyboardBottom = TouchControlsWeb_CoveredBottom() * height;
            top = playLayout ? TouchControlsWeb_CoveredTop() * height : 0f;
#endif
            float rowHeight = ButtonRowHeight();
            buttonRow.anchoredPosition = new Vector2(0f, keyboardBottom);
            buttonRow.sizeDelta = new Vector2(0f, rowHeight);
            keyboardBackdrop.sizeDelta = new Vector2(0f, keyboardBottom);
            keyboardBackdrop.gameObject.SetActive(playLayout && keyboardBottom > 0f);
            if (playLayout)
            {
                bottom = keyboardBottom + rowHeight;
            }
        }
        bottom = Mathf.Min(bottom, height * 0.9f - top); // always leave some of the view
        float free = height - bottom - top;
        if (gameCamera != null)
        {
            gameCamera.rect = new Rect(0f, bottom / height, 1f, free / height);
        }

        // The play area covers the free part of the screen. When that is
        // shorter than MinPlayHeight, everything in it is drawn smaller (the
        // play area itself stays MinPlayHeight units tall inside).
        float width = canvasRect.rect.width;
        float playScale = Mathf.Clamp(free / MinPlayHeight, MinPlayScale, 1f);
        playArea.localScale = new Vector3(playScale, playScale, 1f);
        playArea.sizeDelta = new Vector2(width / playScale - width, free / playScale - height);
        playArea.anchoredPosition = new Vector2(0f, (bottom - top) * 0.5f);
        float playHeight = free / playScale; // in the play area's own units
        AdoptBossBar();

        // The words, floating texts and hints: held upright bigger again (as
        // long as their layer stays MinPlayHeight units tall inside), and in a
        // shrunken play area never smaller than readable.
        float layerScale = upright ? Mathf.Clamp(playHeight / MinPlayHeight, 1f, UprightUiScale) : 1f;
        float cssPixelsPerUnit = CanvasCssHeight() / height;
        float readableScale = MinWordCssPixels / (SmallestWordUnits * cssPixelsPerUnit);
        layerScale = Mathf.Max(layerScale, readableScale / playScale);
        // The top of the play area belongs to the combo (and the boss's health
        // bar in a boss fight). A short word layer reaches up above the play
        // area (see WordTopReach), but never so far that the words get there.
        bool bossFight = bossBar != null && bossBar.gameObject.activeInHierarchy;
        float topBand = bossFight ? BossBand : ComboBand;
        float wordMargin = bossFight ? BossWordTopMargin : WordTopMargin;
        foreach (RectTransform layer in uprightLayers)
        {
            if (layer == null)
            {
                continue;
            }
            float scale = layerScale;
            float reach = 0f;
            if (layer == wordLayer)
            {
                reach = Mathf.Min(MinPlayHeight - playHeight / scale, wordMargin - topBand / scale);
                reach = Mathf.Clamp(reach, 0f, WordTopReach);
            }
            else if (layer == hintLayer)
            {
                scale = Mathf.Min(scale, playHeight / HintRoom); // the hint box must fit
            }
            ScaleInPlace(layer, scale, reach);
        }

        // Held upright, the menus and the FULL SCREEN button finger-sized again.
        float menuScale = upright ? UprightUiScale : 1f;
        FindTipPanel();
        foreach (RectTransform panel in menuPanels)
        {
            if (panel != null)
            {
                ScaleInPlace(panel, menuScale, 0f);
            }
        }
        FitMenuTexts(width / menuScale - 2f * MenuTextMargin);
        if (fullscreenButton != null)
        {
            fullscreenButton.transform.localScale = new Vector3(menuScale, menuScale, 1f);
            fullscreenButton.SetActive(!playLayout && state != GameState.Tip);
        }

        // The wave banner, the big popup and the countdown sit around the middle
        // of the screen. In a play area shorter than the HUD was made for they
        // move closer to its middle, never up into the combo (and the boss's
        // health bar) at the top, and never out of the bottom.
        float reference = scaler != null ? sidewaysReference.y : 1080f;
        float placeScale = Mathf.Min(1f, playHeight / reference);
        foreach (KeyValuePair<RectTransform, float> piece in centredPieces)
        {
            if (piece.Key == null)
            {
                continue;
            }
            float y = piece.Value * placeScale;
            if (piece.Value > 0f)
            {
                TMP_Text text = piece.Key.GetComponent<TMP_Text>();
                float halfHeight = (text != null ? text.preferredHeight : piece.Key.rect.height) * 0.5f;
                float highest = playHeight * 0.5f - topBand - halfHeight;
                float lowest = halfHeight - playHeight * 0.5f;
                y = Mathf.Max(Mathf.Min(y, highest), lowest);
            }
            piece.Key.anchoredPosition = new Vector2(piece.Key.anchoredPosition.x, y);
        }

        // Sideways with the keyboard up on a phone, the view can be very
        // short: suggest holding the phone upright (there is room there). Not
        // over another hint (a first-time explanation is only shown once).
        if (!upright && playLayout && KeyboardOpen && free * cssPixelsPerUnit < ShortStripCss
            && state == GameState.Playing && Time.unscaledTime >= nextShortHint)
        {
            Transform hintBox = hintLayer != null ? hintLayer.Find("HintBox") : null;
            if (hintBox != null && hintBox.gameObject.activeSelf)
            {
                nextShortHint = Time.unscaledTime + 1f; // try again when it is gone
            }
            else
            {
                nextShortHint = Time.unscaledTime + ShortHintEvery;
                hud.ShowHint("Hold your phone upright to see more while you type.", 4f);
            }
        }

        HideHudPowersList();
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!UseOwnKeyboard)
        {
            SendTypeButtonRect();
        }
#endif
    }

    // The boss's health bar: HUD makes it at the first boss fight, on the
    // canvas itself. It belongs to the play area (under its other pieces, as it
    // was under everything), so it stays in line with the combo and the words.
    private void AdoptBossBar()
    {
        Transform found = canvasRect.Find("BossBar");
        if (found != null)
        {
            found.SetParent(playArea, false);
            found.SetAsFirstSibling();
            bossBar = found;
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    // Tells the .jslib where TAP TO TYPE / HIDE KEYBOARD is (fractions of the
    // screen, from the bottom-left), so the browser opens and closes the
    // keyboard during the tap itself (an iPhone only allows it then).
    private void SendTypeButtonRect()
    {
        Rect onScreen = new Rect(); // nothing: the row is hidden
        if (playLayout && typeButton != null)
        {
            typeButton.GetWorldCorners(corners); // screen pixels (the HUD canvas is drawn over the screen)
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);
            onScreen = Rect.MinMaxRect(corners[0].x / screenWidth, corners[0].y / screenHeight,
                corners[2].x / screenWidth, corners[2].y / screenHeight);
        }
        if (onScreen != sentTypeButton)
        {
            sentTypeButton = onScreen;
            TouchControlsWeb_SetTypeButtonRect(onScreen.xMin, onScreen.yMin, onScreen.xMax, onScreen.yMax);
        }
    }
#endif

    // Sideways the HUD is 1080 units tall (as made). Held upright (taller than
    // wide) it is UprightWidth units wide instead, so the pieces made for a wide
    // screen still fit side by side. Returns true when held upright.
    private bool FitHudToScreenShape()
    {
        bool upright = Screen.height > Screen.width;
        if (scaler == null)
        {
            return upright;
        }
        if (upright)
        {
            scaler.matchWidthOrHeight = 0f; // match the width
            scaler.referenceResolution = new Vector2(UprightWidth, sidewaysReference.y);
        }
        else
        {
            scaler.matchWidthOrHeight = sidewaysMatch;
            scaler.referenceResolution = sidewaysReference;
        }
        return upright;
    }

    // Draws a rectangle that fills its parent 'scale' times bigger. It is
    // made smaller by the same amount, so it still covers exactly its parent:
    // its pieces keep their places at its edges and middle, just bigger, and
    // screen points still land on the right spot inside it (WaveSpawner
    // places the words from screen points and keeps them inside its edges).
    // reachUp (in its own units) lets its top edge reach that far above the
    // parent's top edge.
    private static void ScaleInPlace(RectTransform rect, float scale, float reachUp)
    {
        Vector2 full = ((RectTransform)rect.parent).rect.size;
        rect.localScale = new Vector3(scale, scale, 1f);
        rect.sizeDelta = new Vector2(full.x / scale - full.x, full.y / scale - full.y + reachUp);
        rect.anchoredPosition = new Vector2(0f, reachUp * scale * 0.5f);
    }

    // A menu text made wider than the screen (held upright, where the panels
    // are drawn bigger, or a tablet's narrow screen held sideways) would stick
    // out on both sides: it is made as wide as the screen (maxWidth, in the
    // panel's units) and its letters shrink if they need to (down to half).
    // The others stay as they were made.
    private void FitMenuTexts(float maxWidth)
    {
        if (Mathf.Approximately(maxWidth, menuTextsFitTo))
        {
            return; // nothing changed
        }
        menuTextsFitTo = maxWidth;
        for (int i = 0; i < menuTexts.Count; i++)
        {
            TMP_Text text = menuTexts[i];
            if (text == null)
            {
                continue;
            }
            RectTransform rect = text.rectTransform;
            bool tooWide = menuTextWidths[i] > maxWidth;
            rect.sizeDelta = new Vector2(tooWide ? maxWidth : menuTextWidths[i], rect.sizeDelta.y);
            text.enableAutoSizing = tooWide;
            text.margin = menuTextMargins[i];
            if (tooWide)
            {
                text.fontSizeMax = menuTextSizes[i];
                text.fontSizeMin = menuTextSizes[i] * 0.5f;
                if (text.textWrappingMode != TextWrappingModes.NoWrap && rect.sizeDelta.y > 4f * MenuTextMargin)
                {
                    // Narrower, a long text wraps into more lines: keep the
                    // last one off whatever is just below (the Start button).
                    text.margin = menuTextMargins[i] + new Vector4(0f, 0f, 0f, MenuTextMargin);
                }
            }
            else
            {
                text.fontSize = menuTextSizes[i];
            }
        }
    }

    // The HUD's own powers list (bottom-left) is replaced by the big buttons,
    // which show the same charges. It is built the first time a power changes.
    private void HideHudPowersList()
    {
        if (hudPowersPanel == null)
        {
            Transform readouts = playArea.Find("Readouts");
            hudPowersPanel = readouts != null ? readouts.Find("Powers") : null;
        }
        if (hudPowersPanel != null && hudPowersPanel.gameObject.activeSelf)
        {
            hudPowersPanel.gameObject.SetActive(false);
        }
    }

    // Replaces the texts that talk about keys (see KeyTexts). A text is only
    // looked at again after its text changed.
    private void PatchKeyTexts()
    {
        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }
            string original = text.text;
            string lastChecked;
            if (checkedTexts.TryGetValue(text, out lastChecked) && (object)lastChecked == (object)original)
            {
                continue; // the very same text as last time
            }
            checkedTexts[text] = original;
            if (string.IsNullOrEmpty(original))
            {
                continue;
            }
            string patched = original;
            for (int i = 0; i < KeyTexts.Length; i += 2)
            {
                patched = ReplaceIgnoringCase(patched, KeyTexts[i], KeyTexts[i + 1]);
            }
            if (patched != original)
            {
                text.text = patched;
                checkedTexts[text] = text.text;
            }
        }
    }

    // Like string.Replace, but "Esc: Pause" also matches "Esc: pause" (the
    // scenes do not all write their texts the same way).
    private static string ReplaceIgnoringCase(string text, string find, string replacement)
    {
        int at = text.IndexOf(find, System.StringComparison.OrdinalIgnoreCase);
        while (at >= 0)
        {
            text = text.Substring(0, at) + replacement + text.Substring(at + find.Length);
            at = text.IndexOf(find, at + replacement.Length, System.StringComparison.OrdinalIgnoreCase);
        }
        return text;
    }

    // Fingers are bigger than a mouse pointer: every button and toggle reacts a
    // little outside its edges (its look does not change). The extra space
    // never reaches into a neighbouring button.
    private void EnlargeButtonTouchAreas()
    {
        Selectable[] all = FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Selectable selectable in all)
        {
            if (!(selectable is Button) && !(selectable is Toggle))
            {
                continue;
            }
            RectTransform rect = selectable.transform as RectTransform;
            Graphic graphic = selectable.targetGraphic;
            if (rect == null || graphic == null || (buttonRow != null && rect.parent == buttonRow))
            {
                continue; // (the button row's pause button already has its neighbours right beside it)
            }

            float padX = MaxTouchPadding;
            float padY = MaxTouchPadding;
            Rect mine = RectInParent(rect);
            foreach (Selectable other in all)
            {
                RectTransform otherRect = other.transform as RectTransform;
                if (other == selectable || otherRect == null || otherRect.parent != rect.parent)
                {
                    continue;
                }
                Rect theirs = RectInParent(otherRect);
                bool sideBySide = mine.yMin < theirs.yMax && theirs.yMin < mine.yMax;
                bool aboveOrBelow = mine.xMin < theirs.xMax && theirs.xMin < mine.xMax;
                if (sideBySide)
                {
                    float gap = Mathf.Max(theirs.xMin - mine.xMax, mine.xMin - theirs.xMax);
                    padX = Mathf.Min(padX, Mathf.Max(0f, gap * 0.5f - 2f));
                }
                if (aboveOrBelow)
                {
                    float gap = Mathf.Max(theirs.yMin - mine.yMax, mine.yMin - theirs.yMax);
                    padY = Mathf.Min(padY, Mathf.Max(0f, gap * 0.5f - 2f));
                }
            }
            graphic.raycastPadding = new Vector4(-padX, -padY, -padX, -padY); // negative = bigger
        }
    }

    // A rectangle's area in its parent's coordinates (canvas units).
    private static Rect RectInParent(RectTransform rect)
    {
        Rect local = rect.rect;
        Vector2 position = rect.localPosition;
        return new Rect(position.x + local.xMin, position.y + local.yMin, local.width, local.height);
    }
}
