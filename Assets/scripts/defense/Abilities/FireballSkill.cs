using Fusion;
using UnityEngine;

public class FireballSkill : SkillBase
{
    [Header("Dane Fireballa")]
    public GameObject fireballPrefab; // legacy local prefab (fallback)
    public NetworkObject fireballNetworkPrefab; // <-- przypisz NetworkObject prefab tutaj
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
        // nieużywane — używamy OnButtonPress + Update do celowania
    }

    private void SpawnFireball(Vector2 position)
    {
        // Preferujemy sieciowy spawn przez Fusion runner, jeśli dostępny i jeśli prefab przypisany
        var runner = FindObjectOfType<NetworkRunner>();
        Vector2 spawnPos = position + Vector2.up * spawnHeight;

        if (runner != null && fireballNetworkPrefab != null)
        {
            // Server/host wykonuje runner.Spawn -> wszyscy klienci zobaczą obiekt
            NetworkObject no = null;
            try
            {
                no = runner.Spawn(fireballNetworkPrefab, (Vector3)spawnPos, Quaternion.identity, PlayerRef.None);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FireballSkill] runner.Spawn failed: {ex.Message} - falling back to Instantiate");
                no = null;
            }

            if (no != null)
            {
                Rigidbody2D rb = no.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 direction = (position - (Vector2)no.transform.position).normalized;
                    rb.velocity = direction * fireballFallSpeed;
                }
                Debug.Log($"[FireballSkill] Networked fireball spawned at {spawnPos}");
                return;
            }
        }

        // Fallback (lokalny): zachowanie jak wcześniej
        if (fireballPrefab == null)
        {
            Debug.LogWarning("Brak prefabu fireballa (zarówno network jak i lokalny)");
            return;
        }

        GameObject fireball = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);

        Rigidbody2D rbLocal = fireball.GetComponent<Rigidbody2D>();
        if (rbLocal != null)
        {
            Vector2 direction = (position - (Vector2)fireball.transform.position).normalized;
            rbLocal.velocity = direction * fireballFallSpeed;
        }

        Debug.Log($"Fireball rzucony lokalnie w {position}");
    }
}
