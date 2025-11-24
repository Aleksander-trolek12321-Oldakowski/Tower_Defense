using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DestructibleForest : NetworkBehaviour
{
    [Tooltip("Ile uderzeń moba potrzeba, żeby zniszczyć las")]
    public int hitsToDestroy = 5;

    [Networked] public int CurrentHits { get; set; }

    private Collider2D col;

    public override void Spawned()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (Object.HasStateAuthority)
        {
            CurrentHits = 0;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Object.HasStateAuthority) return;

        var enemy = other.GetComponent<EnemyAI>();
        if (enemy == null) return;

        RegisterHit();
    }

    private void RegisterHit()
    {
        CurrentHits++;
        Debug.Log($"[Forest] hit by enemy {CurrentHits}/{hitsToDestroy}");

        if (CurrentHits >= hitsToDestroy)
        {
            DestroyForest();
        }
    }

    private void DestroyForest()
    {
        Debug.Log("[Forest] destroyed!");

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && Runner != null)
        {
            Runner.Despawn(netObj);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
