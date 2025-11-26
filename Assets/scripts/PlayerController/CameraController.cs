using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UI;

namespace Controller
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        public static event Action<Vector2> OnMapClick;

        [Header("References")]
        public Camera cam;

        [Header("Pan settings")]
        public float panSpeed = 1.0f;       // multiplier for pan movement
        public float dragDeadzone = 6f;     // pixels threshold to start panning

        [Header("Zoom settings")]
        public float minOrthographicSize = 2f;
        public float maxOrthographicSize = 12f;
        public float pinchZoomSpeed = 0.02f;
        public float mouseWheelZoomSpeed = 2f;
        public float doubleClickZoomFactor = 0.5f;
        public float doubleClickTime = 0.35f; // max time between clicks for double-click/tap
        public float doubleClickMaxDistance = 50f;

        [Header("Interaction")]
        public LayerMask interactableLayerMask = ~0; // which 2D layers to raycast for interactables
        public float tapMaxMovement = 12f;           // max movement allowed to consider a tap

        [Header("Map bounds (optional)")]
        [Tooltip("Gdy true — granice będą pobierane z mapSprite (world bounds). Jeśli false — użyje manualBounds.")]
        public bool useMapBounds = true;
        [Tooltip("SpriteRenderer mapy — przeciągnij obiekt z SpriteRenderer (np. Tilemap -> Sprite)")]
        public SpriteRenderer mapSprite;
        [Tooltip("Ręczne granice (w world units), używane gdy useMapBounds=false")]
        public Rect manualBounds = new Rect(-10, -10, 20, 20);

        // internal computed bounds
        private Rect mapWorldBounds;

        // internal state
        Vector2 panStartScreenPos;
        Vector3 panStartCamPos;
        bool isPanning = false;

        // touch state
        bool isPinching = false;
        float lastPinchDistance = 0f;

        // potential interactable under pointer at pointer down
        IInteractable potentialInteractable = null;
        Vector2 pointerDownScreenPos;
        bool pointerDownOverUI = false;

        // double-click/tap tracking
        float lastClickTime = 0f;
        Vector2 lastClickPos = Vector2.zero;

        void Reset()
        {
            cam = Camera.main;
        }

        void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>() ?? Camera.main;
            if (cam == null) Debug.LogWarning("[CameraController] No camera assigned or found.");
        }

        void Start()
        {
            ComputeMapWorldBounds();
            // initial clamp (in case scene started outside bounds)
            ClampCameraPosition();
        }

        void Update()
        {
            if (cam == null) return;

            // prioritize touch if available
            if (Input.touchSupported && Input.touchCount > 0)
            {
                HandleTouch();
            }
            else
            {
                HandleMouse();
            }

            // clamp zoom
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);

            // ensure camera position still valid after any zoom changes
            ClampCameraPosition();
        }

        // compute world bounds (from sprite or manual)
        void ComputeMapWorldBounds()
        {
            if (useMapBounds && mapSprite != null)
            {
                var b = mapSprite.bounds; // world-space bounds
                mapWorldBounds = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            }
            else
            {
                mapWorldBounds = manualBounds;
            }
        }

        // public helper — można wywołać z zewnątrz po edycyjnych zmianach
        public void RecomputeMapBounds()
        {
            ComputeMapWorldBounds();
            ClampCameraPosition();
        }

        // -----------------------
        // Touch handling
        // -----------------------
        void HandleTouch()
        {
            int touches = Input.touchCount;

            if (touches == 1)
            {
                Touch t = Input.GetTouch(0);

                // detect UI under finger (robust)
                pointerDownOverUI = UIUtils.IsPointerOverUI();

                if (t.phase == TouchPhase.Began)
                {
                    pointerDownScreenPos = t.position;
                    panStartScreenPos = t.position;
                    panStartCamPos = cam.transform.position;
                    isPinching = false;
                    isPanning = false;

                    potentialInteractable = RaycastForInteractable(t.position);
                }
                else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    if (pointerDownOverUI) return;

                    float moveDist = (t.position - panStartScreenPos).magnitude;
                    if (!isPanning && moveDist > dragDeadzone)
                    {
                        // start panning
                        isPanning = true;
                        potentialInteractable = null;
                    }

                    if (isPanning)
                    {
                        PanCameraByDrag(t.position);
                    }
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    if (pointerDownOverUI)
                    {
                        pointerDownOverUI = false;
                        potentialInteractable = null;
                        isPanning = false;
                        return;
                    }

                    float totalMove = (t.position - pointerDownScreenPos).magnitude;
                    bool isTap = totalMove < tapMaxMovement;

                    // double-tap detection (time + distance)
                    if (Time.time - lastClickTime < doubleClickTime && Vector2.Distance(lastClickPos, t.position) <= doubleClickMaxDistance)
                    {
                        DoDoubleClickZoom(t.position);
                        lastClickTime = 0f;
                        lastClickPos = Vector2.zero;
                    }
                    else
                    {
                        if (isTap && potentialInteractable != null)
                        {
                            potentialInteractable.OnInteract();
                        }
                        else if (isTap && potentialInteractable == null)
                        {
                            float zDist = -cam.transform.position.z;
                            Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(t.position.x, t.position.y, zDist));
                            OnMapClick?.Invoke(new Vector2(worldPoint.x, worldPoint.y));
                        }

                        // record for double-tap detection
                        lastClickTime = Time.time;
                        lastClickPos = t.position;
                    }

                    isPanning = false;
                    potentialInteractable = null;
                }
            }
            else if (touches >= 2)
            {
                // pinch-to-zoom: ignore if fingers over UI
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                bool t0UI = UIUtils.IsPointerOverUI();
                // if any finger is over UI, skip pinch
                if (t0UI) return;

                Vector2 p0 = t0.position;
                Vector2 p1 = t1.position;
                float curDistance = Vector2.Distance(p0, p1);

                if (!isPinching)
                {
                    isPinching = true;
                    lastPinchDistance = curDistance;
                }
                else
                {
                    float delta = curDistance - lastPinchDistance;
                    cam.orthographicSize -= delta * pinchZoomSpeed * (cam.orthographicSize / 5f);
                    lastPinchDistance = curDistance;

                    // clamp and reposition to stay in bounds
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);
                    ClampCameraPosition();
                }
            }
        }

        // -----------------------
        // Mouse handling
        // -----------------------
        void HandleMouse()
        {
            // wheel zoom (ignore when pointer over UI)
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                if (!UIUtils.IsPointerOverUI())
                {
                    cam.orthographicSize -= scroll * mouseWheelZoomSpeed * (cam.orthographicSize / 5f);
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);

                    // after zoom ensure camera inside bounds
                    ClampCameraPosition();
                }
            }

            // mouse button down
            if (Input.GetMouseButtonDown(0))
            {
                pointerDownOverUI = UIUtils.IsPointerOverUI();
                pointerDownScreenPos = Input.mousePosition;
                panStartScreenPos = Input.mousePosition;
                panStartCamPos = cam.transform.position;
                isPanning = false;

                potentialInteractable = RaycastForInteractable(Input.mousePosition);

                // double-click detection handled on MouseUp/MouseDown pattern below (we use time+distance)
                if (Time.time - lastClickTime < doubleClickTime && Vector2.Distance(lastClickPos, (Vector2)Input.mousePosition) <= doubleClickMaxDistance)
                {
                    DoDoubleClickZoom(Input.mousePosition);
                    lastClickTime = 0f;
                    lastClickPos = Vector2.zero;
                    potentialInteractable = null;
                }
                else
                {
                    lastClickTime = Time.time;
                    lastClickPos = Input.mousePosition;
                }
            }

            // dragging
            if (Input.GetMouseButton(0))
            {
                if (pointerDownOverUI) return;

                float move = ((Vector2)Input.mousePosition - panStartScreenPos).magnitude;
                if (!isPanning && move > dragDeadzone)
                {
                    isPanning = true;
                    potentialInteractable = null;
                }

                if (isPanning)
                {
                    PanCameraByDrag(Input.mousePosition);
                }
            }

            // mouse button up
            if (Input.GetMouseButtonUp(0))
            {
                if (pointerDownOverUI)
                {
                    pointerDownOverUI = false;
                    potentialInteractable = null;
                    isPanning = false;
                    return;
                }

                float totalMove = ((Vector2)Input.mousePosition - pointerDownScreenPos).magnitude;
                bool isTap = totalMove < tapMaxMovement;

                if (isTap && potentialInteractable != null)
                {
                    potentialInteractable.OnInteract();
                }
                else if (isTap && potentialInteractable == null)
                {
                    float zDist = -cam.transform.position.z;
                    Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDist));
                    OnMapClick?.Invoke(new Vector2(worldPoint.x, worldPoint.y));
                }

                potentialInteractable = null;
                isPanning = false;
            }
        }

        // -----------------------
        // Helpers
        // -----------------------
        void PanCameraByDrag(Vector2 currentScreenPos)
        {
            float zDist = -cam.transform.position.z;
            Vector3 worldStart = cam.ScreenToWorldPoint(new Vector3(panStartScreenPos.x, panStartScreenPos.y, zDist));
            Vector3 worldNow = cam.ScreenToWorldPoint(new Vector3(currentScreenPos.x, currentScreenPos.y, zDist));
            Vector3 worldDelta = worldStart - worldNow;
            worldDelta.z = 0f;
            cam.transform.position = panStartCamPos + worldDelta * panSpeed;

            // clamp after pan
            ClampCameraPosition();
        }

        IInteractable RaycastForInteractable(Vector2 screenPos)
        {
            if (cam == null) return null;

            float zDist = -cam.transform.position.z;
            Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDist));
            Vector2 origin2D = new Vector2(worldPoint.x, worldPoint.y);

            RaycastHit2D hit = Physics2D.Raycast(origin2D, Vector2.zero, 0f, interactableLayerMask);
            if (hit.collider != null)
            {
                var interact = hit.collider.GetComponentInParent<IInteractable>();
                return interact;
            }
            return null;
        }

        void DoDoubleClickZoom(Vector2 screenPos)
        {
            float target = Mathf.Clamp(cam.orthographicSize * doubleClickZoomFactor, minOrthographicSize, maxOrthographicSize);

            float zDist = -cam.transform.position.z;
            Vector3 worldBefore = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDist));
            cam.orthographicSize = target;
            Vector3 worldAfter = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDist));
            Vector3 diff = worldBefore - worldAfter;
            diff.z = 0f;
            cam.transform.position += diff;

            // clamp to bounds after zoom
            ClampCameraPosition();
        }

        // -----------------------
        // Bounds clamping
        // -----------------------
        // public wrapper so inne skrypty (np. CameraFollow) mogą wymusić clamp
        public void ClampToMapBounds()
        {
            ClampCameraPosition();
        }

        void ClampCameraPosition()
        {
            if (!useMapBounds) return;

            // recompute if sprite changed at runtime
            if (useMapBounds && mapSprite != null)
            {
                var b = mapSprite.bounds;
                mapWorldBounds = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            }

            float vertExtent = cam.orthographicSize;
            float horzExtent = vertExtent * cam.aspect;

            float minX = mapWorldBounds.xMin + horzExtent;
            float maxX = mapWorldBounds.xMax - horzExtent;
            float minY = mapWorldBounds.yMin + vertExtent;
            float maxY = mapWorldBounds.yMax - vertExtent;

            // if view is larger than map in X or Y, center camera on map
            if (minX > maxX || minY > maxY)
            {
                Vector3 center = new Vector3(mapWorldBounds.center.x, mapWorldBounds.center.y, cam.transform.position.z);
                cam.transform.position = center;
                return;
            }

            float x = Mathf.Clamp(cam.transform.position.x, minX, maxX);
            float y = Mathf.Clamp(cam.transform.position.y, minY, maxY);
            cam.transform.position = new Vector3(x, y, cam.transform.position.z);
        }
    }
}
