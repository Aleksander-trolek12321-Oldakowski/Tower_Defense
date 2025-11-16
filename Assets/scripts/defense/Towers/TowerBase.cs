using Fusion;
using UnityEngine;

public class TowerBase : NetworkBehaviour
{
    [Header("Common")]
    public NetworkObject projectilePrefab;
    public float range = 5f;
    public float fireRate = 1f;
    public Transform shootPoint;
    public int damage = 1;

    protected float cooldown = 0f;
    protected Transform currentTarget;
    protected int ownerTeam = 0;

    void Update()
    {
    }

    public override void FixedUpdateNetwork()
    {
        if (!Runner || !Runner.IsServer) return;

        cooldown -= (float)Runner.DeltaTime;
        AcquireTarget();
        if (currentTarget != null)
        {
            RotateTowards(currentTarget.position);
            if (cooldown <= 0f)
            {
                OnFire();
                cooldown = 1f / Mathf.Max(0.0001f, fireRate);
            }
        }
    }

    protected virtual void OnFire()
    {
    }

    protected void AcquireTarget()
    {
        currentTarget = null;
        float bestDist = float.MaxValue;
        var units = GameObject.FindGameObjectsWithTag("Unit");
        foreach (var u in units)
        {
            var en = u.transform;
            float d = Vector2.Distance(transform.position, en.position);
            if (d <= range && d < bestDist)
            {
                bestDist = d;
                currentTarget = en;
            }
        }
    }

    protected void RotateTowards(Vector3 pos)
    {
        Vector3 dir = (pos - transform.position);
        if (dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }
}
