using UnityEngine;
using UI;

namespace Controller
{
    [RequireComponent(typeof(Collider2D))]
    public class InteractiveSpot : MonoBehaviour, IInteractable
    {
        public int spotId = 0;
        public Color gizmoColor = Color.cyan;

        // Called by CameraController when the player clicks/taps the spot
        public void OnInteract()
        {
            Debug.Log($"[InteractiveSpot] OnInteract called for spot {spotId} (obj:{gameObject.name})");

            var local = Networking.PlayerNetwork.Local;
            if (local == null)
            {
                Debug.LogWarning("[InteractiveSpot] No local player found. Ignoring.");
                return;
            }

            if (local.Team != 0)
            {
                Debug.Log("[InteractiveSpot] Local is not defender - cannot open build menu.");
                return;
            }

            local.OpenBuildMenu(spotId);
        }

        // For physics-based input: respond to pointer clicks if used that way
        void OnMouseDown()
        {
            // Note: OnMouseDown works in Editor if collider + camera are set appropriately.
            OnInteract();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                var b = col.bounds;
                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
    }
}