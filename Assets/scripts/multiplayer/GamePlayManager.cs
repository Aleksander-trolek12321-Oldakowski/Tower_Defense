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

        [Header("Ability network prefabs (register in NetworkProjectConfig)")]
        public NetworkObject fireballNetworkPrefab;
        public NetworkObject freezeAreaNetworkPrefab;

        [Header("Tower projectiles")]
        public NetworkObject archerProjectilePrefab;
        public NetworkObject cannonProjectilePrefab;

        [Header("Build spots (scene Transforms)")]
        public Transform[] towerSpots;

        private Dictionary<int, bool> spotOccupied = new Dictionary<int, bool>();

        [Header("Attacker starting units")]
        public Transform[] attackerSpawnPoints;
        public int[] initialUnitsPerSpawnPoint;

        [Header("Economy / Costs")]
        public int[] unitCosts;
        public int[] towerCosts;

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

            SpawnInitialUnitsForTeam(1);
        }

        public void SpawnInitialUnitsForTeam(int team)
        {
            if (!Runner.IsServer) return;
            if (team != 1) return;

            if (attackerSpawnPoints == null || attackerSpawnPoints.Length == 0)
            {
                Debug.Log("[GamePlayManager] No attackerSpawnPoints assigned - skipping initial spawn.");
                return;
            }

            if (unitPrefabs == null || unitPrefabs.Length == 0)
            {
                Debug.LogWarning("[GamePlayManager] No unitPrefabs assigned - cannot spawn initial units.");
                return;
            }

            for (int i = 0; i < attackerSpawnPoints.Length; i++)
            {
                Vector3 pos = attackerSpawnPoints[i].position;
                int unitIndex = 0;

                int spawnCount = 1;
                if (initialUnitsPerSpawnPoint != null && i < initialUnitsPerSpawnPoint.Length)
                    spawnCount = Mathf.Max(1, initialUnitsPerSpawnPoint[i]);

                for (int j = 0; j < spawnCount; j++)
                {
                    Vector3 spawnPos = pos + new Vector3(0.2f * j, 0f, 0f);
                    var no = Runner.Spawn(unitPrefabs[unitIndex], spawnPos, Quaternion.identity, PlayerRef.None);
                    var en = no.GetComponent<EnemyNetwork>();
                    if (en != null) en.Team = team;

                    var tv = no.GetComponent<TeamVisibility>();
                    if (tv != null) tv.UpdateVisibility();
                }
            }

            Debug.Log("[GamePlayManager] SpawnInitialUnitsForTeam executed for team " + team);
        }

        public void SpawnUnitByIndex(int idx, Vector2 pos, int team, PlayerRef requester)
        {
            if (!Runner.IsServer) return;

            if (unitPrefabs == null || idx < 0 || idx >= unitPrefabs.Length)
            {
                Debug.LogWarning($"[GamePlayManager] SpawnUnitByIndex invalid index {idx}");
                return;
            }

            Vector3 spawnPos = new Vector3(pos.x, pos.y, 0f);
            var no = Runner.Spawn(unitPrefabs[idx], spawnPos, Quaternion.identity, PlayerRef.None);
            var en = no.GetComponent<EnemyNetwork>();
            if (en != null) en.Team = team;

            var tv = no.GetComponent<TeamVisibility>();
            if (tv != null) tv.UpdateVisibility();

            Debug.Log($"[GamePlayManager] Spawned unit idx={idx} at {pos} for team={team} requested by {requester}");
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
                ai.IsFrozen = true; // networked -> replicuje
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

        // -------------------------
        // NEW: Server-side ability handling (entrypoint for RPC from clients)
        // abilityId map (internal convention):
        // 0 = DamageBoost (attacker)
        // 1 = FreezeTurret (attacker - targets turret at worldPos)
        // 2 = SpeedBoost (attacker)
        // 3 = Fireball (defender - area at worldPos)
        // 4 = FreezeArea (defender - area at worldPos)
        // 5 = TurretBoost (defender)
        // -------------------------
        public void Server_HandleAbilityRequest(int abilityId, Vector2 worldPos, PlayerRef requester)
        {
            if (!Runner.IsServer) return;

            Debug.Log($"[GamePlayManager] Server_HandleAbilityRequest ability={abilityId} pos={worldPos} from {requester}");

            switch (abilityId)
            {
                case 0: // DamageBoost
                    var dmgSkill = FindObjectOfType<DamageBoostSkill>();
                    if (dmgSkill != null)
                        dmgSkill.OnButtonPress();
                    else
                        StartCoroutine_CastDamageBoost(1, 1.5f, 5f);
                    break;

                case 1: // FreezeTurret (target at worldPos)
                    FreezeTurretAtPosition(worldPos, 4f);
                    break;

                case 2: // SpeedBoost
                    var spdSkill = FindObjectOfType<SpeedBoostSkill>();
                    if (spdSkill != null)
                        spdSkill.OnButtonPress();
                    else
                        StartCoroutine_CastSpeedBoost(1, 1.5f, 5f);
                    break;

                case 3: // Fireball (spawn networked)
                    SpawnNetworkedFireball(worldPos);
                    break;

                case 4: // FreezeArea (spawn networked visual + apply)
                    SpawnNetworkedFreezeAreaAndApply(worldPos, 3f, 5f);
                    break;

                case 5: // TurretBoost
                    var tBoost = FindObjectOfType<TurretBoostSkill>();
                    if (tBoost != null)
                        tBoost.OnButtonPress();
                    else
                        StartCoroutine_CastTurretBoost(0, 1.5f, 5f);
                    break;

                default:
                    Debug.LogWarning("[GamePlayManager] Unknown abilityId " + abilityId);
                    break;
            }
        }

        // --- Networked spawn helpers ---
        void SpawnNetworkedFireball(Vector2 targetPos)
        {
            if (fireballNetworkPrefab == null)
            {
                Debug.LogWarning("[GamePlayManager] fireballNetworkPrefab not set - cannot spawn networked fireball");
                return;
            }

            Vector3 spawnPos = (Vector3)(targetPos + Vector2.up * 5f);
            try
            {
                var no = Runner.Spawn(fireballNetworkPrefab, spawnPos, Quaternion.identity, PlayerRef.None);
                var rb = no.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 dir = (targetPos - (Vector2)no.transform.position).normalized;
                    rb.velocity = dir * 10f;
                }
                Debug.Log("[GamePlayManager] Spawned networked fireball at " + spawnPos);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GamePlayManager] Runner.Spawn fireball failed: " + ex.Message);
            }
        }

        void SpawnNetworkedFreezeAreaAndApply(Vector2 pos, float radius, float duration)
        {
            if (freezeAreaNetworkPrefab == null)
            {
                Debug.LogWarning("[GamePlayManager] freezeAreaNetworkPrefab not set - cannot spawn networked freeze area");
                FreezeUnitsInRadius(pos, radius, duration);
                return;
            }

            try
            {
                var no = Runner.Spawn(freezeAreaNetworkPrefab, (Vector3)pos, Quaternion.identity, PlayerRef.None);
                if (no != null)
                    no.transform.localScale = Vector3.one * (radius * 2f);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GamePlayManager] Runner.Spawn freezeArea failed: " + ex.Message);
            }

            FreezeUnitsInRadius(pos, radius, duration);
        }

        // --- Effects implementations (server-authoritative) ---
        void FreezeTurretAtPosition(Vector2 pos, float duration)
        {
            RaycastHit2D hit = Physics2D.Raycast(pos, Vector2.zero);
            if (hit.collider != null && hit.collider.CompareTag("Turret"))
            {
                var turret = hit.collider.gameObject;
                Debug.Log($"[GamePlayManager] Server: Freezing turret {turret.name} for {duration}s");
                var rb = turret.GetComponent<Rigidbody2D>();
                if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeAll;
                MonoBehaviour[] scripts = turret.GetComponents<MonoBehaviour>();
                foreach (var s in scripts) if (s != null) s.enabled = false;

                StartCoroutine(UnfreezeTurretAfter(turret, duration));
            }
            else
            {
                Debug.Log("[GamePlayManager] FreezeTurretAtPosition: no turret found at pos");
            }
        }

        IEnumerator UnfreezeTurretAfter(GameObject turret, float d)
        {
            yield return new WaitForSeconds(d);
            if (turret == null) yield break;
            var rb = turret.GetComponent<Rigidbody2D>();
            if (rb != null) rb.constraints = RigidbodyConstraints2D.None;
            MonoBehaviour[] scripts = turret.GetComponents<MonoBehaviour>();
            foreach (var s in scripts) if (s != null) s.enabled = true;
            Debug.Log($"[GamePlayManager] Turret {turret.name} unfrozen");
        }

        void FreezeUnitsInRadius(Vector2 pos, float radius, float duration)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Unit"))
                {
                    Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.velocity = Vector2.zero;
                        rb.isKinematic = true;
                    }

                    MonoBehaviour ai = hit.GetComponent<MonoBehaviour>();
                    if (ai != null) ai.enabled = false;
                }
            }

            StartCoroutine(RestoreFrozenUnitsAfter(hits, duration));
        }

        IEnumerator RestoreFrozenUnitsAfter(Collider2D[] hits, float duration)
        {
            yield return new WaitForSeconds(duration);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                if (hit.CompareTag("Unit"))
                {
                    var rb = hit.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.isKinematic = false;
                    var mb = hit.GetComponent<MonoBehaviour>();
                    if (mb != null) mb.enabled = true;
                }
            }
        }

        IEnumerator CastDamageBoostCoroutine(int team, float multiplier, float dur)
        {
            Debug.Log($"[GamePlayManager] Server: applying DamageBoost x{multiplier} to team {team} for {dur}s");
            GameObject[] units = GameObject.FindGameObjectsWithTag("Unit");
            foreach (var u in units)
            {
                Debug.Log($"[GamePlayManager] (sim) Boosting damage on {u.name}");
            }
            yield return new WaitForSeconds(dur);
            foreach (var u in units)
            {
                Debug.Log($"[GamePlayManager] (sim) Reverting damage on {u.name}");
            }
        }
        void StartCoroutine_CastDamageBoost(int team, float multiplier, float dur) => StartCoroutine(CastDamageBoostCoroutine(team, multiplier, dur));

        IEnumerator CastSpeedBoostCoroutine(int team, float multiplier, float dur)
        {
            Debug.Log($"[GamePlayManager] Server: applying SpeedBoost x{multiplier} to team {team} for {dur}s");
            GameObject[] units = GameObject.FindGameObjectsWithTag("Unit");
            foreach (var u in units) Debug.Log($"[GamePlayManager] (sim) Boosting speed on {u.name}");
            yield return new WaitForSeconds(dur);
            foreach (var u in units) Debug.Log($"[GamePlayManager] (sim) Reverting speed on {u.name}");
        }
        void StartCoroutine_CastSpeedBoost(int team, float multiplier, float dur) => StartCoroutine(CastSpeedBoostCoroutine(team, multiplier, dur));

        IEnumerator CastTurretBoostCoroutine(int team, float multiplier, float dur)
        {
            Debug.Log($"[GamePlayManager] Server: applying TurretBoost x{multiplier} to team {team} for {dur}s");
            GameObject[] turrets = GameObject.FindGameObjectsWithTag("Turret");
            foreach (var t in turrets) Debug.Log($"[GamePlayManager] (sim) Boosting turret: {t.name}");
            yield return new WaitForSeconds(dur);
            foreach (var t in turrets) Debug.Log($"[GamePlayManager] (sim) Reverting turret boost: {t.name}");
        }
        void StartCoroutine_CastTurretBoost(int team, float multiplier, float dur) => StartCoroutine(CastTurretBoostCoroutine(team, multiplier, dur));

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
