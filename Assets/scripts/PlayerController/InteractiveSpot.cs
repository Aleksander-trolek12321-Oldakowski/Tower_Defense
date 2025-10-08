using UnityEngine;
using Networking;

namespace Controller
{
    [RequireComponent(typeof(Collider2D))]
    public class InteractiveSpot : MonoBehaviour
    {
        public enum SpotType { BuildSpot, SpawnPoint }
        public SpotType spotType = SpotType.BuildSpot;

        // for build spots: id for tower spot indexing (matches GamePlayManager.towerSpots index)
        public int spotId = 0;

        // optional UI hint or debug name
        public string debugName = "Spot";

        // called by CameraController when player taps/clicks
        public void OnInteract()
        {
            Debug.Log($"[InteractiveSpot] OnInteract called on {debugName} (type={spotType})");

            // ensure we have local player reference
            var local = PlayerNetwork.Local;
            if (local == null)
            {
                Debug.Log("[InteractiveSpot] No local PlayerNetwork (Local is null).");
                return;
            }

            int myTeam = local.Team;
            if (spotType == SpotType.BuildSpot)
            {
                // only defenders (team 0) can interact here
                if (myTeam != 0)
                {
                    Debug.Log("[InteractiveSpot] You are not defender — cannot build here.");
                    // optionally show UI message
                    return;
                }

                // Open build UI or directly request place tower using default towerIndex (example 0)
                // Example: call PlayerNetwork.Local.RPC_RequestPlaceTower(towerIndex, spotId);
                // Here we call RPC (local is InputAuthority) to request host to spawn tower
                local.RPC_RequestPlaceTower(0, spotId); // towerIndex 0 as example — replace with real chooser
                Debug.Log($"[InteractiveSpot] Sent RPC_RequestPlaceTower spot={spotId}");
            }
            else // SpawnPoint
            {
                // only attackers (team 1) can interact (if you want spawn points only for attacker)
                if (myTeam != 1)
                {
                    Debug.Log("[InteractiveSpot] You are not attacker — cannot spawn units here.");
                    return;
                }

                // spawn unit — here we pick unitIndex 0 and spawn position at this spot
                Vector2 spawnPos = transform.position;
                local.RPC_RequestSpawnUnit(0, spawnPos);
                Debug.Log("[InteractiveSpot] Sent RPC_RequestSpawnUnit at " + spawnPos);
            }
        }
    }
}
