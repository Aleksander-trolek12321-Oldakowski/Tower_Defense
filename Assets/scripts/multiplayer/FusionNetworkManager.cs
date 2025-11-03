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

        public static Dictionary<PlayerRef, NetworkObject> PlayerObjects = new Dictionary<PlayerRef, NetworkObject>();
        private Dictionary<PlayerRef, int> playerTeams = new Dictionary<PlayerRef, int>();

        private void Awake()
        {
            // Limit application frame rate for mobile stability
            Application.targetFrameRate = 60;

            var existing = FindObjectOfType<FusionNetworkManager>();
            if (existing != null && existing != this)
            {
                Destroy(this.gameObject);
                return;
            }
            DontDestroyOnLoad(this.gameObject);
        }

        async void Start()
        {
            if (runnerPrefab == null)
            {
                Debug.LogError("[FusionNetworkManager] runnerPrefab is not assigned in inspector!");
                return;
            }

            // Instantiate and configure the NetworkRunner
            runner = Instantiate(runnerPrefab);
            runner.ProvideInput = true;

            // Register callbacks BEFORE starting the runner (safer)
            runner.AddCallbacks(this);

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
                Scene = sceneInfo,
                SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                PlayerCount = maxPlayers
            };

            try
            {
                await runner.StartGame(args);
                Debug.Log("[FusionNetworkManager] Runner started.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FusionNetworkManager] StartGame failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // -------------------------
        // Core callbacks
        // -------------------------
        public void OnPlayerJoined(NetworkRunner runnerRef, PlayerRef player)
        {
            Debug.Log($"[DEBUG] OnPlayerJoined: {player}");

            // Only the host/server should assign teams and spawn players
            if (!runnerRef.IsServer)
            {
                Debug.Log("[DEBUG] OnPlayerJoined: not server, skipping server-side assignment.");
                return;
            }

            // ensure playerPrefab assigned
            if (playerPrefab == null)
            {
                Debug.LogError("[FusionNetworkManager] playerPrefab is NULL in inspector! Cannot spawn player object.");
                return;
            }

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
                    assignedTeam = UnityEngine.Random.Range(0, 2);
                }

                // store assignment
                playerTeams[player] = assignedTeam;
                Debug.Log($"[DEBUG] Assigned PlayerRef {player} -> team {assignedTeam} (stored in playerTeams).");
            }

            // Try reuse existing player object if present
            var existingObject = runnerRef.GetPlayerObject(player);
            if (existingObject != null)
            {
                var pnExisting = existingObject.GetComponent<PlayerNetwork>();
                if (pnExisting != null)
                {
                    pnExisting.Team = assignedTeam;
                    Debug.Log($"[DEBUG] Updated existing player object for {player} team -> {assignedTeam}");
                    pnExisting.Money = 100;
                    PlayerObjects[player] = existingObject;
                }
                else
                {
                    Debug.LogWarning("[FusionNetworkManager] existing player object found but it lacks PlayerNetwork component.");
                }
            }
            else
            {
                // spawn player avatar for this player and set its Team
                Vector3 spawnPos = (assignedTeam == 0) ? new Vector3(-2f, 0f, 0f) : new Vector3(2f, 0f, 0f);
                NetworkObject no = null;
                try
                {
                    no = runnerRef.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FusionNetworkManager] runner.Spawn threw exception: {ex.Message}\n{ex.StackTrace}");
                }

                if (no == null)
                {
                    Debug.LogError("[FusionNetworkManager] runner.Spawn returned null for player object - aborting spawn logic.");
                }
                else
                {
                    var playerNet = no.GetComponent<PlayerNetwork>();
                    if (playerNet != null)
                    {
                        playerNet.Team = assignedTeam;
                        playerNet.Money = 100;
                        PlayerObjects[player] = no;
                        Debug.Log($"[DEBUG] Spawned player object for {player} with team {assignedTeam} and Money=100");
                    }
                    else
                    {
                        Debug.LogWarning("[FusionNetworkManager] spawned player object has no PlayerNetwork component.");
                    }
                }
            }

            // Debug: print current mapping (helpful during tests)
            Debug.Log("[DEBUG] Current playerTeam mapping:");
            foreach (var kv in playerTeams)
            {
                Debug.Log($"[DEBUG]  PlayerRef {kv.Key} => Team {kv.Value}");
            }

            // If we reached the max players and want to start the match, we can call:
            int currentPlayers = 0;
            try
            {
                currentPlayers = new List<PlayerRef>(runnerRef.ActivePlayers).Count;
            }
            catch
            {
                currentPlayers = playerTeams.Count;
            }

            if (currentPlayers >= maxPlayers)
            {
                if (GamePlayManager.Instance != null)
                {
                    Debug.Log("[FusionNetworkManager] Enough players - calling GamePlayManager.StartMatchOnServer()");
                    GamePlayManager.Instance.StartMatchOnServer();
                }
                else
                {
                    Debug.LogWarning("[FusionNetworkManager] Enough players but GamePlayManager.Instance is NULL - cannot start match.");
                }
            }
        }

        public void OnPlayerLeft(NetworkRunner runnerRef, PlayerRef player)
        {
            Debug.Log($"[DEBUG] OnPlayerLeft: {player}");

            if (!runnerRef.IsServer) return;

            if (playerTeams.ContainsKey(player))
            {
                playerTeams.Remove(player);
                Debug.Log($"[DEBUG] Removed PlayerRef {player} from playerTeams.");
            }

            if (PlayerObjects.ContainsKey(player))
            {
                PlayerObjects.Remove(player);
                Debug.Log($"[DEBUG] Removed PlayerRef {player} from PlayerObjects map.");
            }

            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.Server_RemovePlayer(player);
            }

            GamePlayManager.Instance?.OnPlayerLeft(player);
        }

        public void OnSceneLoadDone(NetworkRunner runnerRef)
        {
            if (!runnerRef.IsServer) return;

            Debug.Log("[FusionNetworkManager] OnSceneLoadDone: ensuring player objects exist...");

            var activePlayers = new List<PlayerRef>(runnerRef.ActivePlayers);
            foreach (var p in activePlayers)
            {
                NetworkObject existing = null;
                try { existing = runnerRef.GetPlayerObject(p); } catch { existing = null; }

                if (existing != null)
                {
                    Debug.Log($"[FusionNetworkManager] Player object for {p} already exists - skipping.");
                    continue;
                }

                int assignedTeam = -1;
                try { if (playerTeams != null && playerTeams.TryGetValue(p, out var t)) assignedTeam = t; } catch { assignedTeam = -1; }
                if (assignedTeam == -1) assignedTeam = UnityEngine.Random.Range(0, 2);

                Vector3 spawnPos = (assignedTeam == 0) ? new Vector3(-2f, 0f, 0f) : new Vector3(2f, 0f, 0f);

                NetworkObject spawned = null;
                try
                {
                    spawned = runnerRef.Spawn(playerPrefab, spawnPos, Quaternion.identity, p);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FusionNetworkManager] Exception while spawning player for {p}: {ex.Message}");
                }

                if (spawned == null)
                {
                    Debug.LogError($"[FusionNetworkManager] Failed to spawn player object for {p} (returned null). Check playerPrefab registration in NetworkProjectConfig and inspector.");
                    continue;
                }

                var pn = spawned.GetComponent<PlayerNetwork>();
                if (pn != null)
                {
                    pn.Team = assignedTeam;
                    pn.Money = 100;
                    Debug.Log($"[FusionNetworkManager] Spawned player object for {p} with team {assignedTeam} and Money=100");
                }
                else
                {
                    Debug.LogWarning("[FusionNetworkManager] Spawned player object lacks PlayerNetwork component.");
                }

                try { if (playerTeams != null && !playerTeams.ContainsKey(p)) playerTeams[p] = assignedTeam; } catch { }
            }

            StartCoroutine(WaitForGamePlayManagerAndStartMatchCoroutine());
        }

        private System.Collections.IEnumerator WaitForGamePlayManagerAndStartMatchCoroutine()
        {
            float timeout = 5f;
            float t = 0f;

            Debug.Log("[FusionNetworkManager] Waiting for GamePlayManager.Instance...");

            while (t < timeout)
            {
                if (GamePlayManager.Instance != null) break;
                t += Time.deltaTime;
                yield return null;
            }

            if (GamePlayManager.Instance == null)
            {
                Debug.LogWarning("[FusionNetworkManager] GamePlayManager not found after scene load (timeout). Match will not auto-start.");
                yield break;
            }

            if (runner == null)
            {
                Debug.LogWarning("[FusionNetworkManager] runner reference is null in WaitForGamePlayManagerAndStartMatchCoroutine.");
                yield break;
            }

            if (!runner.IsServer)
            {
                Debug.Log("[FusionNetworkManager] Not server in WaitForGamePlayManagerAndStartMatchCoroutine - abort.");
                yield break;
            }

            int currentPlayers = 0;
            try { currentPlayers = new List<PlayerRef>(runner.ActivePlayers).Count; } catch { currentPlayers = playerTeams != null ? playerTeams.Count : 0; }

            Debug.Log($"[FusionNetworkManager] After scene load: currentPlayers={currentPlayers}, maxPlayers={maxPlayers}");

            if (currentPlayers >= maxPlayers)
            {
                Debug.Log("[FusionNetworkManager] Enough players - calling GamePlayManager.StartMatchOnServer()");
                GamePlayManager.Instance.StartMatchOnServer();
            }
            else
            {
                Debug.Log($"[FusionNetworkManager] Not enough players to start match (have {currentPlayers}, need {maxPlayers}).");
            }
        }

        // -------------------------
        // Other required callbacks (stubs / logs)
        // -------------------------
        public void OnInput(NetworkRunner runner, NetworkInput input) { /* input handling if needed */ }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runnerRef, ShutdownReason reason)
        {
            Debug.Log($"Runner shutdown: {reason}");
        }

        public void OnConnectedToServer(NetworkRunner runnerRef)
        {
            Debug.Log("Connected to server");
        }

        public void OnDisconnectedFromServer(NetworkRunner runnerRef, NetDisconnectReason reason)
        {
            Debug.Log($"Disconnected: {reason}");
        }

        public void OnConnectFailed(NetworkRunner runnerRef, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.Log($"Connect failed to {remoteAddress}: {reason}");
        }

        public void OnReliableDataReceived(NetworkRunner runnerRef, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

        public void OnSceneLoadStart(NetworkRunner runnerRef) { }

        public void OnSessionListUpdated(NetworkRunner runnerRef, List<SessionInfo> sessionList) { }

        public void OnConnectRequest(NetworkRunner runnerRef, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        public void OnUserSimulationMessage(NetworkRunner runnerRef, SimulationMessagePtr message) { }

        public void OnObjectEnterAOI(NetworkRunner runnerRef, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runnerRef, NetworkObject obj, PlayerRef player) { }

        public void OnCustomAuthenticationResponse(NetworkRunner runnerRef, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runnerRef, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataProgress(NetworkRunner runnerRef, PlayerRef player, ReliableKey key, float progress) { }
    }
}
