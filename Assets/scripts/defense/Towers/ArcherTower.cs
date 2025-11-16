using Fusion;
using UnityEngine;

public class ArcherTower : TowerBase
{
    [Header("Archer")]
    public float projectileSpeed = 10f;

    protected override void OnFire()
    {
        if (projectilePrefab == null || currentTarget == null) return;
        Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position;
        Vector2 dir = ((Vector2)currentTarget.position - (Vector2)spawnPos).normalized;

        try
        {
            var no = Runner.Spawn(projectilePrefab, spawnPos, Quaternion.identity, PlayerRef.None);
            var rb = no.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = dir * projectileSpeed;
            }
            var pj = no.GetComponent<ProjectileNetwork>();
            if (pj != null) pj.Init(damage, /*ownerTeam=*/0);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[ArcherTower] Spawn projectile failed: " + ex.Message);
        }
    }
}
