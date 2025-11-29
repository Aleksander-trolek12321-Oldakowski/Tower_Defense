using System.Collections;
using Fusion;
using UnityEngine;
using TMPro;
using Networking;
using UnityEngine.SceneManagement;

public class GameRoundManager : NetworkBehaviour
{
    public static GameRoundManager Instance;

    [Networked] public int CurrentRound { get; set; } = 1;
    [Networked] public float RoundTimer { get; set; } = 60f;
    [Networked] public bool IsGameActive { get; set; } = false;
    [Networked] public bool GameEnded { get; set; } = false;

    [Header("UI References (assign in inspector if possible)")]
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI timerText;

    [Header("Game Settings")]
    public int totalRounds = 20;
    public float roundDuration = 60f;
    public int baseRoundMoney = 25;
    public int defenderKillReward = 5;
    public int attackerHalfwayReward = 10;
    public int attackerFullWayReward = 20;

    [Header("Scene / shutdown settings")]
    public string winSceneName = "WinScene";
    public string loseSceneName = "LoseScene";
    [Tooltip("Delay before shutting down runner (seconds). Allows clients to load scene/UI first.)")]
    public float shutdownDelay = 1.5f;

    private TickTimer _roundTimer;

    private void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        base.Spawned();

        Debug.Log($"[GameRoundManager] Spawned on {(Runner!=null && Runner.IsServer ? "SERVER" : "CLIENT")} - ObjectId={Object.Id} RoundTimer={RoundTimer} IsGameActive={IsGameActive}");

        if (Runner != null && Runner.IsServer)
        {
            StartGame();
        }
    }

    void Update()
    {
        TryEnsureUITextReferences();
        UpdateUI();
    }

    public void StartGame()
    {
        if (Runner != null && Runner.IsServer)
        {
            IsGameActive = true;
            CurrentRound = 1;
            RoundTimer = roundDuration;
            _roundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration);
            Debug.Log("[GameRoundManager] StartGame (server)");
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsGameActive || GameEnded) return;

        if (Runner != null && Runner.IsServer)
        {
            if (_roundTimer.Expired(Runner))
            {
                EndRound();
            }
            else
            {
                RoundTimer = _roundTimer.RemainingTime(Runner) ?? 0f;
            }
        }

        if (Runner != null && Runner.IsServer)
        {
            CheckGameEndConditions();
        }
    }

    private void EndRound()
    {
        if (Runner == null || !Runner.IsServer) return;

        RewardAllPlayers();

        CurrentRound++;

        if (CurrentRound > totalRounds)
        {
            EndGame(true);
            return;
        }

        RoundTimer = roundDuration;
        _roundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration);
        Debug.Log($"[GameRoundManager] EndRound -> Next round {CurrentRound}");
    }

    private void RewardAllPlayers()
    {
        var players = FindObjectsOfType<PlayerNetwork>();
        foreach (var player in players)
        {
            player.Money += baseRoundMoney;
        }
    }

    public void RewardDefenderForKill()
    {
        if (!Runner || !Runner.IsServer) return;

        var players = FindObjectsOfType<PlayerNetwork>();
        foreach (var player in players)
        {
            if (player.Team == 0)
            {
                player.Money += defenderKillReward;
                Debug.Log($"[GameRoundManager] Defender rewarded {defenderKillReward} for kill");
            }
        }
    }

    public void RewardAttackerForDistance(bool isHalfway)
    {
        if (!Runner || !Runner.IsServer) return;

        var players = FindObjectsOfType<PlayerNetwork>();
        foreach (var player in players)
        {
            if (player.Team == 1)
            {
                int reward = isHalfway ? attackerHalfwayReward : attackerFullWayReward;
                player.Money += reward;
                Debug.Log($"[GameRoundManager] Attacker rewarded {reward} for {(isHalfway ? "halfway" : "base attack")}");
            }
        }
    }

    private void CheckGameEndConditions()
    {
        if (!Runner || !Runner.IsServer) return;

        var baseHealth = FindObjectOfType<BaseHealth>();
        if (baseHealth != null && baseHealth.HP <= 0)
        {
            EndGame(false);
        }
    }

    public void EndGame(bool defendersWin)
    {
        if (!Runner || !Runner.IsServer)
        {
            Debug.LogWarning("[GameRoundManager] EndGame called but not server - ignoring.");
            return;
        }

        IsGameActive = false;
        GameEnded = true;

        Debug.Log($"[GameRoundManager] EndGame called. defendersWin={defendersWin}");

        RPC_ShowGameResult(defendersWin);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowGameResult(bool defendersWin, RpcInfo info = default)
    {
        var localPlayer = PlayerNetwork.Local;

        if (localPlayer != null)
        {
            ShowResultAndBlockInputForLocal(defendersWin);
        }
        else
        {
            StartCoroutine(WaitForLocalAndShow(defendersWin, 5f));
        }
    }

    private IEnumerator WaitForLocalAndShow(bool defendersWin, float timeout)
    {
        float t = 0f;
        while (t < timeout)
        {
            if (PlayerNetwork.Local != null)
            {
                ShowResultAndBlockInputForLocal(defendersWin);
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[GameRoundManager] PlayerNetwork.Local not found after timeout; showing UI fallback.");
        HideAllOtherUIAndBlockInput(preserveRoundTimer: true);
    }

    private void ShowResultAndBlockInputForLocal(bool defendersWin)
    {
        HideAllOtherUIAndBlockInput(preserveRoundTimer: true);

        var localPlayer = PlayerNetwork.Local;

        bool playerWon = (defendersWin && localPlayer.Team == 0) || (!defendersWin && localPlayer.Team == 1);

        StartCoroutine(LoadSceneAndMaybeShutdownCoroutine(playerWon));
    }

    private IEnumerator LoadSceneAndMaybeShutdownCoroutine(bool localPlayerWon)
    {
        yield return new WaitForSeconds(0.5f);

        string scene = localPlayerWon ? winSceneName : loseSceneName;

        if (!string.IsNullOrEmpty(scene))
        {
            Debug.Log($"[GameRoundManager] Loading scene '{scene}' for local player (won={localPlayerWon})");
            SceneManager.LoadScene(scene);
        }
        else
        {
            Debug.LogWarning("[GameRoundManager] win/lose scene name not set - skipping SceneManager.LoadScene");
        }

        if (Runner != null && Runner.IsServer)
        {
            float wait = Mathf.Max(0.1f, shutdownDelay);
            Debug.Log($"[GameRoundManager] Server will shutdown runner in {wait} seconds.");
            yield return new WaitForSeconds(wait);

            try
            {
                Debug.Log("[GameRoundManager] Server shutting down NetworkRunner now.");
                Runner.Shutdown();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[GameRoundManager] Exception while shutting down Runner: " + ex.Message);
            }
        }
    }

    private void TryEnsureUITextReferences()
    {
        if (roundText != null && timerText != null) return;

        var all = FindObjectsOfType<TextMeshProUGUI>();
        foreach (var t in all)
        {
            if (t == null) continue;
            string n = t.gameObject.name.ToLower();
            if (roundText == null && (n.Contains("round") || n.Contains("runda") || n.Contains("roundtext")))
            {
                roundText = t;
            }
            if (timerText == null && (n.Contains("time") || n.Contains("timer") || n.Contains("czas") || n.Contains("timertext")))
            {
                timerText = t;
            }
            if (roundText != null && timerText != null) break;
        }
    }

    private void HideAllOtherUIAndBlockInput(bool preserveRoundTimer)
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (var c in canvases)
        {

            if (preserveRoundTimer)
            {
                if (roundText != null && IsParent(c.gameObject, roundText.gameObject)) continue;
                if (timerText != null && IsParent(c.gameObject, timerText.gameObject)) continue;
            }

            try
            {
                c.gameObject.SetActive(false);
            }
            catch { }
        }

        var camController = FindObjectOfType<Controller.CameraController>();
        if (camController != null)
        {
            camController.enabled = false;
        }
    }

    private bool IsParent(GameObject potentialParent, GameObject child)
    {
        if (potentialParent == null || child == null) return false;
        if (potentialParent == child) return true;
        Transform t = child.transform;
        while (t != null)
        {
            if (t.gameObject == potentialParent) return true;
            t = t.parent;
        }
        return false;
    }

    private void EnsureParentCanvasActive(GameObject go)
    {
        if (go == null) return;
        go.SetActive(true);

        var canvas = go.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                var cam = Camera.main;
                if (cam != null) canvas.worldCamera = cam;
            }
        }
    }

    private void UpdateUI()
    {
        if (roundText != null)
        {
            roundText.text = $"Round: {CurrentRound}/{totalRounds}";
        }

        if (timerText != null)
        {
            timerText.text = $"Time: {Mathf.CeilToInt(RoundTimer)}";
        }
    }

    public void OnReturnToMenuButton()
    {
        Debug.Log("[GameRoundManager] ReturnToMenu button pressed - attempting to switch to Menu and shutdown runner.");

        string menuSceneName = "Menu";
        SceneManager.LoadScene(menuSceneName);

        var runner = FindObjectOfType<Fusion.NetworkRunner>();
        if (runner != null)
        {
            try
            {
                runner.Shutdown();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[GameRoundManager] Shutdown failed: " + ex.Message);
            }
        }
        else
        {
            Debug.LogWarning("[GameRoundManager] No NetworkRunner found when attempting to return to menu.");
        }
    }
}
