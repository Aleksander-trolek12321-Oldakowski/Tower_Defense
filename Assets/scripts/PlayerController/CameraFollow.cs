using UnityEngine;

namespace Controller
{
    public class CameraFollow : MonoBehaviour
    {
        [Tooltip("The transform the camera should follow. Set this at runtime.")]
        public Transform target;

        [Tooltip("Offset from the target position (z usually -10 for orthographic).")]
        public Vector3 offset = new Vector3(0f, 0f, -10f);

        [Tooltip("Smoothing speed for following.")]
        [Range(0.01f, 20f)]
        public float smoothSpeed = 8f;

        CameraController camController;

        void Start()
        {
            camController = FindObjectOfType<CameraController>();
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * smoothSpeed);

            if (camController != null)
                camController.ClampToMapBounds();
        }
    }
}
