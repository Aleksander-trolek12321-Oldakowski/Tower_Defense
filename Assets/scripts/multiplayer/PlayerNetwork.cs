using Fusion;
using UnityEngine;
using Controller;

namespace Networking
{
    public class PlayerNetwork : NetworkBehaviour
    {
        // 0 = Defender, 1 = Attacker
        [Networked] public int Team { get; set; } = -1;

        // Local instance helper
        public static PlayerNetwork Local { get; private set; }

        [Header("Local Camera (optional)")]
        public GameObject localCameraPrefab;
        private GameObject localCameraInstance;

        [Header("UI")]
        public GameObject defenderUI;
        public GameObject attackerUI;
        public GameObject waitingUI;

        public override void Spawned()
        {
            base.Spawned();

            // register local instance for easy access
            if (Object.HasInputAuthority)
            {
                Local = this;

                // instantiate local camera if prefab assigned
                if (localCameraPrefab != null && localCameraInstance == null)
                {
                    localCameraInstance = Instantiate(localCameraPrefab);
                    // optional: set follow
                    var follow = localCameraInstance.GetComponent<CameraFollow>();
                    if (follow != null) follow.target = this.transform;
                }
            }

            UpdateLocalUI();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            if (Object.HasInputAuthority)
            {
                // cleanup local camera & Local ref
                if (localCameraInstance != null) Destroy(localCameraInstance);
                if (Local == this) Local = null;
            }
        }

        void Update()
        {
            if (Object.HasInputAuthority)
                UpdateLocalUI();
        }

        void UpdateLocalUI()
        {
            if (!Object.HasInputAuthority) return;
            bool started = GamePlayManager.Instance != null && GamePlayManager.Instance.MatchStarted;
            if (waitingUI) waitingUI.SetActive(!started);
            if (defenderUI) defenderUI.SetActive(started && Team == 0);
            if (attackerUI) attackerUI.SetActive(started && Team == 1);
        }

        // -------------------------
        // RPCs: client -> host (requests)
        // -------------------------
        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPC_RequestSpawnUnit(int unitIndex, Vector2 worldPos, RpcInfo info = default)
        {
            if (!Runner.IsServer) return;
            // validate player's Team on server side (player object for info.Source)
            var srcPlayerRef = info.Source;
            var playerObj = Runner.GetPlayerObject(srcPlayerRef);
            var pn = playerObj != null ? playerObj.GetComponent<PlayerNetwork>() : null;
            if (pn == null || pn.Team != 1) // only Attacker can spawn units
                return;

            // call GamePlayManager to spawn
            GamePlayManager.Instance.SpawnUnitByIndex(unitIndex, worldPos, pn.Team, srcPlayerRef);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPC_RequestPlaceTower(int towerIndex, int spotId, RpcInfo info = default)
        {
            if (!Runner.IsServer) return;
            var srcPlayerRef = info.Source;
            var playerObj = Runner.GetPlayerObject(srcPlayerRef);
            var pn = playerObj != null ? playerObj.GetComponent<PlayerNetwork>() : null;
            if (pn == null || pn.Team != 0) // only Defender can place towers
                return;

            GamePlayManager.Instance.PlaceTowerAtSpot(towerIndex, spotId, pn.Team, srcPlayerRef);
        }
    }
}
