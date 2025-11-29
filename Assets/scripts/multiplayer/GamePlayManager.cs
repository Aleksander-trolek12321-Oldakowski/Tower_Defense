using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Networking
{
    public class GamePlayManager : NetworkBehaviour
    {
        public static GamePlayManager Instance;

        [Header("Networked Prefabs (register these in NetworkProjectConfig)")]
        public NetworkObject[] unitPrefabs;
        public NetworkObject[] towerPrefabs;

        [Header("Tower projectiles")]
        public NetworkObject archerProjectilePrefab;
        public NetworkObject cannonProjectilePrefab;

        [Header("Build spots (scene Transforms)")]
        public Transform[] towerSpots;

        private Dictionary<int, bool> spotOccupied = new Dictionary<int, bool>();

        [Header("Economy / Costs")]
        public int[] unitCosts;
        public int[] towerCosts;

        [Header("Enemy Spawn Settings")]
        public Transform[] attackerSpawnPoints;
        public PathManager startPath;
        public float spawnInterval = 0.15f;
        public int[] defaultCounts = new int[] { 4, 10, 1, 2 };

        public Sprite[] enemyTypeSprites;

        private List<Bridge> bridges = new List<Bridge>();

        [Networked] public bool MatchStarted { get; set; } = false;

        void Awake()
        {
            Instance = this;
        }

        public override void Spawned()
        {
            base.Spawned();
            Instance = this;

            spotOccupied.Clear();
            if (towerSpots != null)
            {
                for (int i = 0; i < towerSpots.Length; i++)
                    spotOccupied[i] = false;
            }
        }

        public void StartMatchOnServer()
        {
            if (!Runner.IsServer) return;

            MatchStarted = true;
            Debug.Log("[GamePlayManager] MatchStarted = true (server)");

        }


        public void SpawnWaveByIndex(int typeIndex, Vector2 spawnPos, int team, PlayerRef requester)
        {
            if (!Runner.IsServer) return;

            Debug.Log($"[GamePlayManager] SpawnWaveByIndex: typeIndex={typeIndex}, spawnPos={spawnPos}, team={team}");

            int count = 1;
            if (defaultCounts != null && typeIndex >= 0 && typeIndex < defaultCounts.Length)
                count = Mathf.Max(1, defaultCounts[typeIndex]);

            StartCoroutine(SpawnWaveCoroutine(typeIndex, spawnPos, count, spawnInterval, team, requester));
        }

        private IEnumerator SpawnWaveCoroutine(int typeIndex, Vector2 spawnPos, int count, float interval, int team, PlayerRef requester)
        {
            Debug.Log($"[GamePlayManager] Starting wave coroutine: {count} units of type {typeIndex}");

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = new Vector2(0.2f * (i % 5), 0.2f * (i / 5));
                Vector3 finalPos = (Vector3)(spawnPos + offset);

                SpawnSingleUnit(typeIndex, finalPos, team, requester);
                yield return new WaitForSeconds(interval);
            }

            Debug.Log($"[GamePlayManager] Completed spawning wave of {count} units for player {requester}");
        }

        private void SpawnSingleUnit(int typeIndex, Vector3 spawnPos, int team, PlayerRef requester)
        {
            if (!Runner.IsServer) return;

            if (unitPrefabs == null || typeIndex < 0 || typeIndex >= unitPrefabs.Length)
            {
                Debug.LogWarning($"[GamePlayManager] SpawnSingleUnit invalid index {typeIndex}");
                return;
            }

            var prefab = unitPrefabs[typeIndex];
            if (prefab == null)
            {
                Debug.LogError($"[GamePlayManager] unitPrefabs[{typeIndex}] is null!");
                return;
            }

            try
            {
                var no = Runner.Spawn(prefab, spawnPos, Quaternion.identity, PlayerRef.None);
                if (no == null)
                {
                    Debug.LogError("[GamePlayManager] Runner.Spawn returned null!");
                    return;
                }

                Debug.Log($"[GamePlayManager] Successfully spawned: {no.name} at {spawnPos}");

                var ai = no.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    ai.InitStats((EnemyType)typeIndex);
                    
                    if (startPath != null)
                    {
                        ai.SetPath(startPath);
                        ai.SetInitialNetworkPosition(spawnPos);
                        Debug.Log($"[GamePlayManager] Set path {startPath.name} for enemy");
                    }
                    else
                    {
                        Debug.LogWarning("[GamePlayManager] startPath is null - enemy won't move!");
                    }
                }
                else
                {
                    Debug.LogWarning("[GamePlayManager] Spawned unit has no EnemyAI component");
                }

                var enNet = no.GetComponent<EnemyNetwork>();
                if (enNet != null) 
                {
                    enNet.Team = team;
                    Debug.Log($"[GamePlayManager] Set team to {team}");
                }

                var tv = no.GetComponent<TeamVisibility>();
                if (tv != null) 
                {
                    tv.UpdateVisibility();
                    Debug.Log($"[GamePlayManager] Updated TeamVisibility");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GamePlayManager] Spawn failed with exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void SpawnUnitByIndex(int idx, Vector2 pos, int team, PlayerRef requester)
        {
            if (!Runner.IsServer) return;

            SpawnWaveByIndex(idx, pos, team, requester);
        }

        public void PlaceTowerAtSpot(int towerIndex, int spotId, int team, PlayerRef requester)
        {
            if (Runner == null)
            {
                Debug.LogError("[GamePlayManager.PlaceTowerAtSpot] Runner is NULL! Cannot spawn tower.");
                return;
            }
            if (!Runner.IsServer)
            {
                Debug.LogWarning("[GamePlayManager.PlaceTowerAtSpot] Called on non-server runner. Ignoring. Runner.IsServer=" + Runner.IsServer);
                return;
            }

            Debug.Log($"[GamePlayManager.PlaceTowerAtSpot] ENTER (server? {Runner.IsServer}) towerIndex={towerIndex} spotId={spotId} team={team} requester={requester}");

            if (towerPrefabs == null)
            {
                Debug.LogError("[GamePlayManager.PlaceTowerAtSpot] towerPrefabs array is NULL!");
                return;
            }
            if (towerIndex < 0 || towerIndex >= towerPrefabs.Length)
            {
                Debug.LogError($"[GamePlayManager.PlaceTowerAtSpot] invalid towerIndex {towerIndex}");
                return;
            }
            var prefab = towerPrefabs[towerIndex];
            if (prefab == null)
            {
                Debug.LogError($"[GamePlayManager.PlaceTowerAtSpot] towerPrefabs[{towerIndex}] is NULL (did you assign network prefab in inspector and register it in NetworkProjectConfig?)");
                return;
            }

            if (towerSpots == null || spotId < 0 || spotId >= towerSpots.Length)
            {
                Debug.LogError($"[GamePlayManager.PlaceTowerAtSpot] invalid spotId {spotId}");
                return;
            }

            if (!spotOccupied.ContainsKey(spotId)) spotOccupied[spotId] = false;
            if (spotOccupied[spotId])
            {
                Debug.Log($"[GamePlayManager.PlaceTowerAtSpot] Spot {spotId} already occupied - skipping.");
                return;
            }

            Vector3 pos = towerSpots[spotId].position;

            try
            {
                Debug.Log($"[GamePlayManager.PlaceTowerAtSpot] Spawning prefab {prefab.name} at {pos}");
                var no = Runner.Spawn(prefab, pos, Quaternion.identity, PlayerRef.None);
                if (no == null)
                {
                    Debug.LogError("[GamePlayManager.PlaceTowerAtSpot] Runner.Spawn returned NULL (spawn failed).");
                    return;
                }

                var tw = no.GetComponent<TowerNetwork>();
                if (tw != null) tw.OwnerTeam = team;

                var tv = no.GetComponent<TeamVisibility>();
                if (tv != null) tv.UpdateVisibility();

                spotOccupied[spotId] = true;
                Debug.Log($"[GamePlayManager.PlaceTowerAtSpot] Placed tower idx={towerIndex} at spot {spotId} for team={team} (by {requester}) - spawn succeeded.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[GamePlayManager.PlaceTowerAtSpot] Runner.Spawn threw exception: " + ex.Message + "\n" + ex.StackTrace);
            }
        }


        public int GetUnitCost(int unitIndex)
        {
            if (unitCosts == null || unitIndex < 0 || unitIndex >= unitCosts.Length) return int.MaxValue;
            return unitCosts[unitIndex];
        }

        public int GetTowerCost(int towerIndex)
        {
            if (towerCosts == null || towerIndex < 0 || towerIndex >= towerCosts.Length) return int.MaxValue;
            return towerCosts[towerIndex];
        }

        public void ApplyFreezeToUnitsInRadius(Vector2 pos, float radius, float duration)
        {
            if (!Runner || !Runner.IsServer) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius);
            List<EnemyAI> frozen = new List<EnemyAI>();
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Unit")) continue;
                var ai = hit.GetComponent<EnemyAI>();
                if (ai == null) continue;
                ai.IsFrozen = true;
                frozen.Add(ai);
                Debug.Log($"[GamePlayManager] Freeze applied to {ai.name}");
            }

            if (frozen.Count > 0)
                StartCoroutine(UnfreezeAfterCoroutine(frozen.ToArray(), duration));
        }

        IEnumerator UnfreezeAfterCoroutine(EnemyAI[] ais, float dur)
        {
            yield return new WaitForSeconds(dur);
            foreach (var ai in ais)
            {
                if (ai == null) continue;
                ai.IsFrozen = false;
                Debug.Log($"[GamePlayManager] Freeze removed from {ai.name}");
            }
        }

        public void OnPlayerLeft(PlayerRef player)
        {
            Debug.Log($"[GamePlayManager] OnPlayerLeft: {player}");
        }

        [Networked] public int SelectedBranchIndex { get; set; } = 0;

        public void SetSelectedBranch(int index)
        {
            if (!Runner) return;

            if (Runner.IsServer)
            {
            SelectedBranchIndex = index;
            Debug.Log($"[GamePlayManager] Path index set to {index} (server).");
            }
        else
            {
            RPC_RequestPathChange(index);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPathChange(int index)
        {
            SelectedBranchIndex = index;
            Debug.Log($"[GamePlayManager] Path index changed via RPC → {index}");
        }

        public void RegisterBridge(Bridge bridge)
        {
            if (bridge == null) return;
            if (!bridges.Contains(bridge))
            {
                bridges.Add(bridge);
                bridge.bridgeIndex = bridges.IndexOf(bridge);
                Debug.Log($"[GamePlayManager] Bridge registered: {bridge.name} as index {bridge.bridgeIndex}");
            }
        }

        public void UnregisterBridge(Bridge bridge)
        {
            if (bridge == null) return;
            if (bridges.Contains(bridge))
            {
                bridges.Remove(bridge);
                for (int i = 0; i < bridges.Count; i++)
                    bridges[i].bridgeIndex = i;
            }
        }

        public Bridge GetBridgeByIndex(int index)
        {
            if (index < 0 || index >= bridges.Count) 
            {
                Debug.LogWarning($"[GamePlayManager] Bridge index {index} out of range (0-{bridges.Count-1})");
                return null;
            }
            return bridges[index];
        }
    }
}
