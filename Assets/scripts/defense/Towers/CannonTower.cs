using UnityEngine;
using Fusion;

public class CannonTower : TowerBase
{
    public NetworkPrefabRef projectilePrefab;
    public Transform muzzle;
    public float projectileSpeed = 6f;
    public float fireRate = 0.6f;
    public float damage = 3f;
    public float aoeRadius = 0.5f;

    protected override void Update()
    {
        base.Update();
        fireCooldown -= Time.deltaTime;
        if (currentTarget != null && fireCooldown <= 0f)
        {
            fireCooldown = 1f / fireRate;
            FireAt(currentTarget);
        }
    }

    public override void FireAt(Transform target)
    {
        if (target == null) return;

        var runnerObj = FindObjectOfType<Fusion.NetworkRunner>();
        if (runnerObj == null || !runnerObj.IsServer) return;

        Vector3 spawnPos = muzzle != null ? muzzle.position : transform.position;
        var no = runnerObj.Spawn(projectilePrefab, spawnPos, Quaternion.identity, Fusion.PlayerRef.None);
        if (no != null)
        {
            var proj = no.GetComponent<ProjectileNetwork>();
            if (proj != null)
            {
                Vector2 dir = ((Vector2)target.position - (Vector2)spawnPos).normalized;
                proj.InitVelocity(dir * projectileSpeed);
                proj.damage = damage;
                proj.aoeRadius = aoeRadius;
            }
        }
    }
}
