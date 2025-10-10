using UnityEngine;
using System.Collections;

public class FreezeSkill : SkillBase
{
    [Header("Dane Freeze")]
    public GameObject freezeAreaPrefab; 
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

        GameObject area = null;
        if (freezeAreaPrefab != null)
        {
            area = Instantiate(freezeAreaPrefab, position, Quaternion.identity);
            area.transform.localScale = Vector3.one * (freezeRadius * 2f); 
        }

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

                // Jeśli mają własny AI movement wyłączyć UnitController:
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

        if (area != null)
            Destroy(area);

        Debug.Log("Freeze over");
    }

    // Pomocnicze później można wywalić
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPosition, freezeRadius);
    }
}
