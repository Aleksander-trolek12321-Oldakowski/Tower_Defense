using System.Collections.Generic;
using Fusion;
using UnityEngine;


namespace Networking
{
    public class GamePlayManager : NetworkBehaviour
    {
        public static GamePlayManager Instance;


        [Header("Prefabs (NetworkObject prefabs registered in Runner)")]
        public NetworkObject[] unitPrefabs; // index corresponds to unitIndex
        public NetworkObject[] towerPrefabs; // towerIndex


        [Header("Build spots (only server enforces occupancy)")]
        public Transform[] towerSpots; // assign in the inspector
        private Dictionary<int, bool> spotOccupied = new Dictionary<int, bool>();


        private void Awake()
        {
            Instance = this;
        }


        public override void Spawned()
        {
            base.Spawned();
            // initialize occupied
            for (int i = 0; i < towerSpots.Length; i++) spotOccupied[i] = false;
        }


        // Attacker -> spawn units
        public void SpawnUnitByIndex(int idx, Vector2 pos, int team, PlayerRef requester)
        {
            // validate index
            if (idx < 0 || idx >= unitPrefabs.Length) return;


            // Host spawns and assigns team (networked prop in EnemyNetwork)
            var no = Runner.Spawn(unitPrefabs[idx], (Vector3)pos, Quaternion.identity, PlayerRef.None);
            var en = no.GetComponent<EnemyNetwork>();
            if (en != null) en.Team = team;


            Debug.Log($"Spawned unit idx={idx} at {pos} for team={team} requested by {requester}");
        }


        // Defender -> place tower at a specific spot
        public void PlaceTowerAtSpot(int towerIndex, int spotId, int team, PlayerRef requester)
        {
            if (towerIndex < 0 || towerIndex >= towerPrefabs.Length) return;
            if (!spotOccupied.ContainsKey(spotId)) return;
            if (spotOccupied[spotId]) return; // occupied


            // optional validation of distance/permissions
            // Spawn tower
            var pos = towerSpots[spotId].position;
            var no = Runner.Spawn(towerPrefabs[towerIndex], pos, Quaternion.identity, PlayerRef.None);
            var tw = no.GetComponent<TowerNetwork>();
            if (tw != null) tw.OwnerTeam = team;


            spotOccupied[spotId] = true;
            Debug.Log($"Placed tower idx={towerIndex} at spot {spotId} for team={team} (by {requester})");
        }


        // cleanup when a player leaves
        public void OnPlayerLeft(PlayerRef player)
        {
            // add logic here e.g. resetting spots if a tower belonged to a player that no longer exists
        }
    }
}