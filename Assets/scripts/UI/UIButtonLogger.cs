using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UIButtonLogger : MonoBehaviour
    {
        public Button targetButton;

        void Start()
        {
            if (targetButton == null) targetButton = GetComponent<Button>();
            if (targetButton != null)
                targetButton.onClick.AddListener(LogClick);
            else
                Debug.LogWarning("[UIButtonLogger] No Button found on " + gameObject.name);
        }

        public void LogClick()
        {
            Debug.Log($"[UIButtonLogger] CLICK detected on '{gameObject.name}'");
        }
    }
}
