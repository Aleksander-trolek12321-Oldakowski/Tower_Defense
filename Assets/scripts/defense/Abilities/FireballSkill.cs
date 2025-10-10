using UnityEngine;

public class FireballSkill : SkillBase
{
    [Header("Dane Fireballa")]
    public GameObject fireballPrefab;
    public float fireballFallSpeed = 10f;
    public float spawnHeight = 5f;

    private bool isAiming = false;
    private Vector2 targetPosition;

    // 🔹 kliknięcie przycisku UI
    public override void OnButtonPress()
    {
        if (isOnCooldown)
        {
            Debug.Log($"{skillName} jest na cooldownie");
            return;
        }

        isAiming = true;
        Debug.Log("celowanie");
    }

    protected override void Update()
    {
        base.Update();

        // do celowania
        if (isAiming && Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            targetPosition = new Vector2(mouseWorld.x, mouseWorld.y);

            SpawnFireball(targetPosition);
            StartCooldown();
            isAiming = false;
        }

        if (isAiming && Input.GetMouseButtonDown(1))
        {
            isAiming = false;
            Debug.Log("Celowanie anulowane");
        }
    }

    protected override void UseSkill()
    {
        
    }

    private void SpawnFireball(Vector2 position)
    {
        if (fireballPrefab == null)
        {
            Debug.LogWarning("Brak prefabu");
            return;
        }

        Vector2 spawnPos = position + Vector2.up * spawnHeight;
        GameObject fireball = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);

        Rigidbody2D rb = fireball.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 direction = (position - (Vector2)fireball.transform.position).normalized;
            rb.velocity = direction * fireballFallSpeed;
        }

        Debug.Log($"Fireball rzucony w {position}");
    }
}
