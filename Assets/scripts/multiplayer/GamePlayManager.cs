using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Networking
{
    public class GamePlayManager : NetworkBehaviour
    {
        public static GamePlayManager Instance;

        [Header("Networked Prefabs (register these in NetworkProjectConfig)")]
        public NetworkObject[] unitPrefabs;   // index -> prefab for units (attacker)
        public NetworkObject[] towerPrefabs;  // index -> prefab for towers (defender)

        [Header("Build spots (scene Transforms)")]
        public Transform[] towerSpots;        // positions for towers (indexable by spotId)

        // occupancy map for spots (server authoritative)
        private Dictionary<int, bool> spotOccupied = new Dictionary<int, bool>();

        [Header("Attacker starting units")]
        public Transform[] attackerSpawnPoints;       // spawn points for initial attacker units
        public int[] initialUnitsPerSpawnPoint;       // optional, how many to spawn per point (can be null)

        [Header("Economy / Costs")]
        public int[] unitCosts;    // cost per unit index (align with unitPrefabs)
        public int[] towerCosts;   // cost per tower index (align with towerPrefabs)

        // Networked flag - signals all clients that match started
        [Networked] public bool MatchStarted { get; set; } = false;

        void Awake()
        {
            Instance = this;
        }

        public override void Spawned()
        {
            base.Spawned();
            Instance = this;

            // initialize spot occupancy map
            spotOccupied.Clear();
            if (towerSpots != null)
            {
                for (int i = 0; i < towerSpots.Length; i++)
                    spotOccupied[i] = false;
            }
        }

        /// <summary>
        /// Server-only: call to start the match.
        /// Sets MatchStarted and spawns initial attacker units.
        /// </summary>
        public void StartMatchOnServer()
        {
            if (!Runner.IsServer) return;

            MatchStarted = true;
            Debug.Log("[GamePlayManager] MatchStarted = true (server)");

            // spawn initial units for attacker team (team id = 1)
            SpawnInitialUnitsForTeam(1);
        }

        /// <summary>
        /// Server-only: spawn initial units for given team (example: attacker team = 1).
        /// Spawns a default unit (index 0) at each attackerSpawnPoint.
        /// </summary>
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

        /// <summary>
        /// Server-only: spawns a unit by index at world position for given team.
        /// Called by server in response to validated RPCs.
        /// </summary>
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

        /// <summary>
        /// Server-only: attempt to place a tower at spotId for given team.
        /// Validates towerIndex and occupancy then spawns tower.
        /// </summary>
        public void PlaceTowerAtSpot(int towerIndex, int spotId, int team, PlayerRef requester)
        {
            if (!Runner.IsServer) return;

            if (towerPrefabs == null || towerIndex < 0 || towerIndex >= towerPrefabs.Length)
            {
                Debug.LogWarning($"[GamePlayManager] PlaceTowerAtSpot invalid towerIndex {towerIndex}");
                return;
            }

            if (towerSpots == null || spotId < 0 || spotId >= towerSpots.Length)
            {
                Debug.LogWarning($"[GamePlayManager] PlaceTowerAtSpot invalid spotId {spotId}");
                return;
            }

            if (!spotOccupied.ContainsKey(spotId))
                spotOccupied[spotId] = false;

            if (spotOccupied[spotId])
            {
                Debug.Log($"[GamePlayManager] Spot {spotId} already occupied - cannot place tower.");
                return;
            }

            Vector3 pos = towerSpots[spotId].position;
            var no = Runner.Spawn(towerPrefabs[towerIndex], pos, Quaternion.identity, PlayerRef.None);
            var tw = no.GetComponent<TowerNetwork>();
            if (tw != null) tw.OwnerTeam = team;

            var tv = no.GetComponent<TeamVisibility>();
            if (tv != null) tv.UpdateVisibility();

            spotOccupied[spotId] = true;

            Debug.Log($"[GamePlayManager] Placed tower idx={towerIndex} at spot {spotId} for team={team} (by {requester})");
        }

        /// <summary>
        /// Authoritative cost lookup for units (server-side).
        /// </summary>
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

        public void OnPlayerLeft(PlayerRef player)
        {
            Debug.Log($"[GamePlayManager] OnPlayerLeft: {player}");
        }
    }
}