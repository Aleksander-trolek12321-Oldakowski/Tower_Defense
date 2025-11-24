using UnityEngine;

public class BranchClickHandler : MonoBehaviour
{
    [SerializeField] private Camera cam;

    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    private void HandleClick()
    {
        if (cam == null) return;

        Vector3 mousePos = Input.mousePosition;
        Vector3 worldPos = cam.ScreenToWorldPoint(mousePos);

        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
        if (hit.collider == null) return;

        var gate = hit.collider.GetComponent<PathBranchGate>();
        if (gate != null)
        {
            gate.OnGateClicked();
            return;
        }

        var bridge = hit.collider.GetComponent<DestructibleBridge>();
        if (bridge != null)
        {
            bridge.RPC_RequestDestroyBridge();
            return;
        }
    }
}
