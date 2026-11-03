// SettingsButton.cs
// ---------------------------------------------------------------------------
// The SETTINGS gear at the top-right of the Start and pause panels of Normal
// and Hard (prefab: Assets/Prefabs/UI/Settings Button.prefab). The Button's
// On Click calls OpenSettings (set in the prefab's Inspector), which opens the
// settings panel over everything (see SettingsPanel). The Easy scene has no
// gear. The Menu scene still builds its own gear in code (SettingsPanel.AddButton).
// ---------------------------------------------------------------------------
using UnityEngine;

public class SettingsButton : MonoBehaviour
{
    public void OpenSettings()
    {
        SettingsPanel.Open(transform);
    }
}
