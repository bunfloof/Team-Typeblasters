// TouchKey.cs
// ---------------------------------------------------------------------------
// NOT USED RIGHT NOW: the game uses the phone's own keyboard. Kept so we can
// go back to it: set UseOwnKeyboard = true in TouchControls.cs.
//
// One key of the on-screen keyboard (TouchKeyboard builds them all).
//
// A key reacts the moment a finger touches it (not when the finger lifts), so
// fast typing with two thumbs feels like a real keyboard, and it lights up and
// grows a little while it is held so the player sees which key they hit.
//
// What a key does (its KeyAction):
//   Character  types its character (TouchKeyboard adds Shift: 'a' or 'A')
//   Hold       holds a key down while touched: Backspace (DEL, drops the
//              word being typed) or Space (raises the shield)
//   Shift      next letter in capitals (tap twice quickly: caps lock)
//   Symbols    shows the symbols row (tap twice quickly: keep it)
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TouchKey : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum KeyAction
    {
        Character,
        Hold,
        Shift,
        Symbols
    }

    public KeyAction Action { get; private set; }
    public char Character { get; private set; } // for Character keys: the lowercase letter or the symbol
    private Key holdKey;                        // for Hold keys: the key held down while touched

    private TouchKeyboard keyboard;
    private Image background;
    private TMP_Text label;
    private Color normalColor;
    private Color pressedColor;
    private int fingersDown; // a key can be touched by two fingers at once

    private const float PressedScale = 1.08f;

    // Called once right after the key is created. owner: the keyboard.
    public void Setup(TouchKeyboard owner, KeyAction action, char character, Key heldKey, Image keyBackground, TMP_Text keyLabel, Color color)
    {
        keyboard = owner;
        Action = action;
        Character = character;
        holdKey = heldKey;
        background = keyBackground;
        label = keyLabel;
        SetColor(color);
    }

    // Changes what a Character key types (the letters and symbols rows share keys).
    public void SetCharacter(char character, string shownText)
    {
        Character = character;
        label.text = shownText;
    }

    public void SetLabel(string shownText)
    {
        label.text = shownText;
    }

    // The key's resting colour (Shift and Symbols light up while they are on).
    public void SetColor(Color color)
    {
        normalColor = color;
        pressedColor = Color.Lerp(color, Color.white, 0.45f);
        if (fingersDown == 0)
        {
            background.color = normalColor;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        fingersDown += 1;
        background.color = pressedColor;
        transform.localScale = new Vector3(PressedScale, PressedScale, 1f);

        switch (Action)
        {
            case KeyAction.Character:
                keyboard.TypeCharacter(Character);
                break;
            case KeyAction.Hold:
                TouchInput.Press(holdKey);
                break;
            case KeyAction.Shift:
                keyboard.OnShiftKey();
                break;
            case KeyAction.Symbols:
                keyboard.OnSymbolsKey();
                break;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        fingersDown = Mathf.Max(0, fingersDown - 1);
        if (fingersDown > 0)
        {
            return;
        }
        background.color = normalColor;
        transform.localScale = Vector3.one;

        if (Action == KeyAction.Hold)
        {
            TouchInput.Release(holdKey);
        }
    }

    // The keyboard was hidden while the key was held: forget the touch.
    private void OnDisable()
    {
        if (fingersDown > 0 && Action == KeyAction.Hold)
        {
            TouchInput.Release(holdKey);
        }
        fingersDown = 0;
        if (background != null)
        {
            background.color = normalColor;
        }
        transform.localScale = Vector3.one;
    }
}
