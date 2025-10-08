using Fusion;
using UnityEngine;

namespace Networking
{
    public class EnemyNetwork : NetworkBehaviour
    {
        [Networked] public int Team { get; set; } = -1;

        public GameObject visuals; // root of sprites/visuals

        public override void Spawned()
        {
            base.Spawned();

            var tv = GetComponent<TeamVisibility>();
            if (tv != null)
            {
                tv.UpdateVisibility();
            }
            else
            {
                // fallback: if no TeamVisibility attached, optionally set visuals directly
                if (visuals != null) visuals.SetActive(true);
            }
        }

        /// <summary>
        /// Update visuals for the local player. Uses Runner.GetPlayerObject(Runner.LocalPlayer) when available,
        /// otherwise falls back to finding PlayerNetwork instance with input authority.
        /// </summary>
        void UpdateVisual()
        {
            NetworkObject localPlayerObj = null;

            // Try to get local player object via Runner API (works on many Fusion versions)
            try
            {
                // Runner.LocalPlayer is a PlayerRef describing the local player
                // Runner.GetPlayerObject(PlayerRef) returns the NetworkObject for that player
                localPlayerObj = Runner.GetPlayerObject(Runner.LocalPlayer);
            }
            catch
            {
                // Fallback: find PlayerNetwork in scene that has input authority (works for simple projects)
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

            bool localIsSameTeam = (localPlayerNet != null && localPlayerNet.Team == Team);

            if (visuals != null)
            {
                visuals.SetActive(true);
                // visuals.SetActive(localIsSameTeam); // uncomment to show only to same-team players
            }
        }
    }
}
