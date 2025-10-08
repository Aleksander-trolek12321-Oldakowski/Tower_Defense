using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Controller
{
    public class CameraController : MonoBehaviour
    {
        [Header("Camera")]
        public Camera cam; // assign your orthographic camera

        [Header("Pan settings")]
        public float panSpeed = 1.0f;          // speed multiplier for panning
        public float dragDeadzone = 5f;       // pixels before we treat as drag

        [Header("Zoom settings")]
        public float minOrthographicSize = 2f;
        public float maxOrthographicSize = 12f;
        public float pinchZoomSpeed = 0.02f;  // multiplier for pinch distance -> zoom
        public float mouseWheelZoomSpeed = 2f;
        public float doubleClickZoomFactor = 0.5f; // how much to scale (smaller => zoom in stronger)
        public float doubleClickTime = 0.35f; // max time between clicks for double-click/tap

        [Header("Interaction")]
        public LayerMask interactableLayerMask = ~0; // set to relevant 2D layers for interactable objects
        public float tapMaxMovement = 10f; // max screen movement for tap detection

        // internal
        Vector3 lastMousePosition;
        bool isPanning = false;
        Vector2 panStartScreenPos;
        Vector3 panStartCamPos;
        int activeTouchId = -1;

        // double tap/click detection
        float lastClickTime = 0f;
        Vector2 lastClickPos = Vector2.zero;

        // pinch tracking
        bool isPinching = false;
        float lastPinchDistance = 0f;

        // potential interactable under pointer
        IInteractable potentialInteractable = null;
        Vector2 pointerDownScreenPos;
        bool pointerDownOverUI = false;

        void Reset()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (cam == null) return;

            // handle touch first if present
            if (Input.touchSupported && Input.touchCount > 0)
            {
                HandleTouch();
            }
            else
            {
                HandleMouse();
            }

            // clamp camera size
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);
        }

        // -----------------------
        // TOUCH
        // -----------------------
        void HandleTouch()
        {
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);

                // Check if pointer is over UI
                pointerDownOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId);

                if (t.phase == TouchPhase.Began)
                {
                    pointerDownScreenPos = t.position;
                    panStartScreenPos = t.position;
                    panStartCamPos = cam.transform.position;
                    isPinching = false;
                    isPanning = false;
                    activeTouchId = t.fingerId;

                    // check for interactable under touch (immediate)
                    potentialInteractable = RaycastForInteractable(t.position);
                }
                else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    if (pointerDownOverUI) return; // let UI handle it

                    float moveDist = (t.position - (Vector2)panStartScreenPos).magnitude;
                    if (!isPanning && moveDist > dragDeadzone)
                    {
                        isPanning = true;
                        potentialInteractable = null; // don't interact if it became a pan
                    }

                    if (isPanning)
                    {
                        PanCameraByDrag(t.position);
                    }
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    if (pointerDownOverUI) return;

                    float totalMove = (t.position - pointerDownScreenPos).magnitude;
                    bool isTap = totalMove < tapMaxMovement && (Time.time - t.deltaTime) < 0.5f;

                    // double-tap detection
                    if (Time.time - lastClickTime < doubleClickTime && (Vector2.Distance(lastClickPos, t.position) < 50f))
                    {
                        // double-tap -> zoom at position
                        DoDoubleClickZoom(t.position);
                        lastClickTime = 0f;
                        lastClickPos = Vector2.zero;
                    }
                    else
                    {
                        if (isTap && potentialInteractable != null)
                        {
                            // call interact
                            potentialInteractable.OnInteract();
                        }
                        // record single tap for double-tap detection
                        lastClickTime = Time.time;
                        lastClickPos = t.position;
                    }

                    isPanning = false;
                    potentialInteractable = null;
                    activeTouchId = -1;
                }
            }
            else if (Input.touchCount >= 2)
            {
                // Pinch-to-zoom (ignore UI touches)
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                bool t0UI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t0.fingerId);
                bool t1UI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t1.fingerId);
                if (t0UI || t1UI) return; // if either finger over UI, ignore pinch

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
                }
            }
        }

        // -----------------------
        // MOUSE (PC)
        // -----------------------
        void HandleMouse()
        {
            // wheel zoom
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return; // avoid UI scroll interference
                float prevSize = cam.orthographicSize;
                cam.orthographicSize -= scroll * mouseWheelZoomSpeed * (cam.orthographicSize / 5f);
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);

                // optional: zoom towards mouse position (keep world point under cursor stable)
                Vector3 mouseWorldBefore = cam.ScreenToWorldPoint(Input.mousePosition);
                // no further action needed - simple wheel zoom is fine
            }

            // pointer down
            if (Input.GetMouseButtonDown(0))
            {
                pointerDownOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                pointerDownScreenPos = Input.mousePosition;
                panStartScreenPos = Input.mousePosition;
                panStartCamPos = cam.transform.position;
                isPanning = false;

                // raycast to check interactive object under pointer
                potentialInteractable = RaycastForInteractable(Input.mousePosition);

                // double-click detection
                if (Time.time - lastClickTime < doubleClickTime && Vector2.Distance(lastClickPos, Input.mousePosition) < 50f)
                {
                    // double-click
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

                float move = (Input.mousePosition - (Vector3)panStartScreenPos).magnitude;
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

            // pointer up
            if (Input.GetMouseButtonUp(0))
            {
                if (pointerDownOverUI)
                {
                    // UI got the click — nothing for camera
                    pointerDownOverUI = false;
                    potentialInteractable = null;
                    isPanning = false;
                    return;
                }

                float totalMove = (Input.mousePosition - (Vector3)pointerDownScreenPos).magnitude;
                bool isTap = totalMove < tapMaxMovement;

                if (isTap && potentialInteractable != null)
                {
                    potentialInteractable.OnInteract();
                }

                isPanning = false;
                potentialInteractable = null;
            }
        }

        // -----------------------
        // Helpers
        // -----------------------
        void PanCameraByDrag(Vector2 currentScreenPos)
        {
            // compute screen delta in xy (Vector3 z = 0)
            Vector3 screenDelta = new Vector3(currentScreenPos.x - panStartScreenPos.x,
                                            currentScreenPos.y - panStartScreenPos.y,
                                            0f);

            // distance from camera to world Z plane we consider (usually z=0 plane)
            float zDist = -cam.transform.position.z;

            // convert screen points to world points using the same zDist
            Vector3 worldStart = cam.ScreenToWorldPoint(new Vector3(panStartScreenPos.x, panStartScreenPos.y, zDist));
            Vector3 worldEnd   = cam.ScreenToWorldPoint(new Vector3(panStartScreenPos.x + screenDelta.x, panStartScreenPos.y + screenDelta.y, zDist));

            Vector3 worldDelta = worldStart - worldEnd;
            worldDelta.z = 0f;

            cam.transform.position = panStartCamPos + worldDelta * panSpeed;
        }

        IInteractable RaycastForInteractable(Vector2 screenPos)
        {
            if (cam == null) return null;

            // use z distance so ScreenToWorldPoint returns a point on world z = 0 plane (typowo)
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
            // compute target orthographic size
            float target = Mathf.Clamp(cam.orthographicSize * doubleClickZoomFactor, minOrthographicSize, maxOrthographicSize);

            // use same zDist approach so worldBefore/worldAfter are on same plane (z=0 plane)
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
