using UnityEngine;

namespace debug
{
    [ExecuteAlways]
    public class SpawnPointGizmo : MonoBehaviour
    {
        public Color gizmoColor = Color.cyan;
        public float size = 0.25f;
        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, size);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * (size + 0.1f), name);
#endif
        }
    }
}
