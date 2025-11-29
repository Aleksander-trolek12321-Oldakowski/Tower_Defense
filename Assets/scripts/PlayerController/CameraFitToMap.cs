using UnityEngine;
using UnityEngine.Tilemaps;

namespace Controller
{
    [RequireComponent(typeof(Camera))]
    public class CameraFitToMap : MonoBehaviour
    {
        public Transform mapRoot;
        public Tilemap tilemap;
        public SpriteRenderer spriteRenderer;
        public float margin = 0.5f;
        public float minOrthoSize = 2f;
        public float maxOrthoSize = 50f;
        public bool recalcEveryFrame = false;

        Camera cam;
        int lastScreenWidth = 0;
        int lastScreenHeight = 0;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (!cam.orthographic)
                Debug.LogWarning("[CameraFitToMap] Kamera nie jest orthographic — skrypt działa najlepiej dla 2D orthographic.");
        }

        void Start()
        {
            RecalculateFit();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }

        void Update()
        {
            if (recalcEveryFrame || Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                RecalculateFit();
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
            }

        }

        void LateUpdate()
        {
            ClampPositionToMap();
        }

        void RecalculateFit()
        {
            if (cam == null) return;

            Bounds mapBounds;
            if (tilemap != null)
            {
                mapBounds = tilemap.localBounds;
                mapBounds = TransformBounds(tilemap.transform.localToWorldMatrix, mapBounds);
            }
            else if (spriteRenderer != null)
            {
                mapBounds = spriteRenderer.bounds;
            }
            else if (mapRoot != null)
            {
                var rends = mapRoot.GetComponentsInChildren<Renderer>();
                if (rends == null || rends.Length == 0)
                {
                    Debug.LogWarning("[CameraFitToMap] Nie znaleziono rendererów pod mapRoot.");
                    return;
                }
                mapBounds = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) mapBounds.Encapsulate(rends[i].bounds);
            }
            else
            {
                Debug.LogWarning("[CameraFitToMap] Nie ustawiono tilemap, spriteRenderer ani mapRoot.");
                return;
            }

            float mapWidth = mapBounds.size.x;
            float mapHeight = mapBounds.size.y;
            Vector3 mapCenter = mapBounds.center;

            float aspect = (float)Screen.width / (float)Screen.height;
            float sizeForHeight = (mapHeight / 2f) + margin;
            float sizeForWidth = (mapWidth / (2f * aspect)) + (margin / aspect);

            float desiredOrtho = Mathf.Max(sizeForHeight, sizeForWidth);
            desiredOrtho = Mathf.Clamp(desiredOrtho, minOrthoSize, maxOrthoSize);

            cam.orthographicSize = desiredOrtho;

            _mapBounds = mapBounds;
        }

        static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            var center = matrix.MultiplyPoint3x4(bounds.center);
            var extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0, 0, extents.z));
            extents = new Vector3(Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        Bounds _mapBounds;

        void ClampPositionToMap()
        {
            if (_mapBounds.size == Vector3.zero) return;

            // world half-sizes widoku
            float vertHalf = cam.orthographicSize;
            float horizHalf = cam.orthographicSize * ((float)Screen.width / Screen.height);

            Vector3 pos = cam.transform.position;

            float leftLimit = _mapBounds.min.x + horizHalf;
            float rightLimit = _mapBounds.max.x - horizHalf;
            float bottomLimit = _mapBounds.min.y + vertHalf;
            float topLimit = _mapBounds.max.y - vertHalf;

            if (leftLimit > rightLimit)
            {
                pos.x = _mapBounds.center.x;
            }
            else
            {
                pos.x = Mathf.Clamp(pos.x, leftLimit, rightLimit);
            }

            if (bottomLimit > topLimit)
            {
                pos.y = _mapBounds.center.y;
            }
            else
            {
                pos.y = Mathf.Clamp(pos.y, bottomLimit, topLimit);
            }

            cam.transform.position = new Vector3(pos.x, pos.y, cam.transform.position.z);
        }
    }
}
