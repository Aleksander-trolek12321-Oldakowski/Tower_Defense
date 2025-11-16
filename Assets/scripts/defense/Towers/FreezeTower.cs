using UnityEngine;

public class FreezeTower : TowerBase
{
    public float freezeRadius = 2f;
    public float freezeDuration = 3f;
    public float fireRate = 5f;

    void Start()
    {
        rotateTowardsTarget = false;
    }

    protected override void Update()
    {
        base.Update();
        fireCooldown -= Time.deltaTime;

        if (fireCooldown <= 0f)
        {
            fireCooldown = fireRate;
            FireAt(null);
        }
    }

    public override void FireAt(Transform target)
    {
        var runnerObj = FindObjectOfType<Fusion.NetworkRunner>();
        if (runnerObj == null || !runnerObj.IsServer)
        {
            return;
        }

        if (Networking.GamePlayManager.Instance != null)
        {
            Networking.GamePlayManager.Instance.ApplyFreezeToUnitsInRadius((Vector2)transform.position, freezeRadius, freezeDuration);
        }
    }
}
