using UnityEngine;

public class TowerBase : MonoBehaviour
{
    [Header("Common")]
    public Transform turretPivot;
    public float range = 5f;
    public bool rotateTowardsTarget = true;
    [Tooltip("Degrees per second for visual rotation")]
    public float rotationSpeed = 720f;

    public float rotationOffset = 90f;

    protected Transform currentTarget;
    protected float fireCooldown = 0f;

    protected virtual void Update()
    {
        AcquireTarget();

        if (rotateTowardsTarget && turretPivot != null && currentTarget != null)
        {
            Vector2 dir = (currentTarget.position - turretPivot.position);
            if (dir.sqrMagnitude > 0.0001f)
            {
                float worldAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                float parentAngle = 0f;
                if (turretPivot.parent != null)
                    parentAngle = turretPivot.parent.eulerAngles.z;

                float desiredLocalAngle = Mathf.DeltaAngle(parentAngle, worldAngle) + rotationOffset;
                Quaternion desiredLocalRot = Quaternion.Euler(0f, 0f, desiredLocalAngle);

                turretPivot.localRotation = Quaternion.RotateTowards(
                    turretPivot.localRotation,
                    desiredLocalRot,
                    rotationSpeed * Time.deltaTime
                );
            }
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
