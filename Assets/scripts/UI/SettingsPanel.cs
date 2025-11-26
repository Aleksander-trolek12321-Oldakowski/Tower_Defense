using UnityEngine;

namespace UI
{
    public class SettingsPanel : MonoBehaviour
    {
        public GameObject panel;

        public void Toggle()
        {
            if (panel == null) { Debug.LogWarning("[SettingsPanel] panel not assigned!"); return; }
            panel.SetActive(!panel.activeSelf);
            Debug.Log("[SettingsPanel] Toggle -> active = " + panel.activeSelf);
        }

        public void Open() { if (panel != null) panel.SetActive(true); }
        public void Close() { if (panel != null) panel.SetActive(false); }
    }
}
