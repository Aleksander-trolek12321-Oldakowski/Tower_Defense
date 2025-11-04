using Fusion;
using UnityEngine;
using System.Collections;

public class FreezeSkill : SkillBase
{
    [Header("Dane Freeze")]
    public GameObject freezeAreaPrefab; // legacy local prefab (fallback)
    public NetworkObject freezeAreaNetworkPrefab; // <-- przypisz NetworkObject prefab tutaj
    public float freezeRadius = 3f;
    public float freezeDuration = 5f;

    private bool isAiming = false;
    private Vector2 targetPosition;

    public override void OnButtonPress()
    {
        if (isOnCooldown)
        {
            Debug.Log($"{skillName} jest na cooldownie");
            return;
        }

        isAiming = true;
        Debug.Log("Celowanie");
    }

    protected override void Update()
    {
        base.Update();

        if (isAiming && Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            targetPosition = new Vector2(mouseWorld.x, mouseWorld.y);

            StartCoroutine(FreezeAreaEffect(targetPosition));
            StartCooldown();
            isAiming = false;
        }

        if (isAiming && Input.GetMouseButtonDown(1))
        {
            isAiming = false;
            Debug.Log("Celowanie anulowane");
        }
    }

    private IEnumerator FreezeAreaEffect(Vector2 position)
    {
        Debug.Log($"Freeze w {position}");

        // spawn networked visual if possible
        GameObject areaGO = null;
        var runner = FindObjectOfType<NetworkRunner>();

        if (runner != null && freezeAreaNetworkPrefab != null)
        {
            NetworkObject no = null;
            try
            {
                no = runner.Spawn(freezeAreaNetworkPrefab, (Vector3)position, Quaternion.identity, PlayerRef.None);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FreezeSkill] runner.Spawn failed: {ex.Message} - falling back to Instantiate");
                no = null;
            }

            if (no != null)
            {
                areaGO = no.gameObject;
                areaGO.transform.localScale = Vector3.one * (freezeRadius * 2f);
            }
        }

        // fallback local instantiation
        if (areaGO == null && freezeAreaPrefab != null)
        {
            areaGO = Instantiate(freezeAreaPrefab, position, Quaternion.identity);
            areaGO.transform.localScale = Vector3.one * (freezeRadius * 2f);
        }

        // Apply freeze effects (server-side authoritative expected)
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, freezeRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Unit"))
            {
                Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                    rb.isKinematic = true;
                }

                // Jeśli mają własny AI movement — wyłączamy (proste)
                MonoBehaviour ai = hit.GetComponent<MonoBehaviour>();
                if (ai != null) ai.enabled = false;
            }
        }

        yield return new WaitForSeconds(freezeDuration);

        foreach (var hit in hits)
        {
            if (hit != null && hit.CompareTag("Unit"))
            {
                Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                }

                MonoBehaviour ai = hit.GetComponent<MonoBehaviour>();
                if (ai != null) ai.enabled = true;
            }
        }

        if (areaGO != null)
            Destroy(areaGO);

        Debug.Log("Freeze over");
    }

    // Pomocnicze później można wywalić
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPosition, freezeRadius);
    }
}
