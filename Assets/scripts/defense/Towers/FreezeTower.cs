using Fusion;
using UnityEngine;

public class FreezeTower : TowerBase
{
    [Header("Freeze")]
    public float freezeRadius = 3f;
    public float freezeDuration = 10f;
    public float cooldownSeconds = 20f;

    protected override void OnFire()
    {
        var gm = Networking.GamePlayManager.Instance;
        if (gm != null)
        {
            gm.ApplyFreezeToUnitsInRadius(transform.position, freezeRadius, freezeDuration);
            Debug.Log("[FreezeTower] Applied freeze at " + transform.position);
        }
        else
        {
            Debug.LogWarning("[FreezeTower] GamePlayManager.Instance == null");
        }

        cooldown = cooldownSeconds;
    }
}
