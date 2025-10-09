using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Controller
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // Map click event (world position in XY)
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
        [Tooltip("Factor applied to orthographic size on double-click/tap (smaller => zoom in more)")]
        public float doubleClickZoomFactor = 0.5f;
        public float doubleClickTime = 0.35f; // max time between clicks for double-click/tap
        [Tooltip("Max distance in pixels between clicks to count as double-click")]
        public float doubleClickMaxDistance = 50f;

        [Header("Interaction")]
        public LayerMask interactableLayerMask = ~0; // which 2D layers to raycast for interactables
        public float tapMaxMovement = 12f;           // max movement allowed to consider a tap

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

                // detect UI under finger
                pointerDownOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId);

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
                        // UI handled it
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
                            // map click (convert to world)
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

                bool t0UI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t0.fingerId);
                bool t1UI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t1.fingerId);
                if (t0UI || t1UI) return;

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
                    // scale zoom with camera size so feel is consistent at different zoom levels
                    cam.orthographicSize -= delta * pinchZoomSpeed * (cam.orthographicSize / 5f);
                    lastPinchDistance = curDistance;
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
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    cam.orthographicSize -= scroll * mouseWheelZoomSpeed * (cam.orthographicSize / 5f);
                }
            }

            // mouse button down
            if (Input.GetMouseButtonDown(0))
            {
                pointerDownOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
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
                    // map click
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
            // convert to world points at z = 0 plane (or whatever plane camera is pointed to)
            float zDist = -cam.transform.position.z;
            Vector3 worldStart = cam.ScreenToWorldPoint(new Vector3(panStartScreenPos.x, panStartScreenPos.y, zDist));
            Vector3 worldNow = cam.ScreenToWorldPoint(new Vector3(currentScreenPos.x, currentScreenPos.y, zDist));
            Vector3 worldDelta = worldStart - worldNow;
            worldDelta.z = 0f;
            cam.transform.position = panStartCamPos + worldDelta * panSpeed;
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
        }
    }
}
