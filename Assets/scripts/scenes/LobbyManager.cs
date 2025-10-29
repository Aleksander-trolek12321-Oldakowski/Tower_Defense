using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Networking
{
    public class LobbyManager : NetworkBehaviour
    {
        public static LobbyManager Instance;

        // serwerowa mapa
        private Dictionary<PlayerRef, string> players = new Dictionary<PlayerRef, string>();

        // countdown
        private Coroutine countdownCoroutine;
        private int countdownSeconds = 15;
        private bool countdownRunning = false;

        [Header("Scene to load for match")]
        [Tooltip("Build index of the GameScene (set in File -> Build Settings).")]
        public int gameSceneBuildIndex = 2;

        void Awake() {}

        public override void Spawned()
        {
            base.Spawned();
            Instance = this;
            Debug.Log("[LobbyManager] Spawned.");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            if (Instance == this) Instance = null;
        }

        // Server-side: add player to lobby (call from PlayerNetwork RPC or directly server-side)
        public void Server_AddPlayer(PlayerRef playerRef, string playerName)
        {
            if (!Runner.IsServer) return;

            if (players.ContainsKey(playerRef))
            {
                players[playerRef] = playerName;
            }
            else
            {
                players.Add(playerRef, playerName);
            }

            Debug.Log($"[LobbyManager] Server_AddPlayer: {playerRef} -> {playerName} (total {players.Count})");
            // broadcast updated list
            BroadcastPlayerList();

            // start countdown if enough players
            if (players.Count >= 2 && !countdownRunning)
            {
                countdownCoroutine = StartCoroutine(CountdownCoroutine(countdownSeconds));
            }
        }

        // Server-side: remove player (call from FusionNetworkManager.OnPlayerLeft)
        public void Server_RemovePlayer(PlayerRef playerRef)
        {
            if (!Runner.IsServer) return;

            if (players.ContainsKey(playerRef))
            {
                players.Remove(playerRef);
                Debug.Log($"[LobbyManager] Server_RemovePlayer: removed {playerRef} (total {players.Count})");
            }

            BroadcastPlayerList();

            // cancel countdown if less than 2 players
            if (players.Count < 2 && countdownRunning)
            {
                if (countdownCoroutine != null)
                {
                    StopCoroutine(countdownCoroutine);
                    countdownCoroutine = null;
                }
                countdownRunning = false;
                RPC_CancelCountdown();
            }
        }

        void BroadcastPlayerList()
        {
            // create concatenated string (simple serialization): "name1|name2|..."
            string concat = string.Join("|", players.Values);
            // call client RPC
            RPC_UpdatePlayerList(concat);
        }

        IEnumerator CountdownCoroutine(int seconds)
        {
            countdownRunning = true;
            int remaining = seconds;
            RPC_StartCountdown(remaining); // notify clients initial value

            while (remaining > 0)
            {
                yield return new WaitForSeconds(1f);
                remaining--;
                RPC_UpdateCountdown(remaining);
            }

            countdownRunning = false;
            countdownCoroutine = null;

            Debug.Log("[LobbyManager] Countdown finished - attempting to load game scene and start match (server).");

            if (Runner == null)
            {
                Debug.LogWarning("[LobbyManager] Runner is null - cannot load scene.");
                yield break;
            }

            if (!Runner.IsServer && !Runner.IsSceneAuthority)
            {
                Debug.LogWarning("[LobbyManager] Not server/scene-authority - skipping scene load.");
                RPC_StartMatch();
                yield break;
            }

            var sceneRef = SceneRef.FromIndex(gameSceneBuildIndex);

            try
            {
                Runner.LoadScene(sceneRef, LoadSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyManager] Runner.LoadScene threw: {ex.Message}\n{ex.StackTrace}");
            }

            float waitTimeout = 10f;
            float waited = 0f;
            while (GamePlayManager.Instance == null && waited < waitTimeout)
            {
                yield return null;
                waited += Time.deltaTime;
            }

            if (GamePlayManager.Instance != null)
            {
                Debug.Log("[LobbyManager] GamePlayManager found - starting match on server.");
                GamePlayManager.Instance.StartMatchOnServer();

                // notify clients that match started
                RPC_StartMatch();
            }
            else
            {
                Debug.LogWarning("[LobbyManager] GamePlayManager.Instance is still null after scene load (timeout). Sending StartMatch RPC anyway.");
                RPC_StartMatch();
            }
        }


        // -----------------------
        // RPCs from server -> clients
        // -----------------------
        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        void RPC_UpdatePlayerList(string concatenatedNames)
        {
            // runs on all clients
            var names = string.IsNullOrEmpty(concatenatedNames) ? new string[0] : concatenatedNames.Split('|');
            OnPlayerListUpdated?.Invoke(names);
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        void RPC_StartCountdown(int seconds)
        {
            OnCountdownStarted?.Invoke(seconds);
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        void RPC_UpdateCountdown(int secondsRemaining)
        {
            OnCountdownUpdated?.Invoke(secondsRemaining);
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        void RPC_CancelCountdown()
        {
            OnCountdownCancelled?.Invoke();
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        void RPC_StartMatch()
        {
            OnMatchStarted?.Invoke();
        }

        // Client-side events (UI subscribes)
        public static event Action<string[]> OnPlayerListUpdated;
        public static event Action<int> OnCountdownStarted;
        public static event Action<int> OnCountdownUpdated;
        public static event Action OnCountdownCancelled;
        public static event Action OnMatchStarted;
    }
}
