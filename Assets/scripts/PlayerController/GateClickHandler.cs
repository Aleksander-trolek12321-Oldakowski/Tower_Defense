using UnityEngine;

[RequireComponent(typeof(Camera))]
public class GateClickHandler : MonoBehaviour
{
    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                HandlePointer(t.position);
            }
        }
        if (Input.GetMouseButtonDown(0))
        {
            HandlePointer(Input.mousePosition);
        }
    }

    void HandlePointer(Vector2 screenPos)
    {
        if (cam == null) return;
        Vector3 wp = cam.ScreenToWorldPoint(screenPos);
        Vector2 w2 = new Vector2(wp.x, wp.y);

        Collider2D[] cols = Physics2D.OverlapPointAll(w2);
        foreach (var c in cols)
        {
            if (c == null) continue;
            var gate = c.GetComponent<PathBranchGate>();
            if (gate != null)
            {
                gate.OnGateClicked();
                return;
            }

            var interact = c.GetComponent<Controller.IInteractable>();
            if (interact != null)
            {
                interact.OnInteract();
                return;
            }
        }
    }

}
