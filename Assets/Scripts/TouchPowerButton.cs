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
//   [3] FRENZY      TAP to use a kept FRENZY, HOLD to buy one with the combo.
//   [4] RPG         the same for the RPG.
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
        Rocket
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
    private TMP_Text countText;    // "x2", "1 / 2"...
    private TMP_Text statusText;   // "HOLD: BUY", "LOADED"...
    private Vector2 laidOutSize;   // the button size the texts were last laid out for
    private CanvasGroup group;
    private Color color;

    // The finger that pressed the button owns it until it lifts; other fingers
    // touching the button meanwhile are ignored (they must not steer the aim).
    private const int NoFinger = int.MinValue;
    private int ownerFinger = NoFinger;
    private float pressTime;       // game time the button went down
    private bool couldBuyAtPress;  // a hold would buy a weapon (decided when the finger went down)
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
        float lineHeight = Mathf.Max(1f, (size.y - 2f * Padding) * 0.5f);
        float capSize = Mathf.Min(MaxCapSize, lineHeight);
        float countWidth = Mathf.Min(MaxCountWidth, size.x * 0.4f);

        TouchUI.Place(cap, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(Padding, -Padding - (lineHeight - capSize) * 0.5f), new Vector2(capSize, capSize));
        TouchUI.Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(2f * Padding + capSize, -Padding), new Vector2(Mathf.Max(1f, size.x - 3f * Padding - capSize), lineHeight));
        TouchUI.Place(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(Padding, Padding), new Vector2(Mathf.Max(1f, size.x - 3f * Padding - countWidth), lineHeight));
        TouchUI.Place(countText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-Padding, Padding), new Vector2(countWidth, lineHeight));
    }

    private void Update()
    {
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
                countText.text = "x" + powers.LureCharges;
                statusText.text = powers.IsAiming ? "DRAG TO AIM" : (powers.LureCharges > 0 ? "TAP / DRAG" : "");
                usable = powers.LureCharges > 0;
                break;
            case Power.Freeze:
                countText.text = "x" + powers.FreezeCharges;
                statusText.text = Powers.IsFrozen ? "FROZEN!" : (powers.FreezeCharges > 0 ? "TAP" : "");
                usable = powers.FreezeCharges > 0;
                break;
            default:
                bool rocket = power == Power.Rocket;
                int kept = rocket ? powers.RocketKept : powers.FrenzyKept;
                bool canBuy = powers.CanBuyNow(rocket);
                countText.text = kept + " / " + Powers.MaxKept;
                if (rocket && powers.RocketLoaded)
                {
                    statusText.text = "LOADED";
                }
                else if (canBuy)
                {
                    statusText.text = kept > 0 ? "TAP: USE HOLD: BUY" : "HOLD: BUY";
                }
                else
                {
                    statusText.text = kept > 0 ? "TAP: USE" : "";
                }
                usable = kept > 0 || canBuy || (rocket && powers.RocketLoaded);
                break;
        }
        bool pressed = ownerFinger != NoFinger;
        group.alpha = usable || pressed ? 1f : DimmedAlpha;

        // While a weapon button is held and a hold would buy: fill it like the
        // key's bar (Powers buys after BuyHoldSeconds of game time, and only
        // counts while playing).
        float filled = 0f;
        bool playing = GameManager.Instance.State == GameState.Playing;
        if (pressed && playing && couldBuyAtPress && (power == Power.Frenzy || power == Power.Rocket))
        {
            filled = Mathf.Clamp01((Time.time - pressTime) / Powers.BuyHoldSeconds);
        }
        fill.rectTransform.anchorMax = new Vector2(filled, 1f);
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
        pressTime = Time.time;
        pressPosition = eventData.position;
        background.color = Color.Lerp(Color.black, color, 0.7f);
        transform.localScale = new Vector3(PressedScale, PressedScale, 1f);

        // The game only starts a hold while playing (not in the 3-2-1 countdown),
        // and only while it reads the keys (not while the shield word is typed,
        // see EasyShield).
        Powers powers = GameManager.Instance != null ? GameManager.Instance.Powers : null;
        bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
        TypingController typing = FindFirstObjectByType<TypingController>();
        bool readingKeys = typing != null && typing.enabled;
        couldBuyAtPress = playing && readingKeys && powers != null && powers.CanBuyNow(power == Power.Rocket);

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
