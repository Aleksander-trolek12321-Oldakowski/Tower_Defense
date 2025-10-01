using Fusion;
using UnityEngine;

namespace Networking
{
    public class PlayerNetwork : NetworkBehaviour
    {
        [Networked] public int Team { get; set; } = -1;

        public GameObject defenderUI;   // visible only for defender
        public GameObject attackerUI;   // visible only for attacker

        public override void Spawned()
        {
            base.Spawned();
            // Ensure the local player's UI matches the current Team value.
            UpdateLocalUI();
        }

        void Update()
        {
            // React to changes in the team (networked property).
            // Only the local player should update their own UI, so check input authority.
            if (Object.HasInputAuthority)
                UpdateLocalUI();
        }

        void UpdateLocalUI()
        {
            if (!Object.HasInputAuthority) return; // only the local player updates their UI

            // Activate UI depending on Team value (0 = defender, 1 = attacker).
            if (defenderUI) defenderUI.SetActive(Team == 0);
            if (attackerUI) attackerUI.SetActive(Team == 1);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPC_RequestSpawnUnit(int unitIndex, Vector2 worldPos, RpcInfo info = default)
        {
            // As a safety check ensure we are on the server/host.
            if (!Runner.IsServer) return;

            // Validation: only attackers (Team == 1) are allowed to spawn units.
            if (Team != 1) return;

            // Forward the spawn request to the GamePlayManager which holds prefabs and performs the actual spawn.
            // Pass the team and the requester (info.Source) so GamePlayManager can attribute ownership / permissions.
            GamePlayManager.Instance.SpawnUnitByIndex(unitIndex, worldPos, Team, info.Source);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPC_RequestPlaceTower(int towerIndex, int spotId, RpcInfo info = default)
        {
            // Ensure this runs only on the server/host.
            if (!Runner.IsServer) return;

            // Validation: only defenders (Team == 0) may place towers.
            if (Team != 0) return;

            // Forward the tower placement request to GamePlayManager.
            // GamePlayManager should validate spot availability, consume resources, and actually place the tower.
            GamePlayManager.Instance.PlaceTowerAtSpot(towerIndex, spotId, Team, info.Source);
        }
    }
}

