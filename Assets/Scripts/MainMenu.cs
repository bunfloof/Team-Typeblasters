// MainMenu.cs
// ---------------------------------------------------------------------------
// The menu scene ("Menu.unity"). Builds its whole UI in code (like HUD does), so
// the scene only needs: a camera, an EventSystem and ONE object with this script.
//
//   MAIN PAGE:   title, a CHARACTER slot (placeholder, not implemented yet) and
//                a START button at the bottom that opens the level page.
//   LEVEL PAGE:  one button per level (the list comes from GameManager.LevelName,
//                so there is a single list of levels) and a BACK button.
//
// Clicking a level loads that scene by name. The scene must be in Build Settings.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        GameObject dim = CreatePage("Dim", canvas);
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = dimColor;
        dimImage.raycastTarget = false;
        mainPage = BuildMainPage(canvas);
        levelPage = BuildLevelPage(canvas);
        ShowMain();
    }

    // ---- Pages ----

    private void ShowMain()
    {
        mainPage.SetActive(true);
        levelPage.SetActive(false);
    }

    private void ShowLevels()
    {
        levelMessage.text = "";
        mainPage.SetActive(false);
        levelPage.SetActive(true);
    }

    private GameObject BuildMainPage(Transform canvas)
    {
        GameObject page = CreatePage("Main Page", canvas);
        CreateText(page.transform, "Title", gameTitle, 84f, new Vector2(0f, 300f), new Vector2(1700f, 120f));
        CreateButton(page.transform, "Start Button", "START", new Vector2(0f, 0f), new Vector2(360f, 100f), ShowLevels);
        return page;
    }

    private GameObject BuildLevelPage(Transform canvas)
    {
        GameObject page = CreatePage("Level Page", canvas);
        CreateText(page.transform, "Title", "SELECT LEVEL", 72f, new Vector2(0f, 300f), new Vector2(1700f, 110f));

        int count = GameManager.LevelCount;
        for (int i = 0; i < count; i++)
        {
            int levelIndex = i; // copy for the lambda
            string label = (i + 1) + ". " + GameManager.LevelName(i);
            float x = (i - (count - 1) * 0.5f) * 400f;
            CreateButton(page.transform, "Level " + GameManager.LevelName(i), label,
                new Vector2(x, 20f), new Vector2(360f, 180f), () => LoadLevel(levelIndex));
        }

        levelMessage = CreateText(page.transform, "Message", "", 32f, new Vector2(0f, -140f), new Vector2(1700f, 50f));
        levelMessage.color = new Color(1f, 0.4f, 0.3f);

        CreateButton(page.transform, "Back Button", "BACK", new Vector2(0f, -300f), new Vector2(360f, 100f), ShowMain);
        return page;
    }

    private void LoadLevel(int index)
    {
        if (loading)
        {
            return;
        }
        string sceneName = GameManager.LevelName(index);
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("MainMenu: scene \"" + sceneName + "\" is missing from Build Settings.");
            levelMessage.text = sceneName + " is not available yet.";
            return;
        }
        loading = true;
        SceneManager.LoadScene(sceneName);
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
        return button;
    }
}
