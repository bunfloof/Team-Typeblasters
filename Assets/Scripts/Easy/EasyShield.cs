using UnityEngine;

// Compatibility component for existing scenes/prefabs. Shield gameplay is now
// owned by Powers and ShieldWord in every level. Retain this script GUID so
// older scenes load without missing components, but never read input or add UI.
[DefaultExecutionOrder(-30)]
public class EasyShield : MonoBehaviour
{
    private void Awake()
    {
        enabled = false;
    }
}
