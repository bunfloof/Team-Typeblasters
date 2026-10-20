// TouchFullscreenButton.cs
// ---------------------------------------------------------------------------
// The FULL SCREEN button of the touch controls (TouchControls builds it). Full
// screen hides the browser's address bar, so the game gets the whole phone
// screen; Android phones and iPads can do it, iPhones cannot (no button there).
//
// Browsers only switch to full screen from inside a touch, and Unity handles
// the touch a frame later. So when the finger goes down here, the page is told
// to switch when that touch ends (Plugins/WebGL/TouchControlsWeb.jslib); the
// page also turns the screen sideways and keeps it there where it can.
// ---------------------------------------------------------------------------
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TouchFullscreenButton : MonoBehaviour, IPointerDownHandler
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int TouchControlsWeb_CanGoFullscreen();

    [DllImport("__Internal")]
    private static extern int TouchControlsWeb_IsFullscreen();

    [DllImport("__Internal")]
    private static extern void TouchControlsWeb_ToggleFullscreenOnRelease();
#endif

    private TMP_Text label;

    // Can this browser show the page full screen?
    public static bool IsSupported()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return TouchControlsWeb_CanGoFullscreen() == 1;
#else
        return false;
#endif
    }

    public void Setup(TMP_Text buttonLabel)
    {
        label = buttonLabel;
    }

    private void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        label.text = TouchControlsWeb_IsFullscreen() == 1 ? "EXIT\nFULL" : "FULL\nSCREEN";
#endif
    }

    public void OnPointerDown(PointerEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TouchControlsWeb_ToggleFullscreenOnRelease();
#endif
    }
}
