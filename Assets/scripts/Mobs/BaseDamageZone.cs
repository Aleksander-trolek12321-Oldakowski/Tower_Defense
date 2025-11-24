using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BaseDamageZone : NetworkBehaviour
{
    [Tooltip("Ile obrażeń zadaje jeden mob po dotarciu do bazy")]
    public float damagePerEnemy = 10f;

    [Tooltip("Referencja do skryptu HP bazy")]
    public BaseHealth baseHealth;

    private Collider2D col;

    public override void Spawned()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (baseHealth == null)
        {
            baseHealth = FindObjectOfType<BaseHealth>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Object.HasStateAuthority) return;

        var enemy = other.GetComponent<EnemyAI>();
        if (enemy == null) return;

        if (baseHealth != null)
        {
            baseHealth.RPC_TakeDamage(damagePerEnemy);
        }

        enemy.RPC_TakeDamage(9999f);
    }
}
