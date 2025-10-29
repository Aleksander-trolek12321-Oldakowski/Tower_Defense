using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Scenes (put exact build index or name)")]
    public string lobbySceneName = "Lobby";

    public void OnPlayPressed()
    {
        // Load Lobby scene (synchronnie).
        SceneManager.LoadScene(lobbySceneName);
    }

    public void OnCreditsPressed()
    {
        Debug.Log("[Menu] Credits pressed - implement UI panel if needed.");
    }

    public void OnQuitPressed()
    {
        Debug.Log("[Menu] Quit pressed - quitting.");
        Application.Quit();
    }
}
