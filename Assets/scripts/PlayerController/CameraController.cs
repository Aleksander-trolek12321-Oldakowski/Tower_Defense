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
        public float panSpeed = 1.0f;
        public float dragDeadzone = 6f;

        [Header("Zoom settings")]
        public float minOrthographicSize = 2f;
        public float maxOrthographicSize = 12f;
        public float pinchZoomSpeed = 0.02f;
        public float mouseWheelZoomSpeed = 2f;
        public float doubleClickZoomFactor = 0.5f;
        public float doubleClickTime = 0.35f;
        public float doubleClickMaxDistance = 50f;

        [Header("Interaction")]
        public LayerMask interactableLayerMask = ~0;
        public float tapMaxMovement = 12f;

        [Header("Map bounds")]
        public bool useMapBounds = true;
        public SpriteRenderer mapSprite;
        public Rect manualBounds = new Rect(-10, -10, 20, 20);

        // Internal state
        private Rect mapWorldBounds;
        private Vector2 panStartScreenPos;
        private Vector3 panStartCamPos;
        private bool isPanning = false;
        private bool isPinching = false;
        private float lastPinchDistance = 0f;
        private IInteractable potentialInteractable = null;
        private Vector2 pointerDownScreenPos;
        private bool pointerDownOverUI = false;
        private float lastClickTime = 0f;
        private Vector2 lastClickPos = Vector2.zero;

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
            ClampCameraPosition();
        }

        void Update()
        {
            if (cam == null) return;

            // Prioritize touch if available
            if (Input.touchSupported && Input.touchCount > 0)
            {
                HandleTouch();
            }
            else
            {
                HandleMouse();
            }

            // Always clamp zoom and position
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);
            ClampCameraPosition();
        }

        void ComputeMapWorldBounds()
        {
            if (useMapBounds && mapSprite != null)
            {
                var b = mapSprite.bounds;
                mapWorldBounds = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            }
            else
            {
                mapWorldBounds = manualBounds;
            }
        }

        public void RecomputeMapBounds()
        {
            ComputeMapWorldBounds();
            ClampCameraPosition();
        }

        void HandleTouch()
        {
            int touches = Input.touchCount;

            if (touches == 1)
            {
                Touch t = Input.GetTouch(0);
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

                    if (Time.time - lastClickTime < doubleClickTime && 
                        Vector2.Distance(lastClickPos, t.position) <= doubleClickMaxDistance)
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

                        lastClickTime = Time.time;
                        lastClickPos = t.position;
                    }

                    isPanning = false;
                    potentialInteractable = null;
                }
            }
            else if (touches >= 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                bool t0UI = UIUtils.IsPointerOverUI();
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
                    float newSize = cam.orthographicSize - delta * pinchZoomSpeed * (cam.orthographicSize / 5f);
                    
                    newSize = Mathf.Clamp(newSize, minOrthographicSize, maxOrthographicSize);
                    
                    Vector3 cameraPosBefore = cam.transform.position;
                    cam.orthographicSize = newSize;
                    
                    ClampCameraPosition();
                    
                    if (cam.transform.position != cameraPosBefore)
                    {
                        AdjustZoomToFitBounds();
                    }

                    lastPinchDistance = curDistance;
                }
            }
        }

        void HandleMouse()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !UIUtils.IsPointerOverUI())
            {
                Vector3 cameraPosBefore = cam.transform.position;
                cam.orthographicSize -= scroll * mouseWheelZoomSpeed * (cam.orthographicSize / 5f);
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);
                
                ClampCameraPosition();
                
                if (cam.transform.position != cameraPosBefore)
                {
                    AdjustZoomToFitBounds();
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                pointerDownOverUI = UIUtils.IsPointerOverUI();
                pointerDownScreenPos = Input.mousePosition;
                panStartScreenPos = Input.mousePosition;
                panStartCamPos = cam.transform.position;
                isPanning = false;

                potentialInteractable = RaycastForInteractable(Input.mousePosition);

                if (Time.time - lastClickTime < doubleClickTime && 
                    Vector2.Distance(lastClickPos, (Vector2)Input.mousePosition) <= doubleClickMaxDistance)
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

        void PanCameraByDrag(Vector2 currentScreenPos)
        {
            float zDist = -cam.transform.position.z;
            Vector3 worldStart = cam.ScreenToWorldPoint(new Vector3(panStartScreenPos.x, panStartScreenPos.y, zDist));
            Vector3 worldNow = cam.ScreenToWorldPoint(new Vector3(currentScreenPos.x, currentScreenPos.y, zDist));
            Vector3 worldDelta = worldStart - worldNow;
            worldDelta.z = 0f;
            cam.transform.position = panStartCamPos + worldDelta * panSpeed;

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
                return hit.collider.GetComponentInParent<IInteractable>();
            }
            return null;
        }

        void DoDoubleClickZoom(Vector2 screenPos)
        {
            Vector3 cameraPosBefore = cam.transform.position;
            float target = Mathf.Clamp(cam.orthographicSize * doubleClickZoomFactor, minOrthographicSize, maxOrthographicSize);

            float zDist = -cam.transform.position.z;
            Vector3 worldBefore = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDist));
            cam.orthographicSize = target;
            Vector3 worldAfter = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDist));
            Vector3 diff = worldBefore - worldAfter;
            diff.z = 0f;
            cam.transform.position += diff;

            ClampCameraPosition();
            
            if (cam.transform.position != cameraPosBefore)
            {
                AdjustZoomToFitBounds();
            }
        }

        void AdjustZoomToFitBounds()
        {
            if (!useMapBounds) return;

            float requiredSizeForX = mapWorldBounds.width / (2f * cam.aspect);
            float requiredSizeForY = mapWorldBounds.height / 2f;
            float requiredSize = Mathf.Max(requiredSizeForX, requiredSizeForY);

            if (cam.orthographicSize < requiredSize)
            {
                cam.orthographicSize = Mathf.Clamp(requiredSize, minOrthographicSize, maxOrthographicSize);
                ClampCameraPosition();
            }
        }

        public void ClampToMapBounds()
        {
            ClampCameraPosition();
        }

        void ClampCameraPosition()
        {
            if (!useMapBounds) return;

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

            if (minX > maxX)
            {
                minX = mapWorldBounds.center.x;
                maxX = mapWorldBounds.center.x;
            }
            if (minY > maxY)
            {
                minY = mapWorldBounds.center.y;
                maxY = mapWorldBounds.center.y;
            }

            float x = Mathf.Clamp(cam.transform.position.x, minX, maxX);
            float y = Mathf.Clamp(cam.transform.position.y, minY, maxY);
            cam.transform.position = new Vector3(x, y, cam.transform.position.z);
        }
    }
}