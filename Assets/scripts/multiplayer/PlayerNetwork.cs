using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UI;
using Controller;
using UnityEngine.SceneManagement;

namespace Networking
{
    public class PlayerNetwork : NetworkBehaviour
    {
        public static PlayerNetwork Local;

        [Header("Local UI Prefabs (assign prefabs, not scene objects)")]
        public GameObject attackerUIPrefab;
        public GameObject defenderUIPrefab;
        public GameObject waitingUIPrefab;

        [Header("Optional legacy references (will be set at runtime)")]
        public GameObject attackerUI;
        public GameObject defenderUI;
        public GameObject waitingUI;

        [Header("Camera")]
        public GameObject localCameraPrefab;
        private GameObject localCameraInstance;

        [Header("Debug")]
        public bool forceAttachButtonHandlers = false;

        // runtime instances (only for local player)
        private GameObject attackerUIInstance;
        private GameObject defenderUIInstance;
        private GameObject waitingUIInstance;

        private const string PERSISTENT_UI_ROOT_NAME = "UI_ROOT_PERSISTENT";

        // Networked state
        [Networked] public int Team { get; set; } = -1;
        [Networked] public int Money { get; set; } = 0;

        // local trackers to detect changes
        private int lastObservedTeam = int.MinValue;
        private bool lastObservedMatchStarted = false;

        void Awake()
        {
            if (!Application.isPlaying) Local = null;
	        var existing = FindObjectOfType<PlayerNetwork>();
            if (existing != null && existing != this)
            {
                Destroy(this.gameObject);
                return;
            }
            DontDestroyOnLoad(this.gameObject);
        }

        public override void Spawned()
        {
            base.Spawned();

            Debug.Log($"[PlayerNetwork] Spawned. IsLocal={Object.HasInputAuthority} Team={Team} Money={Money}");

            if (!Object.HasInputAuthority)
            {
                var otherCanvases = GetComponentsInChildren<Canvas>(true);
                foreach (var c in otherCanvases)
                {
                    c.gameObject.SetActive(false);
                }
                Debug.Log($"[PlayerNetwork] Disabled {otherCanvases.Length} embedded canvases on non-local player instance.");
                return;
            }

            Transform uiParent = null;
            var uiRoot = GameObject.Find("UI_ROOT");
            if (uiRoot != null) uiParent = uiRoot.transform;

            if (attackerUIPrefab != null)
            {
                attackerUIInstance = Instantiate(attackerUIPrefab, uiParent);
                attackerUIInstance.name = attackerUIPrefab.name + "_local";
                attackerUI = attackerUIInstance;
            }
            if (defenderUIPrefab != null)
            {
                defenderUIInstance = Instantiate(defenderUIPrefab, uiParent);
                defenderUIInstance.name = defenderUIPrefab.name + "_local";
                defenderUI = defenderUIInstance;
            }
            if (waitingUIPrefab != null)
            {
                waitingUIInstance = Instantiate(waitingUIPrefab, uiParent);
                waitingUIInstance.name = waitingUIPrefab.name + "_local";
                waitingUI = waitingUIInstance;
            }

            AssignCanvasSettingsForLocalUI();

            if (localCameraPrefab != null && localCameraInstance == null)
            {
                localCameraInstance = Instantiate(localCameraPrefab);
                var cam = localCameraInstance.GetComponent<Camera>();
                if (cam != null)
                {
                    AssignCameraToCanvases(cam);
                }
            }

            Local = this;

            if (Object.HasInputAuthority && SceneManager.GetActiveScene().name == "Lobby")
            {
                // odroczenie wywołania dołączania do lobby, aby server zdążył zarejestrować obiekt i lobby był gotowy
                StartCoroutine(DelayedJoinLobbyCoroutine());
            }

            UpdateLocalUI();

            if (forceAttachButtonHandlers)
                AttachButtonHandlers();

            // subscribe to sceneLoaded so we can recreate UI/cam after scene changes
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // delay join lobby a bit (race conditions) - will internally wait for LobbyManager if needed
            StartCoroutine(DelayedJoinLobbyCoroutine());
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);

            if (attackerUIInstance != null) Destroy(attackerUIInstance);
            if (defenderUIInstance != null) Destroy(defenderUIInstance);
            if (waitingUIInstance != null) Destroy(waitingUIInstance);

            attackerUIInstance = defenderUIInstance = waitingUIInstance = null;

            attackerUI = defenderUI = waitingUI = null;

            if (localCameraInstance != null) Destroy(localCameraInstance);
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (Local == this) Local = null;

            Debug.Log("[PlayerNetwork] Despawned.");
        }

        private System.Collections.IEnumerator DelayedJoinLobbyCoroutine()
        {
            
            yield return new WaitForSeconds(0.1f);

            float timeout = 5f;
            float t = 0f;
            while (Networking.LobbyManager.Instance == null && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }

            string playerName = $"Player{Runner.LocalPlayer}";

            Debug.Log($"[PlayerNetwork] Requesting join lobby as {playerName}");
            RPC_RequestJoinLobby(playerName);
        }

        void Update()
        {
            if (!Object.HasInputAuthority) return;

            bool currentMatchStarted = (GamePlayManager.Instance != null) ? GamePlayManager.Instance.MatchStarted : false;
            if (lastObservedTeam != Team || lastObservedMatchStarted != currentMatchStarted)
            {
                Debug.Log($"[PlayerNetwork] Team/MatchStarted changed. Team: {lastObservedTeam} -> {Team}, MatchStarted: {lastObservedMatchStarted} -> {currentMatchStarted}");
                lastObservedTeam = Team;
                lastObservedMatchStarted = currentMatchStarted;
                UpdateLocalUI();
            }
        }

        void AssignCanvasSettingsForLocalUI()
        {
            List<Canvas> canvases = new List<Canvas>();
            if (attackerUIInstance != null) canvases.AddRange(attackerUIInstance.GetComponentsInChildren<Canvas>(true));
            if (defenderUIInstance != null) canvases.AddRange(defenderUIInstance.GetComponentsInChildren<Canvas>(true));
            if (waitingUIInstance != null) canvases.AddRange(waitingUIInstance.GetComponentsInChildren<Canvas>(true));

            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.gameObject.SetActive(true);
            }

            Debug.Log($"[PlayerNetwork] Assigned {canvases.Count} local canvases to Overlay and activated them.");
        }

        void AssignCameraToCanvases(Camera cam)
        {
            if (cam == null) return;
            var canvases = GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceCamera || c.renderMode == RenderMode.WorldSpace)
                {
                    c.worldCamera = cam;
                }
            }
            Debug.Log("[PlayerNetwork] Assigned local camera to prefab canvases (if any).");
        }

        public void UpdateLocalUI()
        {
            if (!Object.HasInputAuthority) return;

            bool started = (GamePlayManager.Instance != null) ? GamePlayManager.Instance.MatchStarted : false;

            if (attackerUI == null) Debug.LogWarning("[PlayerNetwork] attackerUI is null in UpdateLocalUI!");
            if (defenderUI == null) Debug.LogWarning("[PlayerNetwork] defenderUI is null in UpdateLocalUI!");
            if (waitingUI == null) Debug.LogWarning("[PlayerNetwork] waitingUI is null in UpdateLocalUI!");

            if (waitingUI != null) waitingUI.SetActive(!started);

            if (!started)
            {
                if (attackerUI != null) attackerUI.SetActive(false);
                if (defenderUI != null) defenderUI.SetActive(false);
                Debug.Log("[PlayerNetwork] Match not started - both team UIs hidden (waiting shown).");
                return;
            }

            if (Team == 0)
            {
                if (defenderUI != null) defenderUI.SetActive(true);
                if (attackerUI != null) attackerUI.SetActive(false);
                Debug.Log("[PlayerNetwork] Showing DEFENDER UI, hiding ATTACKER UI.");
            }
            else if (Team == 1)
            {
                if (attackerUI != null) attackerUI.SetActive(true);
                if (defenderUI != null) defenderUI.SetActive(false);
                Debug.Log("[PlayerNetwork] Showing ATTACKER UI, hiding DEFENDER UI.");
            }
            else
            {
                if (attackerUI != null) attackerUI.SetActive(false);
                if (defenderUI != null) defenderUI.SetActive(false);
                Debug.Log("[PlayerNetwork] Team unknown -> hiding both UIs until team assigned.");
            }
        }

        public void AttachButtonHandlers()
        {
            if (attackerUI != null)
            {
                var panelManager = attackerUI.GetComponentInChildren<AttackerUnitPanelManager>(true);
                if (panelManager != null && panelManager.unitButtons != null)
                {
                    for (int i = 0; i < panelManager.unitButtons.Length; i++)
                    {
                        int idx = i;
                        var btn = panelManager.unitButtons[i];
                        if (btn == null) continue;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() =>
                        {
                            Debug.Log($"[PlayerNetwork] (runtime) attacker button {idx} clicked");
                            panelManager.OnUnitButtonClickedRuntime(idx);
                        });
                    }
                }
            }

            var buildManager = defenderUI != null ? defenderUI.GetComponentInChildren<BuildMenuManager>(true) : null;
            if (buildManager != null)
            {
                Debug.Log("[PlayerNetwork] Found BuildMenuManager and ready.");
            }
        }

        public void OpenBuildMenu(int spotId)
        {
            if (!Object.HasInputAuthority)
            {
                Debug.Log("[PlayerNetwork] OpenBuildMenu: not local player, ignoring.");
                return;
            }
            if (Team != 0)
            {
                Debug.Log("[PlayerNetwork] OpenBuildMenu denied: not a defender.");
                return;
            }
            if (defenderUI == null)
            {
                Debug.LogWarning("[PlayerNetwork] OpenBuildMenu: defenderUI is null!");
                return;
            }

            var buildMenu = defenderUI.GetComponentInChildren<BuildMenuManager>(true);
            if (buildMenu == null)
            {
                Debug.LogWarning("[PlayerNetwork] OpenBuildMenu: no BuildMenuManager found under defenderUI!");
                return;
            }

            Debug.Log($"[PlayerNetwork] Opening build menu for spot {spotId}");
            buildMenu.Open(spotId);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPC_RequestJoinLobby(string playerName, RpcInfo info = default)
        {
            if (!Runner.IsServer) 
            {
                Debug.LogWarning("[RPC_RequestJoinLobby] Not running on server - ignoring.");
                return;
            }

            var src = info.Source;
            NetworkObject playerObj = null;
            try
            {
                playerObj = Runner.GetPlayerObject(src);
            }
            catch { playerObj = null; }

            if (playerObj == null)
            {
                try
                {
                    if (FusionNetworkManager.PlayerObjects != null)
                    {
                        FusionNetworkManager.PlayerObjects.TryGetValue(src, out playerObj);
                    }
                }
                catch { /* ignore */ }
            }

            PlayerRef resolvedRef = src; 
            if (playerObj == null)
            {
                if (this != null && this.Object != null)
                {
                    playerObj = this.Object;

                    try
                    {
                        var map = Networking.FusionNetworkManager.PlayerObjects;
                        if (map != null)
                        {
                            foreach (var kv in map)
                            {
                                if (kv.Value == playerObj)
                                {
                                    resolvedRef = kv.Key;
                                    break;
                                }
                            }
                        }
                    }
                    catch { /* ignore */ }
                }
            }

            if (playerObj == null && resolvedRef != PlayerRef.None)
            {
                try
                {
                    playerObj = Runner.GetPlayerObject(resolvedRef);
                }
                catch { playerObj = null; }
            }

            if (playerObj == null)
            {
                Debug.LogWarning($"[RPC_RequestJoinLobby] Could not find player object for RPC source {src} (resolved {resolvedRef}). Aborting join.");
                return;
            }

            var pn = playerObj.GetComponent<PlayerNetwork>();
            if (pn == null)
            {
                Debug.LogWarning("[RPC_RequestJoinLobby] found object has no PlayerNetwork component.");
                return;
            }

            if (resolvedRef == PlayerRef.None)
            {
                try
                {
                    var inputAuth = playerObj.InputAuthority;
                    resolvedRef = inputAuth;
                }
                catch { /* ignore */ }
            }

            if (LobbyManager.Instance != null)
            {
                Debug.Log($"[RPC_RequestJoinLobby] Registering player '{playerName}' as {resolvedRef}");
                LobbyManager.Instance.Server_AddPlayer(resolvedRef, playerName);
            }
            else
            {
                Debug.LogWarning("[RPC_RequestJoinLobby] LobbyManager.Instance is null on server.");
            }
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPC_RequestSpawnUnit(int unitIndex, Vector2 worldPos, RpcInfo info = default)
        {
            if (!Runner.IsServer) return;

            var srcPlayerRef = info.Source;
            var playerObj = Runner.GetPlayerObject(srcPlayerRef);
            var pn = playerObj != null ? playerObj.GetComponent<PlayerNetwork>() : null;
            if (pn == null)
            {
                Debug.Log("[RPC_RequestSpawnUnit] sender has no PlayerNetwork");
                return;
            }

            if (pn.Team != 1)
            {
                Debug.Log("[RPC_RequestSpawnUnit] denied: not attacker");
                return;
            }

            int cost = GamePlayManager.Instance != null ? GamePlayManager.Instance.GetUnitCost(unitIndex) : int.MaxValue;
            if (pn.Money < cost)
            {
                Debug.Log($"[RPC_RequestSpawnUnit] denied: not enough money (have {pn.Money}, need {cost})");
                return;
            }

            pn.Money -= cost;
            GamePlayManager.Instance.SpawnUnitByIndex(unitIndex, worldPos, pn.Team, srcPlayerRef);

            Debug.Log($"[RPC_RequestSpawnUnit] spawned unit {unitIndex} at {worldPos} for player {srcPlayerRef}. Money left: {pn.Money}");
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPC_RequestPlaceTower(int towerIndex, int spotId, RpcInfo info = default)
        {
            if (!Runner.IsServer) return;
            // (maintain original behavior)
            var src = info.Source;
            var playerObj = Runner.GetPlayerObject(src);
            var pn = playerObj != null ? playerObj.GetComponent<PlayerNetwork>() : null;
            if (pn == null) return;

            if (pn.Team != 0)
            {
                Debug.Log("[RPC_RequestPlaceTower] denied: not defender");
                return;
            }

            int cost = GamePlayManager.Instance != null ? GamePlayManager.Instance.GetTowerCost(towerIndex) : int.MaxValue;
            if (pn.Money < cost)
            {
                Debug.Log($"[RPC_RequestPlaceTower] denied: not enough money ({pn.Money} < {cost})");
                return;
            }

            pn.Money -= cost;
            GamePlayManager.Instance.PlaceTowerAtSpot(towerIndex, spotId, pn.Team, src);

            Debug.Log($"[RPC_RequestPlaceTower] placed tower {towerIndex} at spot {spotId} for player {src}. Money left: {pn.Money}");
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPC_RequestUseAbility(int abilityId, Vector2 worldPos, RpcInfo info = default)
        {
            if (!Runner.IsServer)
            {
                Debug.Log("[RPC_RequestUseAbility] Not server - ignoring.");
                return;
            }

            var src = info.Source;

            // Najpierw spróbuj standardowego API
            NetworkObject playerObj = null;
            try
            {
                playerObj = Runner.GetPlayerObject(src);
            }
            catch
            {
                playerObj = null;
            }

            // Fallback: sprawdź mapę utrzymywaną przez FusionNetworkManager
            if (playerObj == null)
            {
                if (Networking.FusionNetworkManager.PlayerObjects != null)
                {
                    Networking.FusionNetworkManager.PlayerObjects.TryGetValue(src, out playerObj);
                }
            }

            var pn = playerObj != null ? playerObj.GetComponent<PlayerNetwork>() : null;
            if (pn == null)
            {
                Debug.Log("[RPC_RequestUseAbility] sender has no PlayerNetwork");
                return;
            }

            int team = pn.Team;
            bool allowed = false;
            if (abilityId >= 0 && abilityId <= 2 && team == 1) allowed = true;
            if (abilityId >= 3 && abilityId <= 5 && team == 0) allowed = true;

            if (!allowed)
            {
                Debug.Log($"[RPC_RequestUseAbility] denied: team {team} cannot use ability {abilityId}");
                return;
            }

            if (GamePlayManager.Instance != null)
            {
                GamePlayManager.Instance.Server_HandleAbilityRequest(abilityId, worldPos, src);
            }
            else
            {
                Debug.LogWarning("[RPC_RequestUseAbility] GamePlayManager.Instance == null");
            }
        }

        // -----------------------
        // Scene / UI persistence helpers
        // -----------------------
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Object.HasInputAuthority) return;
            EnsureLocalUIAndCamera();
        }

        private void EnsurePersistentUIRoot()
        {
            var existing = GameObject.Find(PERSISTENT_UI_ROOT_NAME);
            if (existing == null)
            {
                var go = new GameObject(PERSISTENT_UI_ROOT_NAME);
                DontDestroyOnLoad(go);
            }
        }

        private void EnsureLocalUIAndCamera()
        {
            GameObject uiRoot = GameObject.Find(PERSISTENT_UI_ROOT_NAME);

            if (attackerUIInstance == null && attackerUIPrefab != null)
            {
                attackerUIInstance = Instantiate(attackerUIPrefab, uiRoot != null ? uiRoot.transform : null);
                attackerUIInstance.name = attackerUIPrefab.name + "_local";
                attackerUI = attackerUIInstance;
            }

            if (defenderUIInstance == null && defenderUIPrefab != null)
            {
                defenderUIInstance = Instantiate(defenderUIPrefab, uiRoot != null ? uiRoot.transform : null);
                defenderUIInstance.name = defenderUIPrefab.name + "_local";
                defenderUI = defenderUIInstance;
            }

            if (waitingUIInstance == null && waitingUIPrefab != null)
            {
                waitingUIInstance = Instantiate(waitingUIPrefab, uiRoot != null ? uiRoot.transform : null);
                waitingUIInstance.name = waitingUIPrefab.name + "_local";
                waitingUI = waitingUIInstance;
            }

            if (localCameraInstance == null && localCameraPrefab != null)
            {
                localCameraInstance = Instantiate(localCameraPrefab);
                var cam = localCameraInstance.GetComponent<Camera>();
                if (cam != null)
                {
                    AssignCameraToCanvases(cam);
                }
            }

            AssignCanvasSettingsForLocalUI();
        }


        // Other callbacks and stubs unchanged...
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    }
}
