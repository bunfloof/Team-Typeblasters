// TouchKeyboard.cs
// ---------------------------------------------------------------------------
// NOT USED RIGHT NOW: the game uses the phone's own keyboard. Kept so we can
// go back to it: set UseOwnKeyboard = true in TouchControls.cs.
//
// The on-screen keyboard for phones and tablets (TouchControls shows it at the
// bottom of the screen while playing). Four rows, like a phone keyboard:
//
//   q w e r t y u i o p
//    a s d f g h j k l
//   SHIFT z x c v b n m DEL        DEL: Backspace (drop the word being typed)
//   #+0  [      SPACE       ]      #+0: the symbols rows (tap again: letters)
//                                  SHIELD: Tab; SPACE: a space in a phrase
//
// SHIFT types the next letter in capitals (tap it twice quickly for caps
// lock). Only the Hard level and the boss's energy orbs care about capitals.
// The symbols row has every symbol a word can contain (energy orbs like
// "Sp@rk", Hard words like "Hunter!"); it goes back to letters after one
// symbol, unless #+0 was tapped twice quickly.
//
// Keys are placed with anchors (fractions of the keyboard's size), so the
// layout still fits after the phone is turned or the window resized.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TouchKeyboard : MonoBehaviour
{
    private const string LetterRow1 = "qwertyuiop";
    private const string LetterRow2 = "asdfghjkl";
    private const string LetterRow3 = "zxcvbnm";
    // Same lengths as the letter rows they replace: every symbol used by the
    // energy orb words and the Hard level's punctuation (no 1-4: power keys).
    private const string SymbolRow1 = "!@#$%^&*0?";
    private const string SymbolRow2 = "-+=~;:.,'";

    private const float KeyGap = 10f;            // canvas units between two keys
    private const float LetterFontSize = 46f;
    private const float SpecialFontSize = 30f;
    private const float DoubleTapSeconds = 0.35f; // two taps closer than this: caps lock / keep symbols

    private static readonly Color LetterKeyColor = new Color(0.24f, 0.25f, 0.30f);
    private static readonly Color SpecialKeyColor = new Color(0.14f, 0.15f, 0.19f);
    private static readonly Color ActiveKeyColor = new Color(0.78f, 0.58f, 0.08f); // Shift / symbols switched on

    private const int RowCount = 4;

    private enum Mode
    {
        Off,
        Once,   // for the next character only
        Locked  // until tapped again
    }

    private readonly List<TouchKey> row1Keys = new List<TouchKey>();
    private readonly List<TouchKey> row2Keys = new List<TouchKey>();
    private readonly List<TouchKey> row3Keys = new List<TouchKey>();
    private TouchKey shiftKey;
    private TouchKey symbolsKey;

    private Mode shift = Mode.Off;
    private Mode symbols = Mode.Off;
    private float lastShiftTap = -10f;
    private float lastSymbolsTap = -10f;

    // Builds every key inside this object's RectTransform (called once by TouchControls).
    public void Build()
    {
        RectTransform area = (RectTransform)transform;

        // Row 1: 10 letters.
        for (int i = 0; i < LetterRow1.Length; i++)
        {
            row1Keys.Add(CreateKey(area, 0, i, 1f, TouchKey.KeyAction.Character, LetterRow1[i], LetterKeyColor));
        }
        // Row 2: 9 letters, half a key in from each side (like a real keyboard).
        for (int i = 0; i < LetterRow2.Length; i++)
        {
            row2Keys.Add(CreateKey(area, 1, 0.5f + i, 1f, TouchKey.KeyAction.Character, LetterRow2[i], LetterKeyColor));
        }
        // Row 3: Shift (1.5 keys wide), 7 letters, Backspace (1.5 keys wide).
        shiftKey = CreateKey(area, 2, 0f, 1.5f, TouchKey.KeyAction.Shift, '\0', SpecialKeyColor);
        for (int i = 0; i < LetterRow3.Length; i++)
        {
            row3Keys.Add(CreateKey(area, 2, 1.5f + i, 1f, TouchKey.KeyAction.Character, LetterRow3[i], LetterKeyColor));
        }
        TouchKey delete = CreateKey(area, 2, 8.5f, 1.5f, TouchKey.KeyAction.Hold, '\0', SpecialKeyColor);
        delete.SetLabel("DEL");
        // Row 4: symbols, the same Tab shield as desktop, and a real space.
        symbolsKey = CreateKey(area, 3, 0f, 2f, TouchKey.KeyAction.Symbols, '\0', SpecialKeyColor);
        TouchKey shield = CreateKey(area, 3, 2f, 2f, TouchKey.KeyAction.Hold, '\0', SpecialKeyColor, Key.Tab);
        shield.SetLabel("SHIELD");
        TouchKey space = CreateKey(area, 3, 4f, 6f, TouchKey.KeyAction.Character, ' ', SpecialKeyColor);
        space.SetLabel("SPACE");

        RefreshKeys();
    }

    // Called by a Character key: types it, in capitals if Shift is on.
    public void TypeCharacter(char character)
    {
        bool isLetter = character >= 'a' && character <= 'z';
        if (isLetter && shift != Mode.Off)
        {
            character = char.ToUpperInvariant(character);
        }
        TouchInput.Type(character);

        if (isLetter && shift == Mode.Once)
        {
            shift = Mode.Off;
            RefreshKeys();
        }
        if (!isLetter && symbols == Mode.Once)
        {
            symbols = Mode.Off; // one symbol typed: back to the letters
            RefreshKeys();
        }
    }

    public void OnShiftKey()
    {
        shift = NextMode(shift, lastShiftTap);
        lastShiftTap = Time.unscaledTime;
        RefreshKeys();
    }

    public void OnSymbolsKey()
    {
        symbols = NextMode(symbols, lastSymbolsTap);
        lastSymbolsTap = Time.unscaledTime;
        RefreshKeys();
    }

    // Off -> Once; Once -> Locked if tapped again quickly, else Off; Locked -> Off.
    private static Mode NextMode(Mode mode, float lastTap)
    {
        if (mode == Mode.Off)
        {
            return Mode.Once;
        }
        if (mode == Mode.Once && Time.unscaledTime - lastTap < DoubleTapSeconds)
        {
            return Mode.Locked;
        }
        return Mode.Off;
    }

    // Every new round starts with plain lowercase letters.
    private void OnEnable()
    {
        if (shiftKey == null)
        {
            return; // not built yet
        }
        shift = Mode.Off;
        symbols = Mode.Off;
        RefreshKeys();
    }

    // Shows the letters or the symbols, in capitals while Shift is on, and
    // lights up Shift and the symbols key while they are on.
    private void RefreshKeys()
    {
        bool showSymbols = symbols != Mode.Off;
        bool capitals = shift != Mode.Off;
        for (int i = 0; i < row1Keys.Count; i++)
        {
            char character = showSymbols ? SymbolRow1[i] : LetterRow1[i];
            row1Keys[i].SetCharacter(character, KeyText(character, capitals));
        }
        for (int i = 0; i < row2Keys.Count; i++)
        {
            char character = showSymbols ? SymbolRow2[i] : LetterRow2[i];
            row2Keys[i].SetCharacter(character, KeyText(character, capitals));
        }
        for (int i = 0; i < row3Keys.Count; i++)
        {
            row3Keys[i].SetLabel(KeyText(LetterRow3[i], capitals));
        }

        shiftKey.SetLabel(shift == Mode.Locked ? "CAPS" : "SHIFT");
        shiftKey.SetColor(capitals ? ActiveKeyColor : SpecialKeyColor);
        symbolsKey.SetLabel(showSymbols ? "ABC" : "#+0");
        symbolsKey.SetColor(symbols == Mode.Locked ? ActiveKeyColor : SpecialKeyColor);
    }

    private static string KeyText(char character, bool capitals)
    {
        return (capitals ? char.ToUpperInvariant(character) : character).ToString();
    }

    // One key in row (0 = top), starting 'column' key widths from the left and
    // 'width' key widths wide. Every row is 10 key widths wide. heldKey: for a
    // Hold key, the key it holds down (DEL holds Backspace).
    private TouchKey CreateKey(RectTransform area, int row, float column, float width,
        TouchKey.KeyAction action, char character, Color color, Key heldKey = Key.Backspace)
    {
        const float RowWidth = 10f;

        UnityEngine.UI.Image background = TouchUI.CreateImage("Key " + (character != '\0' ? character.ToString() : action.ToString()), area, color);
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(column / RowWidth, 1f - (row + 1f) / RowCount);
        rect.anchorMax = new Vector2((column + width) / RowWidth, 1f - (float)row / RowCount);
        rect.offsetMin = new Vector2(KeyGap * 0.5f, KeyGap * 0.5f);
        rect.offsetMax = new Vector2(-KeyGap * 0.5f, -KeyGap * 0.5f);

        float fontSize = action == TouchKey.KeyAction.Character ? LetterFontSize : SpecialFontSize;
        TMP_Text label = TouchUI.CreateText("Label", rect, character != '\0' ? character.ToString() : "", fontSize);

        TouchKey key = background.gameObject.AddComponent<TouchKey>();
        key.Setup(this, action, character, heldKey, background, label, color);
        return key;
    }
}
