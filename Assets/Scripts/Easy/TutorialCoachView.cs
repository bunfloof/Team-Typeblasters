using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Minimal cues and a touchable key, rather than a panel of instructions.
public class TutorialCoachView : MonoBehaviour
{
    public bool DemoPressed;
    private EasyTutorial owner;
    private HUD hud;
    private RectTransform root, keyCard, pointer;
    private TMP_Text title, action, keyText, energyCost;
    private Image keyFace, fill;
    private readonly Image[] dots = new Image[6];
    private Button replay;
    private string key = "";
    private Key? pressedCode;
    private bool demo, complete;
    private readonly LineRenderer[] pullLines = new LineRenderer[3];
    private readonly Transform[] pullDots = new Transform[3];
    private Material lineMaterial;
    private static readonly Color Teal = new Color(.25f, .86f, .8f);
    private static readonly Color Gold = new Color(1f, .68f, .3f);
    private static readonly Color WaitColor = new Color(1f, .85f, .45f); // a friendly light gold, not an error red

    // HOW IT WORKS (the demo) vs YOUR TURN must never look alike: during a
    // demo the key card is greyed out with a DEMO tag and a progress bar, and
    // pressing the key gently says "your turn is next". When the demo ends,
    // YOUR TURN pops in and the key card lights up.
    private const float WaitSeconds = 1.5f;   // how long "your turn is next" stays after an early press
    private const float YourTurnPopSeconds = 0.5f;
    private CanvasGroup keyGroup;
    private TMP_Text plusSign;                // [1] + arrows: they are used together
    private RectTransform letterTop, letterBottom; // cinema bars: shown during a demo ("this is a show, not your turn")
    private const float LetterboxHeight = 72f;
    private const float LetterboxSpeed = 360f;  // canvas units per second
    private float holdStart = -1f;              // when the lure key started being held (demo or player), -1 = not held
    private const float DemoCardScale = .78f;   // the key card is smaller and grey during a demo
    private static readonly Color DemoFace = new Color(.16f, .17f, .19f, .97f);
    private static readonly Color DemoFacePressed = new Color(.3f, .32f, .35f, .97f);
    private static readonly Color DemoKeyText = new Color(.62f, .64f, .68f);
    private TMP_Text demoTag;
    private RectTransform demoBar, demoFill;
    private float demoStart, demoLength = 1f;
    private float waitAt = -100f, yourTurnAt = -100f;
    private string actionText = "";   // the instruction (shown again once the early-press note is over)
    private Color actionColor = Teal;

    // The arrow keys (lure lesson): left, right, up, down. They light up while
    // the demo steers the throw, and while the player aims.
    private RectTransform arrowKeys;
    private readonly Image[] arrowCaps = new Image[4];
    private Vector2 demoArrows;
    private static readonly Color CapIdle = new Color(.08f, .17f, .2f, .97f);

    public static TutorialCoachView Create(EasyTutorial owner, HUD hud)
    {
        var go = new GameObject("Tutorial Coach", typeof(RectTransform));
        var view = go.AddComponent<TutorialCoachView>();
        view.owner = owner; view.hud = hud; view.root = (RectTransform)go.transform;
        view.root.SetParent(hud.WordLayer.parent, false);
        view.root.anchorMin = Vector2.zero; view.root.anchorMax = Vector2.one;
        view.root.offsetMin = view.root.offsetMax = Vector2.zero;
        view.Build();
        return view;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private static Image Plate(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }

    private static TMP_Text Text(string name, Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, float font)
    {
        RectTransform rect = Rect(name, parent, anchor, position, size);
        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text; label.fontSize = font; label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = font * .7f; label.fontSizeMax = font;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
        return label;
    }

    private void Build()
    {
        // Cinema bars first, so everything else is drawn over them.
        letterTop = Letterbox("Letterbox Top", 1f);
        letterBottom = Letterbox("Letterbox Bottom", 0f);
        for (int i = 0; i < dots.Length; i++)
            dots[i] = Plate(Rect("Step " + (i + 1), root, new Vector2(.5f, 1f), new Vector2((i - 2.5f) * 28f, -26f), Vector2.one * 12f), new Color(.2f,.27f,.3f));
        title = Text("Cue", root, "", new Vector2(.5f,1f), new Vector2(0,-83), new Vector2(1000,80), 52);
        title.fontStyle = FontStyles.Bold;
        action = Text("Action", root, "", new Vector2(.5f,1f), new Vector2(0,-143), new Vector2(900,54), 34);
        keyCard = Rect("Practice Key", root, new Vector2(.5f,0f), new Vector2(0,210), new Vector2(240,160));
        keyFace = Plate(keyCard, new Color(.08f,.17f,.2f,.97f)); keyFace.raycastTarget = true;
        Plate(Rect("Key Base", keyCard, new Vector2(.5f,.5f), new Vector2(0,-76), new Vector2(240,8)), new Color(.015f,.04f,.06f));
        keyText = Text("Key", keyCard, "", new Vector2(.5f,.5f), new Vector2(0,6), new Vector2(220,110), 72);
        fill = Plate(Rect("Hold Fill", keyCard, new Vector2(0,0), new Vector2(0,5), new Vector2(240,8)), Teal);
        fill.rectTransform.pivot = new Vector2(0,.5f);
        energyCost = Text("Energy Cost",root,"",new Vector2(.5f,0),new Vector2(0,65),new Vector2(750,60),38);
        var input = keyCard.gameObject.AddComponent<TutorialPracticeKey>(); input.View = this;
        keyGroup = keyCard.gameObject.AddComponent<CanvasGroup>();
        demoTag = Text("Demo Tag", root, "DEMO", new Vector2(.5f,0f), new Vector2(0,312), new Vector2(240,36), 28);
        demoTag.color = Gold; demoTag.fontStyle = FontStyles.Bold;
        demoBar = Rect("Demo Progress", root, new Vector2(.5f,0f), new Vector2(0,118), new Vector2(240,10));
        Plate(demoBar, new Color(.015f,.04f,.06f,.9f));
        demoFill = Rect("Fill", demoBar, new Vector2(0f,.5f), Vector2.zero, new Vector2(0,10));
        demoFill.pivot = new Vector2(0f,.5f);
        Plate(demoFill, Gold);
        BuildArrowKeys();
        plusSign = Text("Plus", root, "+", new Vector2(.5f,0f), new Vector2(-155,210), new Vector2(60,60), 56);
        plusSign.gameObject.SetActive(false);
        RectTransform replayRect = Rect("Replay Demo", root, new Vector2(1,0), new Vector2(-150,210), new Vector2(210,100));
        Image replayFace = Plate(replayRect, new Color(.06f,.12f,.16f,.95f)); replayFace.raycastTarget = true;
        replay = replayRect.gameObject.AddComponent<Button>(); replay.targetGraphic = replayFace;
        replay.navigation = new Navigation { mode = Navigation.Mode.None };
        replay.onClick.AddListener(owner.ReplayDemo);
        Text("Replay", replayRect, "REPLAY", new Vector2(.5f,.5f), Vector2.zero, new Vector2(195,75), 25);
        pointer = Rect("Target Arrow", root, new Vector2(.5f,.5f), Vector2.zero, new Vector2(40,60));
        Plate(Rect("Stem", pointer, new Vector2(.5f,.5f), new Vector2(0,10), new Vector2(5,33)), Teal);
        var arm = Rect("Left", pointer, new Vector2(.5f,.5f), new Vector2(-6,-6), new Vector2(5,21)); arm.localRotation = Quaternion.Euler(0,0,45); Plate(arm,Teal);
        arm = Rect("Right", pointer, new Vector2(.5f,.5f), new Vector2(6,-6), new Vector2(5,21)); arm.localRotation = Quaternion.Euler(0,0,-45); Plate(arm,Teal);
    }

    public void Cue(string heading, string instruction, string shownKey, bool demonstrating)
    {
        if (demo && !demonstrating && shownKey.Length > 0)
        {
            yourTurnAt = Time.unscaledTime; // the demo just ended: YOUR TURN pops in
        }
        title.text = heading; action.text = instruction; keyText.text = shownKey;
        key = shownKey; demo = demonstrating;
        title.color = demonstrating ? Gold : Color.white;
        action.color = demonstrating ? Gold : Teal;
        actionText = instruction;
        actionColor = action.color;
        keyCard.gameObject.SetActive(shownKey.Length > 0);
        keyGroup.alpha = demonstrating ? .7f : 1f;  // a little faded (it is also grey and smaller): not yours to press yet
        demoTag.gameObject.SetActive(demonstrating && shownKey.Length > 0);
        demoBar.gameObject.SetActive(demonstrating);
        replay.gameObject.SetActive(!demonstrating && owner.CurrentLesson >= 3);
    }

    // A full-width black bar at the top (edge 1) or bottom (edge 0); its height animates.
    private RectTransform Letterbox(string name, float edge)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = new Vector2(0f, edge);
        rect.anchorMax = new Vector2(1f, edge);
        rect.pivot = new Vector2(.5f, edge);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 0f);
        Plate(rect, new Color(0f, 0f, 0f, .88f));
        return rect;
    }

    // A demo starts: its progress bar fills over about this many seconds.
    public void StartDemoTimer(float seconds)
    {
        demoStart = Time.unscaledTime;
        demoLength = Mathf.Max(.5f, seconds);
    }

    // The demo is steering the lure bomb with these arrows (zero = none).
    public void ShowDemoArrows(Vector2 arrows)
    {
        demoArrows = arrows;
    }

    // Four small key caps, an arrow drawn on each (no font glyph needed).
    private void BuildArrowKeys()
    {
        arrowKeys = Rect("Arrow Keys", root, new Vector2(.5f,0f), new Vector2(-270,200), new Vector2(160,110)); // left of the key card: the aim hint is bottom-right
        Vector2[] spots = { new Vector2(-54,-26), new Vector2(54,-26), new Vector2(0,28), new Vector2(0,-26) };
        float[] turns = { -90f, 90f, 180f, 0f }; // the drawn arrow points down; turned for left, right, up
        for (int i = 0; i < 4; i++)
        {
            RectTransform cap = Rect("Arrow Cap", arrowKeys, new Vector2(.5f,.5f), spots[i], new Vector2(48,48));
            arrowCaps[i] = Plate(cap, CapIdle);
            RectTransform glyph = Rect("Arrow", cap, new Vector2(.5f,.5f), Vector2.zero, new Vector2(40,60));
            glyph.localScale = Vector3.one * .55f;
            glyph.localRotation = Quaternion.Euler(0,0,turns[i]);
            Plate(Rect("Stem", glyph, new Vector2(.5f,.5f), new Vector2(0,10), new Vector2(6,33)), Color.white);
            var a = Rect("Left", glyph, new Vector2(.5f,.5f), new Vector2(-6,-6), new Vector2(6,21)); a.localRotation = Quaternion.Euler(0,0,45); Plate(a, Color.white);
            a = Rect("Right", glyph, new Vector2(.5f,.5f), new Vector2(6,-6), new Vector2(6,21)); a.localRotation = Quaternion.Euler(0,0,-45); Plate(a, Color.white);
        }
        arrowKeys.gameObject.SetActive(false);
    }

    private void UpdateArrowKeys()
    {
        // Shown in the lure lesson while the bomb is aimed: by the demo, or by
        // the player on a keyboard (a phone aims by dragging).
        bool demoAiming = demo && DemoPressed && owner.CurrentLesson == 3;
        bool playerAiming = !demo && owner.CurrentLesson == 3 && GameManager.Instance.Powers.IsAiming && !TouchControls.Active;
        arrowKeys.gameObject.SetActive(demoAiming || playerAiming);
        plusSign.gameObject.SetActive(demoAiming || playerAiming);
        if (!arrowKeys.gameObject.activeSelf) return;
        Vector2 arrows = demoArrows;
        if (playerAiming)
        {
            Keyboard board = Keyboard.current;
            arrows = board == null ? Vector2.zero : new Vector2(
                (board.rightArrowKey.isPressed ? 1f : 0f) - (board.leftArrowKey.isPressed ? 1f : 0f),
                (board.upArrowKey.isPressed ? 1f : 0f) - (board.downArrowKey.isPressed ? 1f : 0f));
        }
        float breathe = .5f + .5f * Mathf.Sin(Time.unscaledTime * 4f);
        Color idle = Color.Lerp(CapIdle, new Color(.13f,.3f,.32f), breathe); // they breathe: "use us"
        bool[] lit = { arrows.x < 0f, arrows.x > 0f, arrows.y > 0f, arrows.y < 0f };
        for (int i = 0; i < 4; i++)
        {
            arrowCaps[i].color = lit[i] ? Gold : idle;
            arrowCaps[i].rectTransform.localScale = Vector3.one * (lit[i] ? 1.25f : 1f); // the key being pressed stands out
        }
    }

    // The key on the card went down this frame (keyboard).
    private bool KeyWentDown()
    {
        Keyboard board = Keyboard.current;
        if (board == null || key.Length == 0) return false;
        return key == "TAB" ? board.tabKey.wasPressedThisFrame : key == "1" ? board.digit1Key.wasPressedThisFrame || board.numpad1Key.wasPressedThisFrame
            : key == "2" ? board.digit2Key.wasPressedThisFrame || board.numpad2Key.wasPressedThisFrame
            : key == "3" ? board.digit3Key.wasPressedThisFrame || board.numpad3Key.wasPressedThisFrame
            : board.digit4Key.wasPressedThisFrame || board.numpad4Key.wasPressedThisFrame;
    }

    public void Success(int lesson)
    {
        dots[lesson - 1].color = Teal;
        Cue("NICE!", "", "", false);
        replay.gameObject.SetActive(false);
    }

    public void Press(bool down)
    {
        if (!down)
        {
            if (pressedCode.HasValue) TouchInput.Release(pressedCode.Value);
            pressedCode = null;
            return;
        }
        if (demo) { waitAt = Time.unscaledTime; return; } // tapped during the demo: "your turn is next"
        if (complete || key.Length == 0) return;
        Key code = key == "TAB" ? Key.Tab : key == "1" ? Key.Digit1 : key == "2" ? Key.Digit2 : key == "3" ? Key.Digit3 : Key.Digit4;
        if (key == "2") { if (down) TouchInput.Type('2'); return; }
        pressedCode = code;
        TouchInput.Press(code);
    }

    private bool Pressed()
    {
        Keyboard board = Keyboard.current;
        if (DemoPressed) return true;
        if (board == null || key.Length == 0) return false;
        return key == "TAB" ? board.tabKey.isPressed : key == "1" ? board.digit1Key.isPressed || board.numpad1Key.isPressed
            : key == "2" ? board.digit2Key.isPressed || board.numpad2Key.isPressed
            : key == "3" ? board.digit3Key.isPressed || board.numpad3Key.isPressed : board.digit4Key.isPressed || board.numpad4Key.isPressed;
    }

    private void LateUpdate()
    {
        TouchInput.Update(); // also release single-frame mouse taps on the desktop key card
        if (owner == null || complete) return;
        float mobile = TouchControls.Active ? 1.35f : 1f;
        float now = Time.unscaledTime;

        // Pressed during the demo: a friendly "your turn is next", and the card wiggles.
        if (demo && !DemoPressed && KeyWentDown()) waitAt = now;
        float wait = demo ? Mathf.Clamp01(1f - (now - waitAt) / WaitSeconds) : 0f;
        if (wait > 0f)
        {
            action.text = "DEMO PLAYING - YOUR TURN IS NEXT";
            action.color = WaitColor;
        }
        else if (action.text != actionText)
        {
            action.text = actionText; // the early-press note is over: the instruction comes back
            action.color = actionColor;
        }
        keyCard.anchoredPosition = new Vector2(Mathf.Sin(now * 60f) * 12f * wait, 210f);

        // The demo's progress bar.
        if (demo) demoFill.sizeDelta = new Vector2(240f * Mathf.Clamp01((now - demoStart) / demoLength), 10f);

        // Cinema bars slide in for a demo and away for YOUR TURN.
        float barHeight = Mathf.MoveTowards(letterTop.sizeDelta.y, demo ? LetterboxHeight : 0f, LetterboxSpeed * Time.unscaledDeltaTime);
        letterTop.sizeDelta = new Vector2(0f, barHeight);
        letterBottom.sizeDelta = new Vector2(0f, barHeight);

        // The lure key being HELD (by the demo or the player): a bar fills on its card.
        bool holdingLure = owner.CurrentLesson == 3 && key == "1" && Pressed();
        if (!holdingLure) holdStart = -1f;
        else if (holdStart < 0f) holdStart = now;

        // YOUR TURN: the title and the key card pop when the demo hands over.
        float pop = Mathf.Clamp01((now - yourTurnAt) / YourTurnPopSeconds);
        float grow = pop < 1f ? Mathf.Sin(pop * Mathf.PI) * .35f : 0f;
        title.rectTransform.localScale = Vector3.one * (1f + grow);
        keyCard.localScale = Vector3.one * mobile * (Pressed() ? .93f : 1f) * (1f + grow) * (demo ? DemoCardScale : 1f);
        UpdateArrowKeys();
        bool effect = owner.CurrentLesson == 2 ? Powers.IsShielded : owner.CurrentLesson == 4 ? Powers.IsFrozen
            : owner.CurrentLesson == 5 ? Powers.IsFrenzy : owner.CurrentLesson == 6 && GameManager.Instance.Powers.RocketLoaded;
        // A demo's card is flat grey (even when the demo presses it): it is not
        // the player's to press. On YOUR TURN it breathes in teal.
        if (demo)
        {
            keyFace.color = Pressed() ? DemoFacePressed : DemoFace;
            keyText.color = DemoKeyText;
        }
        else
        {
            keyFace.color = effect || Pressed() ? new Color(.14f,.49f,.47f) : Color.Lerp(new Color(.05f,.13f,.18f), new Color(.13f,.3f,.32f), .5f + .5f * Mathf.Sin(Time.time * 3f));
            keyText.color = Color.white;
        }
        float progress = effect ? 1f : holdStart >= 0f ? Mathf.Clamp01((now - holdStart) / 0.8f) : 0f;
        fill.rectTransform.sizeDelta = new Vector2(240f * progress,8f);
        fill.color = demo ? Gold : Teal;
        bool energySkill = key == "1" || key == "2" || key == "3" || key == "4";
        energyCost.gameObject.SetActive(energySkill);
        if (energySkill)
        {
            int cost = key == "1" ? Powers.LureCost : key == "2" ? Powers.FreezeCost : key == "4" ? Powers.RocketCost : Powers.FrenzyCost;
            int available = GameManager.Instance.SkillEnergy;
            energyCost.text = cost + (effect ? " ENERGY USED" : " ENERGY") + "   |   ENERGY " + available;
            energyCost.color = effect ? Teal : available >= cost ? Gold : new Color(1f,.35f,.3f);
            energyCost.rectTransform.sizeDelta = new Vector2(Mathf.Min(750f,root.rect.width-40f),60);
        }
        title.rectTransform.sizeDelta = new Vector2(Mathf.Min(1000f,root.rect.width-60f),80);
        action.rectTransform.sizeDelta = new Vector2(Mathf.Min(900f,root.rect.width-60f),54);
        for (int i = 0; i < dots.Length; i++) if (i == owner.CurrentLesson - 1) dots[i].color = demo ? Gold : Teal;
        Zombie target = GameManager.Instance.Spawner.AliveZombies.FirstOrDefault(z => z != null && z.IsAlive);
        pointer.gameObject.SetActive(target != null && owner.CurrentLesson != 2 && (owner.AllowsTyping || demo));
        if (pointer.gameObject.activeSelf)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(target.LabelAnchor);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 at);
            pointer.anchoredPosition = at + Vector2.up * (70f + Mathf.Sin(Time.time*4f)*8f);
        }
        DrawPull();
    }

    private void DrawPull()
    {
        LureBomb bomb = LureBomb.Active.FirstOrDefault(b => b != null && b.IsLuring);
        var zombies = GameManager.Instance.Spawner.AliveZombies;
        for (int i = 0; i < 3; i++)
        {
            bool show = owner.CurrentLesson == 3 && bomb != null && i < zombies.Count && zombies[i] != null && zombies[i].IsAlive;
            if (show && pullLines[i] == null)
            {
                if (lineMaterial == null) lineMaterial = Palette.Lit(Gold);
                var line = new GameObject("Lure Direction"); line.transform.SetParent(owner.transform,false);
                pullLines[i] = line.AddComponent<LineRenderer>(); pullLines[i].sharedMaterial = lineMaterial;
                pullLines[i].widthMultiplier = .055f; pullLines[i].positionCount = 5;
                pullDots[i] = Shapes.Block(PrimitiveType.Sphere,"Attraction",owner.transform,Vector3.zero,Vector3.one*.18f,lineMaterial).transform;
            }
            if (pullLines[i] == null) continue;
            pullLines[i].gameObject.SetActive(show); pullDots[i].gameObject.SetActive(show);
            if (!show) continue;
            Vector3 from=zombies[i].Position+Vector3.up*.4f, to=bomb.Position+Vector3.up*.4f;
            Vector3 back=(from-to).normalized*.8f, side=Vector3.Cross(Vector3.up,back)*.5f;
            pullLines[i].SetPositions(new[]{from,to,to+back+side,to,to+back-side});
            pullDots[i].position=Vector3.Lerp(from,to,Mathf.Repeat(Time.time*.6f+i*.25f,1f));
        }
    }

    public void Complete()
    {
        complete = true;
        root.SetParent(hud.GetComponentInParent<Canvas>().rootCanvas.transform,false);
        foreach (Transform child in root) child.gameObject.SetActive(false);
        Plate(root,new Color(.025f,.045f,.065f,.88f));
        Text("Ready",root,"READY!",new Vector2(.5f,.5f),new Vector2(0,130),new Vector2(800,100),64);
        CompletionButton("Play Normal","PLAY NORMAL",new Vector2(0,-20),()=>GameManager.Instance.SelectLevel(1));
        CompletionButton("Menu","MENU",new Vector2(0,-200),()=>GameManager.Instance.LoadMenu());
    }

    private void CompletionButton(string name,string label,Vector2 at,UnityEngine.Events.UnityAction action)
    {
        RectTransform rect=Rect(name,root,new Vector2(.5f,.5f),at,new Vector2(560,140));
        Image face=Plate(rect,new Color(.08f,.3f,.33f));face.raycastTarget=true;
        Button button=rect.gameObject.AddComponent<Button>();button.targetGraphic=face;button.onClick.AddListener(action);
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        Text("Label",rect,label,new Vector2(.5f,.5f),Vector2.zero,new Vector2(520,110),38);
    }
}

public class TutorialPracticeKey : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public TutorialCoachView View;
    private int? finger;
    public void OnPointerDown(PointerEventData e) { if (finger.HasValue) return; finger=e.pointerId; View.Press(true); }
    public void OnPointerUp(PointerEventData e) { if (finger!=e.pointerId) return; finger=null; View.Press(false); }
    private void OnDisable() { if (finger.HasValue && View != null) View.Press(false); finger=null; }
}
