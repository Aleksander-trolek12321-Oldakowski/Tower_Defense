using Fusion;
using UnityEngine;

namespace Networking
{
    public class TowerNetwork : NetworkBehaviour
    {
        [Networked] public int OwnerTeam { get; set; } = -1;
        public GameObject visuals; // root of tower sprite/visuals

        public override void Spawned()
        {
            base.Spawned();
            UpdateVisual();
        }

        /// <summary>
        /// Update tower visuals for the local player.
        /// Uses Runner.GetPlayerObject(Runner.LocalPlayer) by default, with fallback scanning.
        /// </summary>
        void UpdateVisual()
        {
            NetworkObject localPlayerObj = null;

            // Try using Runner API
            try
            {
                localPlayerObj = Runner.GetPlayerObject(Runner.LocalPlayer);
            }
            catch
            {
                // Fallback: find PlayerNetwork with input authority
                var allPlayerNets = FindObjectsOfType<PlayerNetwork>();
                foreach (var pn in allPlayerNets)
                {
                    if (pn == null) continue;
                    if (pn.Object != null && pn.Object.HasInputAuthority)
                    {
                        localPlayerObj = pn.Object;
                        break;
                    }
                }
            }

            PlayerNetwork localPlayerNet = null;
            if (localPlayerObj != null)
                localPlayerNet = localPlayerObj.GetComponent<PlayerNetwork>();

            bool localIsSameTeam = (localPlayerNet != null && localPlayerNet.Team == OwnerTeam);

            // Example: show tower only to its owner team
            if (visuals != null)
            {
                visuals.SetActive(localIsSameTeam);
            }
        }
    }
}
