using Fusion;
using UnityEngine;

public class BarricadeHealth : NetworkBehaviour
{
    [Networked] public float HP { get; set; } = 10f;

    public void TakeDamage(float dmg)
    {
        if (!Object.HasStateAuthority) return;

        HP -= dmg;
        if (HP <= 0)
        {
            Debug.Log($"{Object.InputAuthority} died.");
        }
    }
}
