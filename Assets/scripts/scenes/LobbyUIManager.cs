using System;
using UnityEngine;
using UnityEngine.UI;
using Networking;
using TMPro;

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI[] playerSlots;     // size 2 (slots for Player1, Player2)
    public TextMeshProUGUI statusText;        // np. "Waiting for players..." / "Starting in: 10"
    public GameObject countdownPanel;

    void OnEnable()
    {
        LobbyManager.OnPlayerListUpdated += OnPlayerListUpdated;
        LobbyManager.OnCountdownStarted += OnCountdownStarted;
        LobbyManager.OnCountdownUpdated += OnCountdownUpdated;
        LobbyManager.OnCountdownCancelled += OnCountdownCancelled;
        LobbyManager.OnMatchStarted += OnMatchStarted;
    }

    void OnDisable()
    {
        LobbyManager.OnPlayerListUpdated -= OnPlayerListUpdated;
        LobbyManager.OnCountdownStarted -= OnCountdownStarted;
        LobbyManager.OnCountdownUpdated -= OnCountdownUpdated;
        LobbyManager.OnCountdownCancelled -= OnCountdownCancelled;
        LobbyManager.OnMatchStarted -= OnMatchStarted;
    }

    void Start()
    {
        // initialize UI
        ClearPlayerSlots();
        if (statusText != null) statusText.text = "Czekam na graczy...";
        if (countdownPanel != null) countdownPanel.SetActive(false);
    }

    void ClearPlayerSlots()
    {
        if (playerSlots == null) return;
        for (int i = 0; i < playerSlots.Length; i++) playerSlots[i].text = "Brak gracza";
    }

    void OnPlayerListUpdated(string[] names)
    {
        // update UI slots
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (i < names.Length && !string.IsNullOrEmpty(names[i]))
                playerSlots[i].text = names[i];
            else
                playerSlots[i].text = "Brak gracza";
        }

        if (names.Length < 2)
        {
            if (statusText != null) statusText.text = "Czekam na graczy...";
        }
    }

    void OnCountdownStarted(int seconds)
    {
        if (statusText != null) statusText.text = $"Start za {seconds}s";
        if (countdownPanel != null) countdownPanel.SetActive(true);
    }

    void OnCountdownUpdated(int secondsRemaining)
    {
        if (statusText != null) statusText.text = $"Start za {secondsRemaining}s";
    }

    void OnCountdownCancelled()
    {
        if (statusText != null) statusText.text = "Przerwano odliczanie - za mało graczy";
        if (countdownPanel != null) countdownPanel.SetActive(false);
    }

    void OnMatchStarted()
    {
        if (statusText != null) statusText.text = "Gra startuje...";
        if (countdownPanel != null) countdownPanel.SetActive(false);

        gameObject.SetActive(false);
    }
}
