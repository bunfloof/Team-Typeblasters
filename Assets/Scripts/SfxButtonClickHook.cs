using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SfxButtonClickHook : MonoBehaviour, IPointerUpHandler
{
    private const string ClickSound = "UI/button_click";

    private Button button;
    private int playedFrame = -1;

    private void Awake()
    {
        button = GetComponent<Button>();
        Sfx.Preload(ClickSound);
        button.onClick.AddListener(PlayFallback); // keyboard Enter/Space -> onClick.Invoke()
    }

    // Plays at the moment of release, only if it will count as a click.
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!button.IsInteractable() || !eventData.eligibleForClick) return;
        if (eventData.pointerEnter == null || !eventData.pointerEnter.transform.IsChildOf(transform)) return;

        playedFrame = Time.frameCount;
        Sfx.PlayUI(ClickSound, 0.04f);
    }

    private void PlayFallback()
    {
        if (playedFrame == Time.frameCount) return; // already played on release
        Sfx.PlayUI(ClickSound, 0.04f);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(PlayFallback);
    }
}
