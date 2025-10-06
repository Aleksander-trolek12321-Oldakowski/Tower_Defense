using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Networking
{
    public class FusionNetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("References")]
        public NetworkRunner runnerPrefab;     // Assign NetworkRunner prefab (must contain NetworkProjectConfig)
        public NetworkObject playerPrefab;     // Player prefab (NetworkObject + PlayerNetwork)

        public int maxPlayers = 2;             // Exactly 2 players

        private NetworkRunner runner;

        private void Awake()
        {
            // Limit application frame rate for mobile stability
            Application.targetFrameRate = 60;
        }

        async void Start()
        {
            // Instantiate and configure the NetworkRunner
            runner = Instantiate(runnerPrefab);
            runner.ProvideInput = true;

            // Create NetworkSceneInfo from current scene (StartGameArgs.Scene expects this type)
            var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (sceneRef.IsValid)
            {
                sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);
            }

            var args = new StartGameArgs()
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = "TD_Session",
                Scene = sceneInfo, // Correct assignment: NetworkSceneInfo
                SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                PlayerCount = maxPlayers   // Limit players to 2
            };

            await runner.StartGame(args);
            runner.AddCallbacks(this);
        }

        // -------------------------
        // Core callbacks
        // -------------------------
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"OnPlayerJoined: {player}");

            // Only the server/host should perform spawn and assignment logic
            if (!runner.IsServer) return;

            // Count active players from ActivePlayers collection
            int currentPlayers = new List<PlayerRef>(runner.ActivePlayers).Count;

            int assignedTeam = 0;
            if (currentPlayers == 1)
            {
                // If this is the first player (host), assign randomly to team 0 or 1
                assignedTeam = UnityEngine.Random.value < 0.5f ? 0 : 1;
            }
            else
            {
                // For the second player, try to find the existing player's team and assign the opposite
                foreach (var kv in runner.ActivePlayers)
                {
                    if (kv != player)
                    {
                        var obj = runner.GetPlayerObject(kv);
                        if (obj != null)
                        {
                            var pn = obj.GetComponent<PlayerNetwork>();
                            if (pn != null)
                            {
                                assignedTeam = (pn.Team == 0) ? 1 : 0;
                            }
                        }
                    }
                }
            }

            if (currentPlayers == 2)
            {
                Debug.Log("[DEBUG] Two players connected — ready to start match (DEBUG LOG)");
            }

            // Choose spawn position depending on team
            Vector3 spawnPos = (assignedTeam == 0) ? new Vector3(-2f, 0f, 0f) : new Vector3(2f, 0f, 0f);

            // Spawn the player object with input authority assigned to the joining player
            var no = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            var playerNet = no.GetComponent<PlayerNetwork>();
            if (playerNet != null)
            {
                // Server/host sets the networked Team property
                playerNet.Team = assignedTeam;
            }

            Debug.Log($"Spawned player object for {player} with team {assignedTeam}");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"OnPlayerLeft: {player}");

            if (!runner.IsServer) return;
            GamePlayManager.Instance?.OnPlayerLeft(player);
        }

        // -------------------------
        // Other required callbacks (stubs / logs)
        // -------------------------
        public void OnInput(NetworkRunner runner, NetworkInput input) { /* input handling if needed */ }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            Debug.Log($"Runner shutdown: {reason}");
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("Connected to server");
        }

        // Disconnected callback with NetDisconnectReason
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"Disconnected: {reason}");
        }

        // Connect failed callback signature
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.Log($"Connect failed to {remoteAddress}: {reason}");
        }

        // Reliable data received
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        // Optional: handle connect requests (accept/reject players if needed)
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        // AOI (Area of Interest)
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnDisconnectedFromServer(NetworkRunner runner) { }
    }
}