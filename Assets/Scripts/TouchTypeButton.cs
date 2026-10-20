// TouchTypeButton.cs
// ---------------------------------------------------------------------------
// The TYPE button at the right end of the touch controls' button row
// (TouchControls builds it). The game is typed with the phone's own keyboard:
//   keyboard closed: "TAP TO TYPE", lit up - opens the keyboard. (At the start
//                    of play any tap on the game opens it; once the player
//                    closed it, only this button does.)
//   keyboard open:   "HIDE KEYBOARD" - closes it, to see more of the game
// The taps themselves are handled by the browser (Plugins/WebGL/
// TouchControlsWeb.jslib, which TouchControls tells where this button is): a
// phone only opens its keyboard during the tap itself, and Unity sees a tap a
// moment later. This script only shows which of the two the button does.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TouchTypeButton : MonoBehaviour
{
    private static readonly Color ClosedColor = new Color(0.78f, 0.58f, 0.08f); // lit up: tap here
    private static readonly Color OpenColor = new Color(0.14f, 0.15f, 0.19f);

    private Image background;
    private TMP_Text label;

    public void Setup(Image buttonBackground, TMP_Text buttonLabel)
    {
        background = buttonBackground;
        label = buttonLabel;
    }

    private void Update()
    {
        bool open = TouchControls.KeyboardOpen;
        label.text = open ? "HIDE\nKEYBOARD" : "TAP TO\nTYPE";
        background.color = open ? OpenColor : ClosedColor;
    }
}
