using UnityEngine;
using UnityEngine.SceneManagement;

namespace UI
{
    public class SettingsPanel : MonoBehaviour
    {
        public string menuSceneName = "Menu";

        public void ChangeScene()
        {
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
