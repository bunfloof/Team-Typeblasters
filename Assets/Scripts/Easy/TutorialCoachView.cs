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
        title.text = heading; action.text = instruction; keyText.text = shownKey;
        key = shownKey; demo = demonstrating;
        action.color = demonstrating ? Gold : Teal;
        keyCard.gameObject.SetActive(shownKey.Length > 0);
        replay.gameObject.SetActive(!demonstrating && owner.CurrentLesson >= 3);
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
        if (demo || complete || key.Length == 0) return;
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
        keyCard.localScale = Vector3.one * mobile * (Pressed() ? .93f : 1f);
        bool effect = owner.CurrentLesson == 2 ? Powers.IsShielded : owner.CurrentLesson == 4 ? Powers.IsFrozen
            : owner.CurrentLesson == 5 ? Powers.IsFrenzy : owner.CurrentLesson == 6 && GameManager.Instance.Powers.RocketLoaded;
        keyFace.color = effect || Pressed() ? new Color(.14f,.49f,.47f) : Color.Lerp(new Color(.05f,.13f,.18f), new Color(.13f,.3f,.32f), .5f + .5f * Mathf.Sin(Time.time * 3f));
        float progress = effect ? 1f : 0f;
        fill.rectTransform.sizeDelta = new Vector2(240f * progress,8f);
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
