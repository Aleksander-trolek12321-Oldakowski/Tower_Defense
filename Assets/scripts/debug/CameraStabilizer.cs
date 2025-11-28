using UnityEngine;

namespace debug
{
    public class CameraStabilizer : MonoBehaviour
    {
        public float fixedZ = -10f;              // typical 2D camera Z
        public bool enforceEveryFrame = true;
        public bool debugLogs = true;

        Camera cam;

        void Start()
        {
            // try to grab any main camera; if null we'll catch in Update
            cam = Camera.main;
            if (cam == null && debugLogs) Debug.LogWarning("[CameraStabilizer] Camera.main not found at Start -> waiting for camera to appear.");
        }

        void Update()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam != null && debugLogs) Debug.Log($"[CameraStabilizer] Found Camera.main = '{cam.name}'");
                if (cam == null) return;
            }

            // keep camera on orthographic (we expect 2D)
            if (!cam.orthographic && debugLogs) Debug.LogWarning("[CameraStabilizer] Camera is not orthographic (expected 2D).");

            // fix Z position if it drifted
            Vector3 pos = cam.transform.position;
            if (Mathf.Abs(pos.z - fixedZ) > 0.0001f)
            {
                if (debugLogs) Debug.Log($"[CameraStabilizer] Fixing camera.z from {pos.z:F3} to {fixedZ:F3} (camera: {cam.name})");
                pos.z = fixedZ;
                cam.transform.position = pos;
            }

            // make sure near/far planes are reasonable
            if (cam.nearClipPlane < 0.01f)
            {
                if (debugLogs) Debug.Log($"[CameraStabilizer] Adjusting nearClipPlane from {cam.nearClipPlane:F4} to 0.01");
                cam.nearClipPlane = 0.01f;
            }

            if (!enforceEveryFrame) enabled = false;
        }
    }
}
