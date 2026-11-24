// MainMenu.cs
// ---------------------------------------------------------------------------
// The menu scene ("Menu.unity"). Builds its whole UI in code (like HUD does), so
// the scene only needs: a camera, an EventSystem and ONE object with this script.
//
//   MAIN PAGE:   the title on keycaps with a red tape under it, a yellow START
//                block, FULL SCREEN and ALMANAC as terminal buttons, a crowd of
//                zombies standing in a blast ring and the SETTINGS gear top-right
//                (see SettingsPanel).
//   LEVEL PAGE:  "SELECT LEVEL" on keycaps and one terminal-window card per level
//                (the list comes from GameManager.LevelName, so there is a single
//                list of levels, plus the Endless arena) and a BACK button.
//
// All the pictures are drawn in code (see MenuArt, MenuBackground). Clicking a
// card loads that scene by name. The scene must be in Build Settings.
// ---------------------------------------------------------------------------
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Tells MainMenu that the mouse is over a button, so the mouse and the keys share one selection.
public class MenuHoverRelay : MonoBehaviour, IPointerEnterHandler
{
    public System.Action onEnter;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (onEnter != null)
        {
            onEnter();
        }
    }
}

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameTitle = "The Typeblaster of the Dead";
    [SerializeField] private Color backgroundColor = new Color(0.04f, 0.025f, 0.06f);

    // ---- Look ----
    private static readonly Color Ink = new Color(0.03f, 0.04f, 0.08f);
    private static readonly Color Cream = new Color(0.93f, 0.91f, 0.87f);
    private static readonly Color Amber = new Color(0.93f, 0.91f, 0.87f, 0.55f);
    private static readonly Color StartFace = new Color(0.93f, 0.91f, 0.87f);
    private static readonly Color StartHover = new Color(1f, 1f, 1f);
    private static readonly Color StartPressed = new Color(0.72f, 0.70f, 0.66f);
    private static readonly Color TerminalFace = new Color(0.07f, 0.055f, 0.10f, 0.94f);
    private static readonly Color TerminalHover = new Color(0.17f, 0.13f, 0.25f, 1f);
    private static readonly Color TerminalPressed = new Color(0.04f, 0.03f, 0.06f, 1f);
    private static readonly Color CardFace = new Color(0.086f, 0.067f, 0.12f);
    private static readonly Color CardHover = new Color(0.15f, 0.12f, 0.21f);
    private static readonly Color CardPressed = new Color(0.06f, 0.045f, 0.09f);
    private static readonly Color FrameGold = new Color(0.93f, 0.91f, 0.87f);
    private const string Prompt = "<color=#E0262A>></color>  ";   // the terminal's ">" in front of a label

    // What each level card says, in the order of GameManager's list; Endless comes last.
    private static readonly string[] LevelPlaces = { "USC CAMPUS", "THE HOLLOW FOREST" };
    private static readonly string[] LevelTags = { "3 WAVES + BOSS", "FAST ZOMBIES / LONG WORDS" };
    private static readonly int[] LevelDifficulty = { 2, 4 };
    private static readonly Color[] LevelAccent =
    {
        new Color(0.93f, 0.91f, 0.87f), new Color(0.93f, 0.91f, 0.87f)
    };
    private static readonly Color EndlessAccent = new Color(0.93f, 0.91f, 0.87f);

    // One terminal-window card on the level page.
    private sealed class LevelCard
    {
        public RectTransform root;        // the button: layout position and the keyboard frame
        public RectTransform content;     // slides and fades in when the page opens
        public CanvasGroup group;
        public RectTransform bar, barFill, mapBox, shadow;
        public RectTransform[] dots = new RectTransform[3];
        public RectTransform[] pips = new RectTransform[5];
        public Image map;
        public TMP_Text code, name, place, tag;
        public int mapIndex;
    }

    // A title spelled on keycaps.
    private sealed class KeyTitle
    {
        public RectTransform group;
        public readonly List<RectTransform> keys = new List<RectTransform>();
        public readonly List<bool> gaps = new List<bool>();     // true = this item is the space between words
        public readonly List<Vector2> basePositions = new List<Vector2>();
        public float size;
    }

    // One zombie of the crowd on the main page.
    private sealed class CrowdMember
    {
        public RectTransform rect;
        public float baseY, phase;
    }

    private GameObject mainPage;
    private GameObject levelPage;
    private TMP_Text levelMessage;   // shows an error if a level scene is missing from Build Settings
    private bool loading;
    private RectTransform canvasRect;
    private CanvasScaler canvasScaler;
    private KeyTitle mainKeys, levelKeys;
    private RectTransform tape;
    private RectTransform startButton, backButton;
    private RectTransform endlessButton;
    private RectTransform almanacButton;
    private AlmanacPanel almanac;   // the ALMANAC screen, opened over the menu
    private RectTransform fullscreenButton;
    private TMP_Text fullscreenHint, fullscreenLabel, footer;
    private RectTransform crowd, boom, floorRing;
    private readonly List<CrowdMember> crowdMembers = new List<CrowdMember>();
    private readonly List<RectTransform> levelButtons = new List<RectTransform>();
    private readonly List<LevelCard> cards = new List<LevelCard>();
    private Vector2 lastLayoutSize;
    private bool portraitLayout;
    private readonly List<RectTransform> menuItems = new List<RectTransform>(); // buttons the keys can select on the current page
    private int selected;           // index in menuItems
    private bool keyboardSelectionStarted;
    private bool almanacWasOpen;    // the same for the Almanac
    private bool settingsWasOpen;   // SettingsPanel was open last frame (it closes itself on Esc / Enter)
    private float levelPageOpenedAt;
    private static bool openLevelSelectionOnStart;

    // The full-screen question: asked once, when the game first opens (not again
    // when a level returns to the menu), and only where the browser can do it.
    private static bool fullscreenAsked;
    private GameObject fullscreenGate;
    private RectTransform gateYes, gateNo;
    private TMP_Text gateTitle, gateText;
    private int gateSelected;       // 0 = GO FULL SCREEN, 1 = STAY IN WINDOW
    private float gateClosedAt = -1f;

    private const string EndlessScene = "Endless";
    // Picking a level: the button pulses once, then the screen fades to black.
    private const float LaunchPulseSeconds = 0.4f;
    private const float LaunchFadeStartSeconds = 0.4f;   // when the fade to black begins
    private const float LaunchPulseGrowth = 0.1f;

    private const float SelectedScale = 1.05f;
    private const float CardAppearSeconds = 0.35f;
    private const float CardAppearStagger = 0.08f;

    private void Start()
    {
        Time.timeScale = 1f; // in case we arrive here from a paused game
        Sfx.Preload("UI/button_click");

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
            ShowFullscreenGate();
        }
        fullscreenAsked = true;
        StartCoroutine(PaintMaps());
    }

    // The level pictures take a few milliseconds each: one per frame, after the first screen is up.
    private IEnumerator PaintMaps()
    {
        for (int index = 0; index < MenuArt.MapCount; index++)
        {
            yield return null;
            Sprite picture = MenuArt.Map(index);
            foreach (LevelCard card in cards)
            {
                if (card.mapIndex == index)
                {
                    card.map.sprite = picture;
                    card.map.color = Color.white;
                }
            }
        }
    }

    private void Update()
    {
        UpdateResponsiveLayout();
        LayoutFullscreenGate();
        UpdateFullscreenLabel();
        AnimateKeys(mainKeys);
        AnimateKeys(levelKeys);
        AnimateCrowd();
        AnimateCards();
        HandleKeys();
    }

    // The keys of a title bob in a slow wave.
    private static void AnimateKeys(KeyTitle title)
    {
        if (title == null || !title.group.gameObject.activeInHierarchy) return;
        float time = Time.unscaledTime;
        for (int i = 0; i < title.keys.Count; i++)
        {
            Vector2 position = title.basePositions[i];
            position.y += Mathf.Sin(time * 2.2f + i * 0.55f) * title.size * 0.02f;
            title.keys[i].anchoredPosition = position;
        }
    }

    // The crowd sways on the spot; the tape and the BOOM sticker rock a little.
    private void AnimateCrowd()
    {
        if (crowd == null || !crowd.gameObject.activeInHierarchy) return;
        float time = Time.unscaledTime;
        foreach (CrowdMember member in crowdMembers)
        {
            Vector2 position = member.rect.anchoredPosition;
            position.y = member.baseY + Mathf.Sin(time * 1.8f + member.phase) * 6f;
            member.rect.anchoredPosition = position;
        }

        tape.localEulerAngles = new Vector3(0f, 0f, -2.5f + Mathf.Sin(time * 1.1f) * 0.3f);
    }

    // The cards slide up and fade in one after another when the level page opens.
    private void AnimateCards()
    {
        if (levelPage == null || !levelPage.activeSelf) return;
        float since = Time.unscaledTime - levelPageOpenedAt;
        for (int i = 0; i < cards.Count; i++)
        {
            float t = Mathf.Clamp01((since - i * CardAppearStagger) / CardAppearSeconds);
            float e = Ui.EaseOut(t);
            cards[i].group.alpha = e;
            cards[i].content.anchoredPosition = new Vector2(0f, -(1f - e) * 70f);
        }
    }

    // The menu has no HUD, so TouchControls cannot adapt its layout for it.
    // Use the canvas aspect (also works in Device Simulator), not a platform flag.
    private void UpdateResponsiveLayout()
    {
        if (canvasRect == null || mainKeys == null) return;
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
        LayoutMainPage(size, width, portrait);
        LayoutLevelPage(size, width, portrait);
    }

    private void LayoutMainPage(Vector2 size, float width, bool portrait)
    {
        float top = size.y * 0.5f;
        LayoutKeys(mainKeys, portrait ? 430f : top - 168f, portrait ? 60f : 93f, width);

        PlaceCenter(tape, new Vector2(0f, portrait ? 290f : top - 321f), portrait ? new Vector2(760f, 142f) : new Vector2(780f, 146f));
        TMP_Text tapeText = tape.GetComponentInChildren<TMP_Text>(true);
        tapeText.fontSize = portrait ? 66f : 84f;

        LayoutButton(startButton, new Vector2(0f, portrait ? -10f : top - 501f),
            portrait ? new Vector2(780f, 210f) : new Vector2(640f, 172f), portrait ? 96f : 84f);

        // FULL SCREEN and ALMANAC: side by side (stacked on a phone) under START.
        bool hasFullscreen = fullscreenButton != null;
        Vector2 small = portrait ? new Vector2(700f, 120f) : new Vector2(495f, 93f);
        float smallSize = portrait ? 48f : 40f;
        if (hasFullscreen)
        {
            LayoutButton(fullscreenButton, new Vector2(portrait ? 0f : -315f, portrait ? -250f : top - 684f), small, smallSize);
            LayoutButton(almanacButton, new Vector2(portrait ? 0f : 315f, portrait ? -400f : top - 684f), small, smallSize);
            fullscreenLabel.fontSize = smallSize;
            PlaceText(fullscreenHint, width, 60f, portrait ? -500f : top - 759f, portrait ? 32f : 28f);
        }
        else
        {
            LayoutButton(almanacButton, new Vector2(0f, portrait ? -250f : top - 684f), small, smallSize);
        }

        // the crowd stands at the bottom of a wide screen only
        crowd.gameObject.SetActive(!portrait);

        footer.rectTransform.anchorMin = Ui.TopLeft;
        footer.rectTransform.anchorMax = Ui.TopLeft;
        footer.rectTransform.pivot = Ui.TopLeft;
        footer.rectTransform.anchoredPosition = new Vector2(32f, -26f);
        footer.rectTransform.sizeDelta = new Vector2(600f, 40f);
        footer.alignment = TextAlignmentOptions.Left;
        footer.fontSize = portrait ? 28f : 22f;
    }

    private void LayoutLevelPage(Vector2 size, float width, bool portrait)
    {
        float top = size.y * 0.5f;
        LayoutKeys(levelKeys, portrait ? top - 230f : top - 111f, portrait ? 66f : 75f, width);

        int count = cards.Count;
        float cardWidth, cardHeight, gap;
        if (portrait)
        {
            gap = 24f;
            cardWidth = Mathf.Min(940f, size.x - 80f);
            cardHeight = Mathf.Min(400f, (size.y - 640f - gap * (count - 1)) / count);
        }
        else
        {
            gap = 57f;
            cardWidth = Mathf.Max(260f, Mathf.Min(558f, (size.x - 120f - gap * (count - 1)) / count));
            cardHeight = Mathf.Min(cardWidth * 1.18f, size.y - 330f);
        }

        float bottomOfCards = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector2 position;
            if (portrait)
            {
                float firstTop = top - 330f - cardHeight * 0.5f;
                position = new Vector2(0f, firstTop - i * (cardHeight + gap));
            }
            else
            {
                position = new Vector2((i - (count - 1) * 0.5f) * (cardWidth + gap), -30f);
            }
            PlaceCenter(cards[i].root, position, new Vector2(cardWidth, cardHeight));
            LayoutCard(cards[i], cardWidth, cardHeight, portrait);
            bottomOfCards = position.y - cardHeight * 0.5f;
        }

        Vector2 backSize = portrait ? new Vector2(520f, 110f) : new Vector2(345f, 78f);
        Vector2 backPosition = portrait
            ? new Vector2(0f, -size.y * 0.5f + 130f)
            : new Vector2(-size.x * 0.5f + 50f + backSize.x * 0.5f, -size.y * 0.5f + 40f + backSize.y * 0.5f);
        LayoutButton(backButton, backPosition, backSize, portrait ? 50f : 40f);

        float messageY = portrait ? backPosition.y + 100f : bottomOfCards - 30f;
        PlaceText(levelMessage, width, portrait ? 80f : 50f, messageY, portrait ? 36f : 32f);
        levelMessage.textWrappingMode = TextWrappingModes.Normal;
    }

    // Everything inside one terminal-window card, placed from its top-left corner.
    private static void LayoutCard(LevelCard card, float w, float h, bool portrait)
    {
        float k = portrait ? 1f : w / 558f;      // landscape cards scale with their width
        float barHeight = 57f * k;
        SetTop(card.bar, 0f, barHeight, true);
        SetTop(card.barFill, barHeight * 0.5f, barHeight * 0.5f, true);
        for (int i = 0; i < card.dots.Length; i++)
        {
            PlaceTopLeft(card.dots[i], new Vector2(24f * k + i * 30f * k + 9f * k, -barHeight * 0.5f), new Vector2(18f * k, 18f * k), new Vector2(0.5f, 0.5f));
        }
        card.code.fontSize = 27f * k;
        PlaceTopRight(card.code.rectTransform, new Vector2(-24f * k, -barHeight * 0.5f), new Vector2(260f * k, barHeight));

        float mapTop = barHeight + 18f * k;
        float mapWidth = portrait ? 440f : w - 36f * k;
        float mapHeight = mapWidth * 0.75f;
        if (portrait)
        {
            PlaceTopLeft(card.mapBox, new Vector2(18f, -mapTop), new Vector2(mapWidth, mapHeight), new Vector2(0f, 1f));
        }
        else
        {
            card.mapBox.anchorMin = new Vector2(0f, 1f);
            card.mapBox.anchorMax = new Vector2(1f, 1f);
            card.mapBox.pivot = new Vector2(0.5f, 1f);
            card.mapBox.anchoredPosition = new Vector2(0f, -mapTop);
            card.mapBox.sizeDelta = new Vector2(-36f * k, mapHeight);
        }

        float textLeft = portrait ? 480f : 30f * k;
        float textWidth = portrait ? w - 480f - 24f : w - 60f * k;
        float nameY = portrait ? -(mapTop + 56f) : -(mapTop + mapHeight + 51f * k);
        float placeY = portrait ? nameY - 62f : nameY - 57f * k;
        float tagY = portrait ? placeY - 44f : placeY - 39f * k;
        float nameSize = portrait ? 68f : 75f * k;
        PlaceTopLeft(card.name.rectTransform, new Vector2(textLeft, nameY), new Vector2(textWidth - (portrait ? 0f : 170f * k), nameSize * 1.3f), new Vector2(0f, 0.5f));
        PlaceTopLeft(card.place.rectTransform, new Vector2(textLeft, placeY), new Vector2(textWidth, 44f * k), new Vector2(0f, 0.5f));
        PlaceTopLeft(card.tag.rectTransform, new Vector2(textLeft, tagY), new Vector2(textWidth, 40f * k), new Vector2(0f, 0.5f));
        card.name.fontSizeMax = nameSize;
        card.place.fontSizeMax = portrait ? 32f : 30f * k;
        card.tag.fontSizeMax = portrait ? 28f : 25f * k;

        float pip = 21f * k;
        float pipsY = portrait ? tagY - 52f : nameY;
        for (int i = 0; i < card.pips.Length; i++)
        {
            float x = portrait ? textLeft + pip * 0.5f + i * 30f : w - 36f * k - pip * 0.5f - (card.pips.Length - 1 - i) * 30f * k;
            PlaceTopLeft(card.pips[i], new Vector2(x, pipsY), new Vector2(pip, pip), new Vector2(0.5f, 0.5f));
        }
    }

    // A band across the top of a card (full width), top offset and height in pixels.
    private static void SetTop(RectTransform rect, float topOffset, float height, bool fullWidth)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -topOffset);
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void PlaceTopRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // The keycaps of a title in one row, centred, as big as fits (at most maxSize wide).
    private static void LayoutKeys(KeyTitle title, float centerY, float maxSize, float width)
    {
        float units = 0f;
        for (int i = 0; i < title.keys.Count; i++) units += title.gaps[i] ? 0.55f : 1f;
        units += (title.keys.Count - 1) * 0.1f;
        float size = Mathf.Min(maxSize, width / units);
        title.size = size;
        float x = -units * size * 0.5f;
        title.basePositions.Clear();
        for (int i = 0; i < title.keys.Count; i++)
        {
            float w = (title.gaps[i] ? 0.55f : 1f) * size;
            title.keys[i].localScale = Vector3.one * (size / 100f);
            Vector2 position = new Vector2(x + w * 0.5f, centerY);
            title.basePositions.Add(position);
            title.keys[i].anchoredPosition = position;
            x += w + size * 0.1f;
        }
    }

    private static void PlaceCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = Ui.Centre;
        rect.anchorMax = Ui.Centre;
        rect.pivot = Ui.Centre;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void PlaceText(TMP_Text text, float width, float height, float y, float fontSize)
    {
        PlaceCenter(text.rectTransform, new Vector2(0f, y), new Vector2(width, height));
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = fontSize;
    }

    private static void LayoutButton(RectTransform button, Vector2 position, Vector2 size, float fontSize)
    {
        PlaceCenter(button, position, size);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        label.fontSize = fontSize;
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
        levelPageOpenedAt = Time.unscaledTime;
        selected = 0;
        keyboardSelectionStarted = false;
        RefreshHighlight();
        AnimateCards();
    }

    // The Almanac: every enemy met so far (see Almanac, AlmanacPanel). It takes
    // the keys while it is open (HandleKeys), and closes itself on Esc.
    private void OpenAlmanac()
    {
        if (almanac == null)
        {
            almanac = AlmanacPanel.Create(canvasRect);
        }
        almanac.Open(null);
    }

    private GameObject BuildMainPage(Transform canvas)
    {
        GameObject page = CreatePage("Main Page", canvas);
        BuildCrowd(page.transform);

        // "THE TYPEBLASTER" on keycaps over "OF THE DEAD" on a red tape (the title is split at " of the ")
        string top = gameTitle;
        string bottom = "";
        int split = gameTitle.IndexOf(" of the ", System.StringComparison.OrdinalIgnoreCase);
        if (split > 0)
        {
            top = gameTitle.Substring(0, split);
            bottom = gameTitle.Substring(split + 1);
        }
        top = top.ToUpperInvariant();
        int blast = top.IndexOf("BLAST", System.StringComparison.Ordinal);   // these five keys are red
        mainKeys = CreateKeyTitle(page.transform, top, blast, blast < 0 ? 0 : 5);
        tape = CreateTape(page.transform, bottom.ToUpperInvariant());

        startButton = (RectTransform)CreateComicButton(page.transform, "Start Button", "START", ShowLevels).transform;
        almanacButton = (RectTransform)CreateTerminalButton(page.transform, "Almanac Button", "ALMANAC", OpenAlmanac).transform;
        BuildFullscreenControl(page.transform);
        SettingsPanel.AddButton((RectTransform)page.transform, new Vector2(-64f, -48f));

        footer = Ui.Label(page.transform, "Footer", "TEAM TYPEBLASTERS", 22f, FontStyles.Bold);
        MenuArt.Style(footer, 0f, Ink);
        footer.color = new Color(0.55f, 0.51f, 0.50f);
        Ui.Caps(footer, 8f);
        return page;
    }

    // The zombies of the title screen: a crowd in a blast ring at the bottom, a few with a word over their head, and a BOOM! sticker.
    private void BuildCrowd(Transform page)
    {
        crowd = Ui.Rect(page, "Crowd");
        crowd.anchorMin = new Vector2(0.5f, 0f);
        crowd.anchorMax = new Vector2(0.5f, 0f);
        crowd.pivot = new Vector2(0.5f, 0f);
        crowd.anchoredPosition = Vector2.zero;
        crowd.sizeDelta = Vector2.zero;

        Image ring = Ui.Panel(crowd, "Blast Ring", Color.white);
        ring.sprite = MenuArt.FloorRing;
        floorRing = ring.rectTransform;
        floorRing.anchorMin = new Vector2(0.5f, 0f);
        floorRing.anchorMax = new Vector2(0.5f, 0f);
        floorRing.pivot = Ui.Centre;
        floorRing.anchoredPosition = new Vector2(0f, 52f);
        floorRing.sizeDelta = new Vector2(900f, 225f);

        // x (from the middle), zombie kind, size, mirrored
        float[] xs = { 0f, -380f, -190f, 190f, 380f, 0f };
        int[] kinds = { 0, 2, 4, 3, 5, 6 };
        float[] sizes = { 150f, 168f, 150f, 150f, 168f, 150f };
        bool[] flips = { false, true, false, true, false, true };
        for (int i = 1; i < xs.Length - 1; i++) AddCrowdMember(xs[i], kinds[i], sizes[i], flips[i], 12f);
        AddCrowdMember(0f, 1, 225f, false, 0f);    // the red one in the middle: the twist


        boom = Ui.Rect(crowd, "Boom");
        boom.anchorMin = new Vector2(0.5f, 0f);
        boom.anchorMax = new Vector2(0.5f, 0f);
        boom.pivot = Ui.Centre;
        boom.anchoredPosition = new Vector2(765f, 195f);
        boom.sizeDelta = new Vector2(203f, 203f);
        Image burst = boom.gameObject.AddComponent<Image>();
        burst.sprite = MenuArt.Burst;
        burst.raycastTarget = false;
        TMP_Text boomText = Ui.Label(boom, "Text", "BOOM!", 44f, FontStyles.Bold);
        MenuArt.Style(boomText, 0.1f, Ink);
        boomText.color = Ink;
        Ui.Stretch(boomText.rectTransform);
        boom.localEulerAngles = new Vector3(0f, 0f, 10f);
        boom.gameObject.SetActive(false);   // too loud
    }

    private void AddCrowdMember(float x, int kind, float size, bool flip, float feetFromBottom)
    {
        Image body = Ui.Panel(crowd, "Zombie", Color.white);
        body.sprite = MenuArt.Zombie(kind);
        RectTransform rect = body.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(size, size);
        rect.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
        float y = feetFromBottom - 0.08f * size;     // the feet are 8% up the picture
        rect.anchoredPosition = new Vector2(x, y);
        crowdMembers.Add(new CrowdMember { rect = rect, baseY = y, phase = x * 0.01f });
    }

    // A word on a dark plate over a zombie, like in the game (the first letters typed are yellow).
    private void AddPlate(string richText, string plainText, float x, float yFromBottom, float fontSize)
    {
        Image plate = Ui.Panel(crowd, "Word Plate", new Color(0.03f, 0.04f, 0.08f, 0.82f));
        SetSliced(plate, 16);
        TMP_Text word = Ui.Label(plate.transform, "Word", richText, fontSize, FontStyles.Normal);
        MenuArt.Style(word, 0f, Ink);
        word.color = new Color(0.94f, 0.94f, 0.96f);
        word.richText = true;
        Ui.Stretch(word.rectTransform);
        RectTransform rect = plate.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = Ui.Centre;
        rect.anchoredPosition = new Vector2(x, yFromBottom);
        rect.sizeDelta = new Vector2(word.GetPreferredValues(plainText).x + 44f, fontSize * 1.7f);
    }

    // ---- The full-screen question ----
    // The first thing a player sees: "play in full screen?" over the whole menu.
    // Yes switches the browser to full screen (the same bridge as the FULL SCREEN
    // button, which needs the click or key press itself), No goes on in the
    // window. Either way the normal menu is underneath.
    private void ShowFullscreenGate()
    {
        if (fullscreenAsked || !TouchFullscreenButton.IsSupported() || TouchFullscreenButton.IsFullscreen)
        {
            return;
        }
        RectTransform gate = Ui.Rect(canvasRect, "Fullscreen Gate");
        Ui.Stretch(gate);
        gate.SetAsLastSibling();
        fullscreenGate = gate.gameObject;
        Image shade = Ui.Panel(gate, "Shade", backgroundColor);   // opaque: also takes every click
        Ui.Stretch(shade.rectTransform);

        gateTitle = Ui.Label(gate, "Title", "BEST IN FULL SCREEN", 84f, FontStyles.Bold);
        MenuArt.Style(gateTitle, 0f, Ink);
        gateTitle.color = new Color(0.96f, 0.94f, 0.90f);
        gateText = Ui.Label(gate, "Text", "The words are easier to read and nothing gets cut off.", 34f);
        MenuArt.Style(gateText, 0f, Ink);
        gateText.color = new Color(0.75f, 0.72f, 0.70f);

        Button yes = CreateTerminalButton(gate, "Go Fullscreen", "GO FULL SCREEN", CloseFullscreenGate);
        gateYes = (RectTransform)yes.transform;
#if UNITY_WEBGL && !UNITY_EDITOR
        // Pointer-down keeps the browser's gesture (see BuildFullscreenControl).
        yes.gameObject.AddComponent<TouchFullscreenButton>();
#endif
        gateNo = (RectTransform)CreateTerminalButton(gate, "Stay In Window", "STAY IN WINDOW", CloseFullscreenGate).transform;
        LayoutFullscreenGate();
    }

    private void LayoutFullscreenGate()
    {
        if (fullscreenGate == null) return;
        bool portrait = portraitLayout;
        float width = Mathf.Max(240f, canvasRect.rect.width - 120f);
        PlaceText(gateTitle, width, 130f, portrait ? 260f : 190f, portrait ? 76f : 84f);
        PlaceText(gateText, width, 90f, portrait ? 150f : 90f, portrait ? 36f : 34f);
        Vector2 size = portrait ? new Vector2(700f, 120f) : new Vector2(495f, 93f);
        float fontSize = portrait ? 48f : 40f;
        LayoutButton(gateYes, portrait ? new Vector2(0f, -40f) : new Vector2(-270f, -70f), size, fontSize);
        LayoutButton(gateNo, portrait ? new Vector2(0f, -190f) : new Vector2(270f, -70f), size, fontSize);
        SetHighlight(gateYes, gateSelected == 0);
        SetHighlight(gateNo, gateSelected == 1);
    }

    private void CloseFullscreenGate()
    {
        if (fullscreenGate == null) return;
        Destroy(fullscreenGate);
        fullscreenGate = null;
        gateClosedAt = Time.unscaledTime;
        RefreshHighlight();
    }

#if ENABLE_INPUT_SYSTEM
    // Enter = the selected answer, F = full screen, Esc = stay in the window, A D / arrows choose.
    private void HandleGateKeys(Keyboard keyboard)
    {
        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame
            || keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
        {
            gateSelected = 0;
        }
        else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame
            || keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
        {
            gateSelected = 1;
        }
        SetHighlight(gateYes, gateSelected == 0);
        SetHighlight(gateNo, gateSelected == 1);

        bool confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
            || keyboard.spaceKey.wasPressedThisFrame;
        if (keyboard.fKey.wasPressedThisFrame || (confirm && gateSelected == 0))
        {
            TouchFullscreenButton.ToggleFullscreen();
            CloseFullscreenGate();
        }
        else if (keyboard.escapeKey.wasPressedThisFrame || (confirm && gateSelected == 1))
        {
            CloseFullscreenGate();
        }
    }
#endif

    private void BuildFullscreenControl(Transform page)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!TouchFullscreenButton.IsSupported()) return;
#endif
        fullscreenHint = Ui.Label(page, "Fullscreen Hint", "", 28f);
        MenuArt.Style(fullscreenHint, 0f, Ink);
        fullscreenHint.color = new Color(0.75f, 0.72f, 0.70f);
        Button button = CreateTerminalButton(page, "Fullscreen Button", "FULL SCREEN", null);
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
        fullscreenLabel.text = Prompt + (fullscreen ? "EXIT FULL SCREEN" : "FULL SCREEN");
        fullscreenHint.text = fullscreen ? "Full screen is on" : "For the best view, play in full screen.";
    }

    private GameObject BuildLevelPage(Transform canvas)
    {
        GameObject page = CreatePage("Level Page", canvas);
        levelKeys = CreateKeyTitle(page.transform, "SELECT LEVEL", -1, 0);

        int count = GameManager.LevelCount;
        for (int i = 0; i < count; i++)
        {
            int levelIndex = i; // copy for the lambda
            int place = Mathf.Min(i, LevelPlaces.Length - 1);
            LevelCard card = CreateCard(page.transform, "Level " + GameManager.LevelName(i), i + 1,
                GameManager.LevelName(i).ToUpperInvariant(), LevelPlaces[place], LevelTags[place],
                LevelAccent[place], LevelDifficulty[place], place, () => LoadLevel(levelIndex));
            cards.Add(card);
            levelButtons.Add(card.root);
        }

        int best = EndlessRecords.BestWave;
        LevelCard endless = CreateCard(page.transform, "Level Endless", count + 1, "ENDLESS", "THE COLISEUM",
            best > 0 ? "BEST WAVE " + best : "SURVIVE THE HORDE", EndlessAccent, 5, 2, () => LoadScene(EndlessScene, endlessButton));
        cards.Add(endless);
        endlessButton = endless.root;

        levelMessage = Ui.Label(page.transform, "Message", "", 32f);
        MenuArt.Style(levelMessage, 0.15f, Ink);
        levelMessage.color = new Color(1f, 0.45f, 0.35f);

        backButton = (RectTransform)CreateTerminalButton(page.transform, "Back Button", "BACK", ShowMain).transform;
        return page;
    }

    private void LoadLevel(int index)
    {
        LoadScene(GameManager.LevelName(index), levelButtons[index]);
    }

    // Any scene by name (a level, or the Endless scene).
    private void LoadScene(string sceneName, RectTransform pressed)
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

        loading = true; // from now on no other button or key does anything

        StartCoroutine(PulseButton(pressed));
        ScreenFade.LoadSceneWithFade(sceneName, LaunchFadeStartSeconds);
    }

    // One gentle pulse: the button grows a little and comes back.
    private IEnumerator PulseButton(RectTransform button)
    {
        Vector3 rest = button.localScale;

        for (float time = 0f; time < LaunchPulseSeconds; time += Time.unscaledDeltaTime)
        {
            float grow = Mathf.Sin(time / LaunchPulseSeconds * Mathf.PI);
            button.localScale = rest * (1f + LaunchPulseGrowth * grow);
            yield return null;
        }

        button.localScale = rest;
    }

    // The buttons the keyboard can select on the page that is showing, in order
    // (the same order as on screen, see LayoutMainPage / LayoutLevelPage:
    // FULL SCREEN, where it exists, sits between START and ALMANAC).
    private List<RectTransform> CurrentItems()
    {
        menuItems.Clear();
        if (mainPage.activeSelf)
        {
            menuItems.Add(startButton);
            if (fullscreenButton != null) menuItems.Add(fullscreenButton);
            menuItems.Add(almanacButton);
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
        bool almanacBusy = (almanac != null && almanac.IsOpen) || almanacWasOpen;
        almanacWasOpen = almanac != null && almanac.IsOpen;
        if (almanacBusy)
        {
            return;
        }
        if (keyboard == null || loading || settingsBusy)
        {
            return;
        }
        // The full-screen question takes every key while it is up (and the key
        // that answered it must not also press START underneath).
        if (fullscreenGate != null)
        {
            HandleGateKeys(keyboard);
            return;
        }
        if (Time.unscaledTime - gateClosedAt < 0.15f)
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

    // The mouse moving over a button selects it, like the keys do.
    private void SelectByMouse(RectTransform button)
    {
        if (fullscreenGate != null)
        {
            if (button == gateYes) gateSelected = 0;
            else if (button == gateNo) gateSelected = 1;
            return;
        }
        if (loading || SettingsPanel.IsOpen || (almanac != null && almanac.IsOpen)) return;
        int index = CurrentItems().IndexOf(button);
        if (index < 0 || (index == selected && keyboardSelectionStarted)) return;
        selected = index;
        keyboardSelectionStarted = true;
        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        List<RectTransform> items = CurrentItems();
        selected = Mathf.Clamp(selected, 0, items.Count - 1);

        SetHighlight(startButton, false);
        SetHighlight(almanacButton, false);
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

    // ---- Small UI builders (the look of the menu) ----

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
        RectTransform rect = Ui.Rect(parent, pageName);
        Ui.Stretch(rect);
        return rect.gameObject;
    }

    private static void SetSliced(Image image, int radius)
    {
        image.sprite = MenuArt.RoundedBox(radius);
        image.type = Image.Type.Sliced;
    }

    // A rounded rectangle at a spot of its parent (from the parent's bottom-left corner).
    private static Image Cap(RectTransform parent, string objectName, Color color, float x, float y, float width, float height, int radius)
    {
        Image image = Ui.Panel(parent, objectName, color);
        SetSliced(image, radius);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        return image;
    }

    // A title spelled on keycaps. hotCount letters from hotStart are red. A space makes a gap.
    private KeyTitle CreateKeyTitle(Transform parent, string text, int hotStart, int hotCount)
    {
        KeyTitle title = new KeyTitle();
        title.group = Ui.Rect(parent, "Title");
        Ui.Stretch(title.group);
        for (int i = 0; i < text.Length; i++)
        {
            bool gap = text[i] == ' ';
            RectTransform key;
            if (gap)
            {
                key = Ui.Rect(title.group, "Space");
                key.anchorMin = Ui.Centre;
                key.anchorMax = Ui.Centre;
                key.sizeDelta = new Vector2(100f, 116f);
            }
            else
            {
                key = CreateKeycap(title.group, text[i].ToString(), i >= hotStart && i < hotStart + hotCount);
            }
            title.keys.Add(key);
            title.gaps.Add(gap);
        }
        return title;
    }

    // One keycap, 100 x 116 before scaling: a lip, a dark outline, a face and a letter. Tilted a little.
    private static RectTransform CreateKeycap(Transform parent, string letter, bool hot)
    {
        RectTransform root = Ui.Rect(parent, "Key " + letter);
        root.anchorMin = Ui.Centre;
        root.anchorMax = Ui.Centre;
        root.pivot = Ui.Centre;
        root.sizeDelta = new Vector2(100f, 116f);
        root.localEulerAngles = new Vector3(0f, 0f, Random.Range(-1f, 1f));

        Color lip = hot ? new Color(0.59f, 0.13f, 0.10f) : new Color(0.59f, 0.56f, 0.51f);
        Color face = hot ? new Color(0.93f, 0.28f, 0.20f) : Cream;
        Cap(root, "Lip", lip, 0f, 0f, 100f, 100f, 22);
        Cap(root, "Outline", Ink, 0f, 16f, 100f, 100f, 22);
        Image faceImage = Cap(root, "Face", face, 4f, 20f, 92f, 92f, 19);
        Cap(root, "Shine", new Color(1f, 1f, 1f, 0.16f), 12f, 28f, 76f, 76f, 15);

        TMP_Text label = Ui.Label(faceImage.transform, "Letter", letter, 66f, FontStyles.Bold);
        if (hot)
        {
            MenuArt.Style(label, 0.2f, Ink);
            label.color = Color.white;
        }
        else
        {
            MenuArt.Style(label, 0.05f, Ink);
            label.color = Ink;
        }
        Ui.Stretch(label.rectTransform);
        return root;
    }

    // The red torn tape with the second line of the title on it.
    private static RectTransform CreateTape(Transform parent, string text)
    {
        RectTransform root = Ui.Rect(parent, "Tape");
        Image image = root.gameObject.AddComponent<Image>();
        image.sprite = MenuArt.Tape;
        image.raycastTarget = false;
        TMP_Text label = Ui.Caps(Ui.Label(root, "Text", text, 84f, FontStyles.Bold), 10f);
        MenuArt.Style(label, 0.24f, Ink);
        label.color = Color.white;
        Ui.Stretch(label.rectTransform);
        return root;
    }

    // The START block: a slanted yellow comic button with a hard shadow.
    private Button CreateComicButton(Transform parent, string objectName, string text, UnityAction onClick)
    {
        RectTransform root = Ui.Rect(parent, objectName);
        root.anchorMin = Ui.Centre;
        root.anchorMax = Ui.Centre;

        // the glow that shows the keys selected it (behind the button; SetHighlight turns it on)
        Image glow = Ui.Panel(root, "Selection Frame", new Color(1f, 1f, 1f, 0.25f));
        glow.sprite = Ui.Glow;
        Ui.Stretch(glow.rectTransform, -90f, -70f, -90f, -70f);
        glow.gameObject.SetActive(false);

        Image face = Ui.Panel(root, "Face", Color.white, true);
        face.sprite = MenuArt.ComicButton;
        Ui.Stretch(face.rectTransform);

        TMP_Text label = Ui.Label(face.transform, "Text", text, 84f, FontStyles.Bold);
        MenuArt.Style(label, 0.12f, Ink);
        label.color = Ink;
        Ui.Caps(label, 6f);
        Ui.Stretch(label.rectTransform, 10f, 14f, 22f, 0f);

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        ColorBlock colors = button.colors;
        colors.normalColor = StartFace;
        colors.highlightedColor = StartHover;
        colors.pressedColor = StartPressed;
        colors.selectedColor = StartFace;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Sfx.HookButton(button);
        if (onClick != null) button.onClick.AddListener(onClick);

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        AddMouseSelection(root);
        return button;
    }

    // A terminal-style button: a pale outline, a dark face, amber corner marks and "> LABEL".
    private Button CreateTerminalButton(Transform parent, string objectName, string text, UnityAction onClick)
    {
        RectTransform root = Ui.Rect(parent, objectName);
        root.anchorMin = Ui.Centre;
        root.anchorMax = Ui.Centre;

        Image outline = Ui.Panel(root, "Outline", Cream);
        SetSliced(outline, 8);
        Ui.Stretch(outline.rectTransform);

        Image face = Ui.Panel(root, "Face", Color.white, true);
        SetSliced(face, 6);
        Ui.Stretch(face.rectTransform, 4f, 4f, 4f, 4f);

        for (int corner = 0; corner < 4; corner++)
        {
            float cx = corner % 2;
            float cy = corner / 2;
            Image across = Ui.Panel(root, "Mark", Amber);
            Ui.Place(across.rectTransform, new Vector2(cx, cy), new Vector2(cx, cy), Vector2.zero, new Vector2(22f, 6f));
            Image down = Ui.Panel(root, "Mark", Amber);
            Ui.Place(down.rectTransform, new Vector2(cx, cy), new Vector2(cx, cy), Vector2.zero, new Vector2(6f, 22f));
        }

        TMP_Text label = Ui.Label(face.transform, "Text", Prompt + text, 44f, FontStyles.Bold, TextAlignmentOptions.Left);
        MenuArt.Style(label, 0.1f, Ink);
        label.color = Cream;
        label.richText = true;
        Ui.Caps(label, 3f);
        Ui.Stretch(label.rectTransform, 34f, 0f, 12f, 0f);

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        ColorBlock colors = button.colors;
        colors.normalColor = TerminalFace;
        colors.highlightedColor = TerminalHover;
        colors.pressedColor = TerminalPressed;
        colors.selectedColor = TerminalFace;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Sfx.HookButton(button);
        if (onClick != null) button.onClick.AddListener(onClick);

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        AddSelectionFrame(root, 14);
        AddMouseSelection(root);
        return button;
    }

    // A level card made like a terminal window: a coloured title bar with three dots and "LEVEL_01",
    // the picture of the level, its name, place and a line about it, and the difficulty as squares.
    private LevelCard CreateCard(Transform parent, string objectName, int number, string levelName, string place, string tag,
        Color accent, int difficulty, int mapIndex, UnityAction onClick)
    {
        LevelCard card = new LevelCard();
        card.mapIndex = mapIndex;

        RectTransform root = Ui.Rect(parent, objectName);
        root.anchorMin = Ui.Centre;
        root.anchorMax = Ui.Centre;
        card.root = root;

        card.content = Ui.Rect(root, "Content");
        Ui.Stretch(card.content);
        card.group = card.content.gameObject.AddComponent<CanvasGroup>();

        Image shadow = Ui.Panel(card.content, "Shadow", new Color(0f, 0f, 0f, 0.43f));
        SetSliced(shadow, 10);
        Ui.Stretch(shadow.rectTransform, 10f, -12f, -10f, 12f);
        card.shadow = shadow.rectTransform;

        Image body = Ui.Panel(card.content, "Card", Color.white, true);
        SetSliced(body, 10);
        Ui.Stretch(body.rectTransform);

        Image bar = Ui.Panel(card.content, "Title Bar", accent);
        SetSliced(bar, 10);
        card.bar = bar.rectTransform;
        Image barFill = Ui.Panel(card.content, "Title Bar Fill", accent);   // squares off the bar's lower corners
        card.barFill = barFill.rectTransform;
        for (int i = 0; i < 3; i++)
        {
            Image dot = Ui.Panel(card.content, "Dot", new Color(0.08f, 0.055f, 0.095f, 0.86f));
            dot.sprite = MenuArt.Dot;
            card.dots[i] = dot.rectTransform;
        }
        card.code = Ui.Label(card.content, "Code", "LEVEL_" + number.ToString("00"), 27f, FontStyles.Bold, TextAlignmentOptions.Right);
        MenuArt.Style(card.code, 0f, Ink);
        card.code.color = new Color(0.08f, 0.055f, 0.095f);
        Ui.Caps(card.code, 3f);

        // the picture, with monitor lines and a thin frame
        card.mapBox = Ui.Rect(card.content, "Picture");
        card.mapBox.gameObject.AddComponent<RectMask2D>();
        card.map = Ui.Panel(card.mapBox, "Map", new Color(0.12f, 0.1f, 0.18f));
        Ui.Stretch(card.map.rectTransform);
        Image scan = Ui.Panel(card.mapBox, "Scanlines", new Color(1f, 1f, 1f, 0.2f));
        scan.sprite = MenuArt.Scanlines;
        scan.type = Image.Type.Tiled;
        scan.pixelsPerUnitMultiplier = 0.7f;
        Ui.Stretch(scan.rectTransform);
        Ui.Frame(card.mapBox, new Color(1f, 1f, 1f, 0.2f), 3f);

        // the words
        card.name = Ui.Label(card.content, "Name", levelName, 75f, FontStyles.Bold, TextAlignmentOptions.Left);
        MenuArt.Style(card.name, 0.1f, Ink);
        card.name.color = Cream;
        card.name.enableAutoSizing = true;
        card.name.fontSizeMin = 18f;
        card.place = Ui.Label(card.content, "Place", place, 30f, FontStyles.Bold, TextAlignmentOptions.Left);
        MenuArt.Style(card.place, 0.08f, Ink);
        card.place.color = accent;
        Ui.Caps(card.place, 6f);
        card.place.enableAutoSizing = true;
        card.place.fontSizeMin = 14f;
        card.tag = Ui.Label(card.content, "Tag", tag, 25f, FontStyles.Normal, TextAlignmentOptions.Left);
        MenuArt.Style(card.tag, 0f, Ink);
        card.tag.color = new Color(0.62f, 0.58f, 0.62f);
        Ui.Caps(card.tag, 2f);
        card.tag.enableAutoSizing = true;
        card.tag.fontSizeMin = 12f;

        // difficulty: five squares
        for (int i = 0; i < 5; i++)
        {
            Image pip = Ui.Panel(card.content, "Difficulty", i < difficulty ? accent : new Color(0.24f, 0.2f, 0.26f));
            card.pips[i] = pip.rectTransform;
        }

        Image rim = Ui.Panel(card.content, "Rim", accent);
        rim.sprite = MenuArt.RoundedOutline(10, 4);
        rim.type = Image.Type.Sliced;
        Ui.Stretch(rim.rectTransform);

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = body;
        ColorBlock colors = button.colors;
        colors.normalColor = CardFace;
        colors.highlightedColor = CardHover;
        colors.pressedColor = CardPressed;
        colors.selectedColor = CardFace;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Sfx.HookButton(button);
        button.onClick.AddListener(onClick);
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        AddSelectionFrame(root, 10);
        AddMouseSelection(root);
        return card;
    }

    // A yellow rounded frame around the button, hidden until it is selected.
    private static void AddSelectionFrame(RectTransform button, int radius)
    {
        Image frame = Ui.Panel(button, "Selection Frame", FrameGold);
        frame.sprite = MenuArt.RoundedOutline(radius + 6, 6);
        frame.type = Image.Type.Sliced;
        Ui.Stretch(frame.rectTransform, -8f, -8f, -8f, -8f);
        frame.gameObject.SetActive(false);
    }

    private void AddMouseSelection(RectTransform button)
    {
        button.gameObject.AddComponent<MenuHoverRelay>().onEnter = () => SelectByMouse(button);
    }

    public static void RequestLevelSelection()
    {
        openLevelSelectionOnStart = true;
    }
}
