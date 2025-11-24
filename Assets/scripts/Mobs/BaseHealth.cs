using Fusion;
using UnityEngine;

public class BaseHealth : NetworkBehaviour
{
    [Networked] public float CurrentHP { get; set; }

    [Header("Config")]
    public float maxHP = 100f;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentHP = maxHP;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float amount)
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentHP <= 0f) return;

        CurrentHP -= amount;
        Debug.Log($"[Base] took {amount} dmg. HP: {CurrentHP}/{maxHP}");

        if (CurrentHP <= 0f)
        {
            CurrentHP = 0f;
            OnBaseDestroyed();
        }
    }

    private void OnBaseDestroyed()
    {
        Debug.Log("[Base] GAME OVER");
    }
}
