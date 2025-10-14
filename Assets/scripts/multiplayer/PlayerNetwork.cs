using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UI;
using Controller;

namespace Networking
{
    public class PlayerNetwork : NetworkBehaviour
    {
        public static PlayerNetwork Local;

        [Header("Local UI Prefabs (assign prefabs, not scene objects)")]
        public GameObject attackerUIPrefab;
        public GameObject defenderUIPrefab;
        public GameObject waitingUIPrefab;

        // optional: legacy inspector fields (kept for compatibility)
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

        // Networked state
        [Networked] public int Team { get; set; } = -1;
        [Networked] public int Money { get; set; } = 0;

        // local trackers to detect changes
        private int lastObservedTeam = int.MinValue;
        private bool lastObservedMatchStarted = false;

        void Awake()
        {
            // make sure Local cleared on domain reloads
            if (!Application.isPlaying) Local = null;
        }

        public override void Spawned()
        {
            base.Spawned();

            Debug.Log($"[PlayerNetwork] Spawned. IsLocal={Object.HasInputAuthority} Team={Team} Money={Money}");

            // Non-local instances: disable embedded canvases (if any) so they don't block or show UI
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

            // LOCAL player: instantiate UI prefabs (only local)
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

            // ensure canvases are overlay by default to avoid camera/world issues
            AssignCanvasSettingsForLocalUI();

            // instantiate local camera if provided
            if (localCameraPrefab != null && localCameraInstance == null)
            {
                localCameraInstance = Instantiate(localCameraPrefab);
                var cam = localCameraInstance.GetComponent<Camera>();
                if (cam != null)
                {
                    AssignCameraToCanvases(cam);
                }
            }

            // Set local static
            Local = this;

            // initial UI update (maybe Team not assigned yet; Update() will refresh when Team changes)
            UpdateLocalUI();

            // debug runtime button attaching if requested
            if (forceAttachButtonHandlers)
                AttachButtonHandlers();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);

            // destroy local UI instances if exist
            if (attackerUIInstance != null) Destroy(attackerUIInstance);
            if (defenderUIInstance != null) Destroy(defenderUIInstance);
            if (waitingUIInstance != null) Destroy(waitingUIInstance);

            attackerUIInstance = defenderUIInstance = waitingUIInstance = null;
            attackerUI = defenderUI = waitingUI = null;

            if (localCameraInstance != null) Destroy(localCameraInstance);
            if (Local == this) Local = null;

            Debug.Log("[PlayerNetwork] Despawned.");
        }

        void Update()
        {
            // Only local cares about showing UI
            if (!Object.HasInputAuthority) return;

            // detect changes in Team or MatchStarted (networked values may change asynchronously)
            bool currentMatchStarted = (GamePlayManager.Instance != null) ? GamePlayManager.Instance.MatchStarted : false;
            if (lastObservedTeam != Team || lastObservedMatchStarted != currentMatchStarted)
            {
                Debug.Log($"[PlayerNetwork] Team/MatchStarted changed. Team: {lastObservedTeam} -> {Team}, MatchStarted: {lastObservedMatchStarted} -> {currentMatchStarted}");
                lastObservedTeam = Team;
                lastObservedMatchStarted = currentMatchStarted;
                UpdateLocalUI();
            }
        }

        // Ensures instantiated canvases use Overlay and are active
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

            // safety null checks
            if (attackerUI == null) Debug.LogWarning("[PlayerNetwork] attackerUI is null in UpdateLocalUI!");
            if (defenderUI == null) Debug.LogWarning("[PlayerNetwork] defenderUI is null in UpdateLocalUI!");
            if (waitingUI == null) Debug.LogWarning("[PlayerNetwork] waitingUI is null in UpdateLocalUI!");

            // waiting UI visible until match starts
            if (waitingUI != null) waitingUI.SetActive(!started);

            if (!started)
            {
                if (attackerUI != null) attackerUI.SetActive(false);
                if (defenderUI != null) defenderUI.SetActive(false);
                Debug.Log("[PlayerNetwork] Match not started - both team UIs hidden (waiting shown).");
                return;
            }

            // match started -> show only UI for our team
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
                // no team assigned yet -> hide both
                if (attackerUI != null) attackerUI.SetActive(false);
                if (defenderUI != null) defenderUI.SetActive(false);
                Debug.Log("[PlayerNetwork] Team unknown -> hiding both UIs until team assigned.");
            }
        }

        // runtime attach handlers to unit buttons (optional helper)
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

            // attach for build menu - optional
            var buildManager = defenderUI != null ? defenderUI.GetComponentInChildren<BuildMenuManager>(true) : null;
            if (buildManager != null)
            {
                Debug.Log("[PlayerNetwork] Found BuildMenuManager and ready.");
            }
        }

        // called from InteractiveSpot when player taps a build spot
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

        // RPCs unchanged below (kept for server interaction)...
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
    }
}
