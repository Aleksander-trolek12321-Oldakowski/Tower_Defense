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

        private Dictionary<PlayerRef, int> playerTeams = new Dictionary<PlayerRef, int>();

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
            Debug.Log($"[DEBUG] OnPlayerJoined: {player}");

            // Only the host/server should assign teams and spawn players
            if (!runner.IsServer) return;

            // If we already assigned this player before (reconnect), reuse assignment
            int assignedTeam;
            if (playerTeams.TryGetValue(player, out assignedTeam))
            {
                Debug.Log($"[DEBUG] Player {player} already assigned to team {assignedTeam} (reconnect or cached).");
            }
            else
            {
                // Determine team:
                if (playerTeams.Count == 0)
                {
                    // first player -> random team 0 or 1
                    assignedTeam = UnityEngine.Random.Range(0, 2);
                }
                else if (playerTeams.Count == 1)
                {
                    // second player -> assign opposite of the existing player
                    int otherTeam = -1;
                    foreach (var kv in playerTeams)
                    {
                        otherTeam = kv.Value;
                        break;
                    }
                    assignedTeam = 1 - otherTeam;
                }
                else
                {
                    // Shouldn't happen in 2-player setup, but fallback to random
                    assignedTeam = UnityEngine.Random.Range(0, 2);
                }

                // store assignment
                playerTeams[player] = assignedTeam;
                Debug.Log($"[DEBUG] Assigned PlayerRef {player} -> team {assignedTeam} (stored in playerTeams).");
            }

            // Avoid double-spawn: if player object exists, just update its Team property
            var existingObject = runner.GetPlayerObject(player);
            if (existingObject != null)
            {
                var pn = existingObject.GetComponent<PlayerNetwork>();
                if (pn != null)
                {
                    pn.Team = assignedTeam;
                    Debug.Log($"[DEBUG] Updated existing player object for {player} team -> {assignedTeam}");
                }
            }
            else
            {
                // spawn player avatar for this player and set its Team
                Vector3 spawnPos = (assignedTeam == 0) ? new Vector3(-2f, 0f, 0f) : new Vector3(2f, 0f, 0f);
                var no = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
                var playerNet = no.GetComponent<PlayerNetwork>();
                if (playerNet != null)
                {
                    playerNet.Team = assignedTeam;
                }
                Debug.Log($"[DEBUG] Spawned player object for {player} with team {assignedTeam}");
            }

            // Debug: print current mapping (helpful during tests)
            Debug.Log("[DEBUG] Current playerTeam mapping:");
            foreach (var kv in playerTeams)
            {
                Debug.Log($"[DEBUG]  PlayerRef {kv.Key} => Team {kv.Value}");
            }

            // If we reached the max players and want to start the match, we can call:
            int currentPlayers = new List<PlayerRef>(runner.ActivePlayers).Count;
            if (currentPlayers >= maxPlayers)
            {
                if (GamePlayManager.Instance != null)
                {
                    GamePlayManager.Instance.StartMatchOnServer(); // optional: start match
                }
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[DEBUG] OnPlayerLeft: {player}");

            if (!runner.IsServer) return;

            // remove mapping so next join gets fresh assignment (or keep mapping if you want reconnection preserving)
            if (playerTeams.ContainsKey(player))
            {
                playerTeams.Remove(player);
                Debug.Log($"[DEBUG] Removed PlayerRef {player} from playerTeams.");
            }

            // Optional: if you want to keep teams stable across short reconnects, do NOT remove mapping here.

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