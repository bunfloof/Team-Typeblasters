// StartPanelStyle.cs
// ---------------------------------------------------------------------------
// Gives the Start panel that every level opens on (Normal, Hard, Endless) the
// same look as the main menu (see MainMenu, MenuArt): a cream title with a dark
// outline and a red shadow, a short line about the level, the slanted yellow
// START block, a rounded "Show tips" box, rounded typing-difficulty buttons and
// a terminal-style MENU button.
//
// It only changes how things LOOK. The objects, their positions and their
// scripts stay the ones saved in the scene (and the Typing Difficulty /
// Menu Button prefabs), so nothing about the levels changes. HUD.ShowStartPanel
// calls Apply once; calling it again does nothing.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartPanelStyle : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.03f, 0.04f, 0.08f);
    private static readonly Color StartFace = new Color(0.93f, 0.91f, 0.87f);
    private static readonly Color StartHover = new Color(1f, 1f, 1f);
    private static readonly Color StartPressed = new Color(0.72f, 0.70f, 0.66f);
    private static readonly Color TerminalFace = new Color(0.07f, 0.055f, 0.10f, 0.94f);
    private static readonly Color TerminalHover = new Color(0.17f, 0.13f, 0.25f, 1f);
    private static readonly Color TerminalPressed = new Color(0.04f, 0.03f, 0.06f, 1f);
    private static readonly Color Cream = new Color(0.93f, 0.91f, 0.87f);
    private static readonly Color Amber = new Color(0.93f, 0.91f, 0.87f);
    private static readonly Color TitleRed = new Color(0.87f, 0.15f, 0.16f);

    public static void Apply(GameObject panel)
    {
        if (panel == null || panel.GetComponent<StartPanelStyle>() != null)
        {
            return;
        }
        panel.AddComponent<StartPanelStyle>();
        Transform root = panel.transform;

        StyleTitle(root.Find("Title"));

        Transform start = root.Find("Button");
        if (start != null) StyleButton(start.GetComponent<Button>(), true);

        Transform hint = root.Find("Enter Hint");
        if (hint != null)
        {
            TMP_Text hintText = hint.GetComponent<TMP_Text>();
            if (hintText != null)
            {
                MenuArt.Style(hintText, 0.12f, Ink);
                hintText.color = new Color(0.72f, 0.80f, 0.93f);
            }
        }

        StyleToggle(root.Find("Tips Toggle"));

        TypingDifficultySelector difficulty = panel.GetComponentInChildren<TypingDifficultySelector>(true);
        if (difficulty != null) StyleDifficulty(difficulty);

        MenuButton menu = panel.GetComponentInChildren<MenuButton>(true);
        if (menu != null)
        {
            Button menuButton = menu.GetComponent<Button>();
            if (menuButton == null) menuButton = menu.GetComponentInChildren<Button>(true);
            if (menuButton == null) menuButton = menu.GetComponentInParent<Button>();
            StyleButton(menuButton, false);
        }
    }

    // ---- The title: the level's name in gold, with a line about the level under it ----

    private static void StyleTitle(Transform titleTransform)
    {
        if (titleTransform == null) return;
        TMP_Text title = titleTransform.GetComponent<TMP_Text>();
        if (title == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        string tag = LevelLine(sceneName);

        title.text = title.text.ToUpperInvariant();
        title.fontStyle = FontStyles.Bold;
        title.fontSize = 96f;
        title.enableAutoSizing = true;
        title.fontSizeMin = 36f;
        title.fontSizeMax = 96f;
        title.characterSpacing = 4f;
        MenuArt.Style(title, 0.24f, Ink);
        title.color = Cream;

        // a dark copy a little lower and to the right, behind it
        GameObject shadowObject = Instantiate(title.gameObject, titleTransform.parent);
        shadowObject.name = "Title Shadow";
        TMP_Text shadow = shadowObject.GetComponent<TMP_Text>();
        shadow.color = TitleRed;
        MenuArt.Style(shadow, 0.32f, Ink);
        RectTransform shadowRect = shadow.rectTransform;
        shadowRect.anchoredPosition = ((RectTransform)titleTransform).anchoredPosition + new Vector2(8f, -9f);
        shadowRect.SetSiblingIndex(titleTransform.GetSiblingIndex());

        // the line about the level, under the title
        if (!string.IsNullOrEmpty(tag))
        {
            RectTransform titleRect = (RectTransform)titleTransform;
            TMP_Text line = Ui.Caps(Ui.Label(titleTransform.parent, "Level Line", tag, 34f, FontStyles.Bold), 6f);
            MenuArt.Style(line, 0.12f, Ink);
            line.color = LevelColor(sceneName);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = titleRect.anchorMin;
            rect.anchorMax = titleRect.anchorMax;
            rect.pivot = titleRect.pivot;
            rect.sizeDelta = new Vector2(titleRect.sizeDelta.x, 50f);
            rect.anchoredPosition = titleRect.anchoredPosition + new Vector2(0f, -titleRect.sizeDelta.y * 0.5f - 22f);
        }
    }

    private static string LevelLine(string sceneName)
    {
        if (sceneName == "Normal") return "LEVEL 1  /  3 WAVES + BOSS";
        if (sceneName == "Hard") return "LEVEL 2  /  FAST ZOMBIES, LONG WORDS";
        if (sceneName == "Endless")
        {
            int best = EndlessRecords.BestWave;
            return best > 0 ? "SURVIVAL  /  BEST WAVE " + best : "SURVIVAL";
        }
        return "";
    }

    private static Color LevelColor(string sceneName)
    {
        if (sceneName == "Hard") return new Color(1f, 0.58f, 0.25f);
        if (sceneName == "Endless") return new Color(0.82f, 0.45f, 1f);
        return Ui.Teal;
    }

    // ---- Buttons ----

    // START: the slanted yellow comic block. MENU: a terminal button (pale outline, dark face).
    private static void StyleButton(Button button, bool primary)
    {
        if (button == null) return;
        Image face = button.targetGraphic as Image;
        if (face == null) face = button.GetComponent<Image>();
        if (face == null) return;

        ColorBlock colors = button.colors;
        colors.fadeDuration = 0.08f;
        if (primary)
        {
            face.sprite = MenuArt.ComicButton;
            face.type = Image.Type.Simple;
            face.preserveAspect = false;
            face.color = Color.white;
            colors.normalColor = StartFace;
            colors.highlightedColor = StartHover;
            colors.pressedColor = StartPressed;
            colors.selectedColor = StartFace;
        }
        else
        {
            AddBackdrop((RectTransform)button.transform, Cream, 4f, 8);
            face.sprite = MenuArt.RoundedBox(6);
            face.type = Image.Type.Sliced;
            face.color = Color.white;
            colors.normalColor = TerminalFace;
            colors.highlightedColor = TerminalHover;
            colors.pressedColor = TerminalPressed;
            colors.selectedColor = TerminalFace;
        }
        button.colors = colors;

        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.fontStyle = FontStyles.Bold;
            MenuArt.Style(label, primary ? 0.12f : 0.1f, Ink);
            label.color = primary ? Ink : Cream;
        }
    }

    // ---- "Show tips" ----

    private static void StyleToggle(Transform toggleTransform)
    {
        if (toggleTransform == null) return;
        Toggle toggle = toggleTransform.GetComponent<Toggle>();
        Image box = toggle != null ? toggle.targetGraphic as Image : null;
        if (box != null)
        {
            AddBackdrop((RectTransform)box.transform, Cream, 3f, 14);
            box.sprite = MenuArt.RoundedBox(10);
            box.type = Image.Type.Sliced;
            box.color = Color.white;
            ColorBlock colors = toggle.colors;
            colors.normalColor = TerminalFace;
            colors.highlightedColor = TerminalHover;
            colors.pressedColor = TerminalPressed;
            colors.selectedColor = TerminalFace;
            toggle.colors = colors;
        }
        Image check = toggle != null ? toggle.graphic as Image : null;
        if (check != null)
        {
            check.sprite = MenuArt.RoundedBox(6);
            check.type = Image.Type.Sliced;
            check.color = Amber;
            check.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            check.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            check.rectTransform.sizeDelta = new Vector2(18f, 18f);
            check.rectTransform.anchoredPosition = Vector2.zero;
        }
        foreach (TMP_Text label in toggleTransform.GetComponentsInChildren<TMP_Text>(true))
        {
            label.fontStyle = FontStyles.Bold;
            MenuArt.Style(label, 0.12f, Ink);
            label.color = new Color(0.86f, 0.90f, 0.97f);
        }
    }

    // ---- Typing difficulty (EASY, MEDIUM, HARD, EXTREME) ----
    // Only the shapes and the font change: TypingDifficultySelector still sets the colours.

    private static void StyleDifficulty(TypingDifficultySelector selector)
    {
        foreach (Image image in selector.GetComponentsInChildren<Image>(true))
        {
            bool isButton = image.GetComponent<Button>() != null;
            image.sprite = MenuArt.RoundedBox(isButton ? 16 : 22);
            image.type = Image.Type.Sliced;
        }
        foreach (TMP_Text label in selector.GetComponentsInChildren<TMP_Text>(true))
        {
            label.fontStyle = FontStyles.Bold;
            MenuArt.Style(label, 0.1f, Ink);
        }
        selector.Refresh();
    }

    // A rounded shape just behind a UI element and a little bigger than it (the outline).
    private static void AddBackdrop(RectTransform target, Color color, float grow, int radius)
    {
        RectTransform parent = target.parent as RectTransform;
        if (parent == null) return;
        Image back = Ui.Panel(parent, target.name + " Outline", color);
        back.sprite = MenuArt.RoundedBox(radius);
        back.type = Image.Type.Sliced;
        RectTransform rect = back.rectTransform;
        rect.anchorMin = target.anchorMin;
        rect.anchorMax = target.anchorMax;
        rect.pivot = target.pivot;
        rect.anchoredPosition = target.anchoredPosition;
        rect.sizeDelta = target.sizeDelta + new Vector2(grow * 2f, grow * 2f);
        rect.localScale = target.localScale;
        rect.SetSiblingIndex(target.GetSiblingIndex());
    }
}
