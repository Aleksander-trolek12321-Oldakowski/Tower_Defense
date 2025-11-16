using UnityEngine;

public class TowerBase : MonoBehaviour
{
    [Header("Common")]
    public Transform turretPivot;
    public float range = 5f;
    public bool rotateTowardsTarget = true;

    protected Transform currentTarget;
    protected float fireCooldown = 0f;

    protected virtual void Update()
    {
        AcquireTarget();

        if (rotateTowardsTarget && turretPivot != null && currentTarget != null)
        {
            Vector2 dir = (currentTarget.position - turretPivot.position);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            turretPivot.rotation = Quaternion.Lerp(turretPivot.rotation, Quaternion.Euler(0f, 0f, angle), Time.deltaTime * 12f);
        }
    }

    protected void AcquireTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
        Transform nearest = null;
        float best = float.MaxValue;
        foreach (var c in hits)
        {
            if (!c.CompareTag("Unit")) continue;
            float d = Vector2.SqrMagnitude(c.transform.position - transform.position);
            if (d < best)
            {
                best = d;
                nearest = c.transform;
            }
        }
        currentTarget = nearest;
    }

    public virtual void FireAt(Transform target)
    {
    }
}
