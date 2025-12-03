using UnityEngine;
using Fusion;

public class ArcherTower : TowerBase
{
    public NetworkPrefabRef projectilePrefab;
    public Transform muzzle;
    public float projectileSpeed = 8f;
    public float fireRate = 1.0f;
    public float damage = 1f;

    void Start()
    {
    }

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
                proj.damage = damage;
                proj.aoeRadius = 0f;
                proj.speed = projectileSpeed;

                var enemy = target.GetComponent<EnemyAI>();
                if (enemy != null)
                {
                    float towerRange = 6f;
                    proj.SetTargetAndOrigin(enemy, transform.position, towerRange);
                }
                else
                {
                    Vector2 dir = ((Vector2)target.position - (Vector2)spawnPos).normalized;
                    proj.NetworkedVelocity = dir * projectileSpeed;
                    proj.NetworkedRotation = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }
            }
        }

    }
}
