using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionNetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public NetworkRunner runnerPrefab;     // assign NetworkRunner prefab (must contain NetworkProjectConfig)
    private NetworkRunner runner;

    public NetworkObject playerPrefab;     // player prefab (NetworkObject + PlayerNetwork)
    public int maxPlayers = 2;             

    private void Awake()
    {
        Application.targetFrameRate = 60;
    }

    async void Start()
    {
        // Runner Spawn
        runner = Instantiate(runnerPrefab);
        runner.ProvideInput = true;

        var args = new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = "TD_Session",
            Scene = SceneManager.GetActiveScene().buildIndex,
            SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount = maxPlayers   // limit of players
        };

        await runner.StartGame(args);
        runner.AddCallbacks(this);
    }

    // When player joins - host spawn avatar and give a team by random
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;
        Debug.Log($"OnPlayerJoined: {player}");

        // Count player
        int currentPlayers = runner.ActivePlayersCount;

        // if first player than give him random team
        // second player get opposite team
        int assignedTeam = 0;
        if (currentPlayers == 1)
        {
            // first player
            assignedTeam = UnityEngine.Random.value < 0.5f ? 0 : 1;
        }
        else
        {
            // second player - opposite team
            assignedTeam = 1; // fallback
            foreach (var kv in runner.ActivePlayers)
            {
                // ActivePlayers includes all current players; but host can track earlier - najprościej:
            }
            // Prostsz: jeśli już istnieje PlayerObject dla hosta, odczytamy team stamtąd.
            // (zamieszczamy zabezpieczenie niżej w spawnie)
        }

        // Spawn position
        Vector3 spawnPos = (assignedTeam == 0) ? new Vector3(-2f, 0f, 0f) : new Vector3(2f, 0f, 0f);

        // Spawn player avatar and assign inputAuthority = player
        var no = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);

        var pn = no.GetComponent<PlayerNetwork>();
        // we seat the team on networked property (host/StateAuthority has right)
        pn.Team = assignedTeam;

        Debug.Log($"Spawned player object for {player} with team {assignedTeam}");
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;
        Debug.Log($"Player left: {player}");
        GamePlayManager.Instance?.OnPlayerLeft(player);
    }

    // ---------- callbacks -----------
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner, string sceneName, LoadSceneMode loadSceneMode, List<int> clients) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}