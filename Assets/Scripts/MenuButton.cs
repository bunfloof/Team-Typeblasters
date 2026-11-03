// MenuButton.cs
// ---------------------------------------------------------------------------
// The MENU button on the Start, pause, win and lose panels of the level scenes
// (prefab: Assets/Prefabs/UI/Menu Button.prefab, one instance per panel).
// The Button's On Click calls GoToMenu (set in the prefab's Inspector), which
// goes back to the menu scene through GameManager, so a run in progress is
// still ended for analytics first.
// ---------------------------------------------------------------------------
using UnityEngine;

public class MenuButton : MonoBehaviour
{
    public void GoToMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadMenu();
        }
    }
}
