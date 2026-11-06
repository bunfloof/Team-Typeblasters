// TouchPowerButton.cs
// ---------------------------------------------------------------------------
// One of the four power buttons of the touch controls (built by TouchControls,
// in the row above the phone's keyboard). Each does exactly what its key does
// on a keyboard:
//
//   [1] LURE BOMB   TAP to throw it straight ahead, or HOLD and DRAG to aim
//                   (drag left / right = direction, up / down = distance) and
//                   let go to throw. The drag works like the arrow keys.
//   [2] FREEZE      TAP to freeze every enemy.
//   [3] FRENZY      TAP to spend 10 energy and activate.
//   [4] RPG         TAP to spend 15 energy and load a rocket.
//
// The button shows what the player has (charges, weapons kept), lights up
// while touched, fills up while a weapon is being bought, and is dimmed when
// pressing it would do nothing. Its texts are laid out in two lines that fit
// any button size (the row is short and, held upright, narrow):
//   [key] NAME
//   status        count
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TouchPowerButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
{
    public enum Power
    {
        Lure,
        Freeze,
        Frenzy,
        Rocket,
        Shield
    }

    private bool hiddenByIntro;
    private float revealTime = -100f;

    // The HUD feature that shows this button (HudReveal).
    private HudFeature Feature
    {
        get
        {
            switch (power)
            {
                case Power.Lure: return HudFeature.Lure;
                case Power.Freeze: return HudFeature.Freeze;
                case Power.Frenzy: return HudFeature.Frenzy;
                case Power.Rocket: return HudFeature.Rpg;
                default: return HudFeature.Shield;
            }
        }
    }

    private const float DragDeadZone = 35f;    // canvas units the finger must move before the drag steers
    private const float PressedScale = 1.04f;
    private const float DimmedAlpha = 0.45f;   // nothing to use and nothing to buy

    private const float Padding = 10f;
    private const float MaxCapSize = 100f;     // the key cap, at most this big
    private const float MaxCountWidth = 160f;

    private Power power;
    private Image background;
    private Image fill;            // grows from the left while a weapon is being bought
    private RectTransform cap;     // the key cap: the power's key in a white square
    private TMP_Text nameText;
    private TMP_Text countText;    // charges or energy cost
    private TMP_Text statusText;   // TAP, LOW ENERGY, ACTIVE, LOADED
    private Vector2 laidOutSize;   // the button size the texts were last laid out for
    private CanvasGroup group;
    private Color color;

    // The finger that pressed the button owns it until it lifts; other fingers
    // touching the button meanwhile are ignored (they must not steer the aim).
    private const int NoFinger = int.MinValue;
    private int ownerFinger = NoFinger;
    private Vector2 pressPosition; // screen pixels where the finger went down (lure aiming)

    // Called once by TouchControls right after it creates the button.
    public void Setup(Power buttonPower, string keyText, string powerName, Color powerColor)
    {
        power = buttonPower;
        color = powerColor;
        RectTransform rect = (RectTransform)transform;

        background = gameObject.AddComponent<Image>();
        background.color = Color.Lerp(Color.black, color, 0.35f);
        group = gameObject.AddComponent<CanvasGroup>();
        if (EasyTutorial.Active != null) { group.alpha = 0f; group.blocksRaycasts = false; }

        // The buying bar fills the button from the left (behind the texts).
        fill = TouchUI.CreateImage("Buy Fill", rect, new Color(color.r, color.g, color.b, 0.55f));
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        // The key cap: the power's key in a white square (the hints say "HOLD 3"...).
        Image capImage = TouchUI.CreateImage("Key Cap", rect, Color.white);
        capImage.raycastTarget = false;
        cap = capImage.rectTransform;
        TMP_Text key = CreateFittingText("Key", cap, keyText, 20f, 80f);
        key.color = Color.black;
        key.fontStyle = FontStyles.Bold;

        nameText = CreateFittingText("Name", rect, powerName, 18f, 72f);
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.fontStyle = FontStyles.Bold;

        countText = CreateFittingText("Count", rect, "", 20f, 90f);
        countText.alignment = TextAlignmentOptions.Right;
        countText.fontStyle = FontStyles.Bold;

        statusText = CreateFittingText("Status", rect, "", 12f, 50f);
        statusText.alignment = TextAlignmentOptions.Left;
        statusText.color = new Color(1f, 1f, 1f, 0.85f);
        statusText.textWrappingMode = TextWrappingModes.Normal; // a long status may take two lines

        LayOut();
    }

    // A text that grows or shrinks (between minSize and maxSize) to fit its
    // rectangle: the button is about a finger tall, which is more canvas units
    // when the phone is held upright.
    private static TMP_Text CreateFittingText(string objectName, RectTransform parent, string text, float minSize, float maxSize)
    {
        TMP_Text label = TouchUI.CreateText(objectName, parent, text, maxSize);
        label.enableAutoSizing = true;
        label.fontSizeMin = minSize;
        label.fontSizeMax = maxSize;
        return label;
    }

    // Places the texts in two lines that fill the button (each half of its
    // height): the key cap and the name on top, the status and the count below.
    private void LayOut()
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        laidOutSize = size;
        float textHeight = Mathf.Max(2f, size.y - 2f * Padding);
        bool showsEnergyCost = power != Power.Shield;
        float costLineHeight = textHeight * (showsEnergyCost ? 0.56f : 0.5f);
        float lineHeight = textHeight - costLineHeight;
        float capSize = Mathf.Min(MaxCapSize, lineHeight);
        float countWidth = Mathf.Min(MaxCountWidth, size.x * (showsEnergyCost ? 0.48f : 0.4f));

        TouchUI.Place(cap, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(Padding, -Padding - (lineHeight - capSize) * 0.5f), new Vector2(capSize, capSize));
        TouchUI.Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(2f * Padding + capSize, -Padding), new Vector2(Mathf.Max(1f, size.x - 3f * Padding - capSize), lineHeight));
        TouchUI.Place(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(Padding, Padding), new Vector2(Mathf.Max(1f, size.x - 3f * Padding - countWidth), costLineHeight));
        TouchUI.Place(countText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-Padding, Padding), new Vector2(countWidth, costLineHeight));
    }

    private void Update()
    {
        if (EasyTutorial.Active != null && !EasyTutorial.Active.IsFinalWave)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            return; // the contextual key card replaces the five-button toolbar
        }
        // Guided first wave: a button appears only once its power is introduced, with a pop.
        if (!HudReveal.IsShown(Feature))
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            hiddenByIntro = true;
            return;
        }
        if (hiddenByIntro)
        {
            hiddenByIntro = false;
            revealTime = Time.unscaledTime;
        }
        float reveal = Mathf.Clamp01((Time.unscaledTime - revealTime) / 0.45f);
        transform.localScale = Vector3.one * (1f + (reveal < 1f ? Mathf.Sin(reveal * Mathf.PI) * 0.35f : 0f));
        group.blocksRaycasts = true;
        // The button changed size (the phone turned): lay the texts out again.
        if (((RectTransform)transform).rect.size != laidOutSize)
        {
            LayOut();
        }

        Powers powers = GameManager.Instance != null ? GameManager.Instance.Powers : null;
        if (powers == null)
        {
            return;
        }

        bool usable;
        switch (power)
        {
            case Power.Lure:
                countText.text = Powers.LureCost + " E";
                usable = GameManager.Instance.SkillEnergy >= Powers.LureCost || powers.IsAiming;
                statusText.text = powers.IsAiming ? "DRAG TO AIM" : usable ? "TAP / DRAG" : "LOW ENERGY";
                break;
            case Power.Freeze:
                countText.text = Powers.FreezeCost + " E";
                usable = GameManager.Instance.SkillEnergy >= Powers.FreezeCost || Powers.IsFrozen;
                statusText.text = Powers.IsFrozen ? "ACTIVE" : usable ? "TAP" : "LOW ENERGY";
                break;
            case Power.Shield:
                countText.text = "x" + powers.ShieldCharges;
                statusText.text = Powers.IsShielded ? "ACTIVE" : powers.ShieldCharges > 0 ? "TAP" : "NEXT WAVE";
                usable = Powers.IsShielded || powers.ShieldCharges > 0;
                break;
            default:
                bool rocket = power == Power.Rocket;
                bool ready = powers.CanUseNow(rocket);
                int cost = rocket ? Powers.RocketCost : Powers.FrenzyCost;
                bool running = rocket ? powers.RocketLoaded : Powers.IsFrenzy;
                countText.text = cost + " E";
                statusText.text = running ? (rocket ? "LOADED" : "ACTIVE") : ready ? "TAP" : "LOW ENERGY";
                usable = ready || running;                break;
        }
        bool pressed = ownerFinger != NoFinger;
        bool active = power == Power.Lure ? powers.IsAiming : power == Power.Freeze ? Powers.IsFrozen
            : power == Power.Shield ? Powers.IsShielded : power == Power.Frenzy ? Powers.IsFrenzy : powers.RocketLoaded;
        background.color = Color.Lerp(Color.black, color, pressed ? 0.7f : active ? 0.6f : 0.35f);
        group.alpha = usable || pressed ? 1f : DimmedAlpha;

        fill.rectTransform.anchorMax = new Vector2(active ? 1f : 0f,1f);
    }

    // The drag starts at once (no threshold), so aiming reacts immediately.
    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ownerFinger != NoFinger)
        {
            return; // a second finger on the same button changes nothing
        }
        ownerFinger = eventData.pointerId;

        pressPosition = eventData.position;
        background.color = Color.Lerp(Color.black, color, 0.7f);
        transform.localScale = new Vector3(PressedScale, PressedScale, 1f);

        switch (power)
        {
            case Power.Lure:
                TouchInput.Press(Key.Digit1);
                break;
            case Power.Freeze:
                TouchInput.Type('2'); // the game reads the typed character 2 for the freeze
                break;
            case Power.Frenzy:
                TouchInput.Press(Key.Digit3);
                break;
            case Power.Rocket:
                TouchInput.Press(Key.Digit4);
                break;
            case Power.Shield:
                TouchInput.Press(Key.Tab);
                break;
        }
    }

    // Lure bomb only: the finger's offset from where it went down acts like the
    // arrow keys (right / left = direction, up / down = distance).
    public void OnDrag(PointerEventData eventData)
    {
        if (power != Power.Lure || eventData.pointerId != ownerFinger)
        {
            return;
        }
        Canvas canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        Vector2 offset = (eventData.position - pressPosition) / Mathf.Max(0.01f, scale);

        SetArrow(Key.RightArrow, offset.x > DragDeadZone);
        SetArrow(Key.LeftArrow, offset.x < -DragDeadZone);
        SetArrow(Key.UpArrow, offset.y > DragDeadZone);
        SetArrow(Key.DownArrow, offset.y < -DragDeadZone);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != ownerFinger)
        {
            return; // a finger that was ignored when it went down
        }
        ownerFinger = NoFinger;
        LetGo();
    }

    // The controls were hidden while the button was held: let go of everything.
    private void OnDisable()
    {
        if (ownerFinger != NoFinger)
        {
            ownerFinger = NoFinger;
            LetGo();
        }
    }

    private void LetGo()
    {
        if (background != null)
        {
            background.color = Color.Lerp(Color.black, color, 0.35f);
        }
        transform.localScale = Vector3.one;

        switch (power)
        {
            case Power.Lure:
                SetArrow(Key.RightArrow, false);
                SetArrow(Key.LeftArrow, false);
                SetArrow(Key.UpArrow, false);
                SetArrow(Key.DownArrow, false);
                TouchInput.Release(Key.Digit1);
                break;
            case Power.Frenzy:
                TouchInput.Release(Key.Digit3);
                break;
            case Power.Rocket:
                TouchInput.Release(Key.Digit4);
                break;
            case Power.Shield:
                TouchInput.Release(Key.Tab);
                break;
        }
    }

    private static void SetArrow(Key key, bool down)
    {
        if (down)
        {
            TouchInput.Press(key);
        }
        else
        {
            TouchInput.Release(key);
        }
    }
}
