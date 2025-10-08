using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Networking
{
    public class GamePlayManager : NetworkBehaviour
    {
        public static GamePlayManager Instance;

        [Header("Networked prefabs (register these in NetworkProjectConfig)")]
        public NetworkObject[] unitPrefabs;
        public NetworkObject[] towerPrefabs;

        [Header("Build spots (scene transforms)")]
        public Transform[] towerSpots;

        private Dictionary<int, bool> spotOccupied = new Dictionary<int, bool>();

        [Networked] public bool MatchStarted { get; set; } = false;

        private void Awake()
        {
            Instance = this;
        }

        public override void Spawned()
        {
            base.Spawned();
            Instance = this;

            // init spots occupancy
            spotOccupied.Clear();
            for (int i = 0; i < towerSpots.Length; i++) spotOccupied[i] = false;
        }

        // Called by host to start the match
        public void StartMatchOnServer()
        {
            if (!Runner.IsServer) return;
            MatchStarted = true;
            Debug.Log("[GamePlayManager] MatchStarted = true (server)");
        }

        // Host spawns unit (units are networked prefabs)
        public void SpawnUnitByIndex(int idx, Vector2 pos, int team, PlayerRef requester)
        {
            if (!Runner.IsServer) return;
            if (idx < 0 || idx >= unitPrefabs.Length) return;

            var no = Runner.Spawn(unitPrefabs[idx], (Vector3)pos, Quaternion.identity, PlayerRef.None);
            var en = no.GetComponent<EnemyNetwork>();
            if (en != null) en.Team = team;

            // update visibility immediately if component present
            var tv = no.GetComponent<TeamVisibility>();
            if (tv != null) tv.UpdateVisibility();

            Debug.Log($"Spawned unit idx={idx} at {pos} for team={team} requested by {requester}");
        }

        // Host places tower at a spot
        public void PlaceTowerAtSpot(int towerIndex, int spotId, int team, PlayerRef requester)
        {
            if (!Runner.IsServer) return;
            if (towerIndex < 0 || towerIndex >= towerPrefabs.Length) return;
            if (!spotOccupied.ContainsKey(spotId)) return;
            if (spotOccupied[spotId]) return;

            var pos = towerSpots[spotId].position;
            var no = Runner.Spawn(towerPrefabs[towerIndex], pos, Quaternion.identity, PlayerRef.None);
            var tw = no.GetComponent<TowerNetwork>();
            if (tw != null) tw.OwnerTeam = team;

            var tv = no.GetComponent<TeamVisibility>();
            if (tv != null) tv.UpdateVisibility();

            spotOccupied[spotId] = true;
            Debug.Log($"Placed tower idx={towerIndex} at spot {spotId} for team={team} (by {requester})");
        }

        public void OnPlayerLeft(PlayerRef player)
        {
            // optional cleanup
        }
    }
}
