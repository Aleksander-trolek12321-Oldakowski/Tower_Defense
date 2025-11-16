using System;
using Fusion;
using UnityEngine;

public class ProjectileNetwork : NetworkBehaviour
{
    public int damage = 1;
    public bool isAoe = false;
    public float aoeRadius = 1.5f;
    public float aoeDamage = 1f;

    public void Init(int dmg, int ownerTeam)
    {
        damage = dmg;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!Runner || !Runner.IsServer) return;

        if (other.CompareTag("Unit"))
        {
            var ai = other.GetComponent<EnemyAI>();
            if (ai != null)
            {
                if (isAoe)
                {
                    Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, aoeRadius);
                    foreach (var h in hits)
                    {
                        if (h.CompareTag("Unit"))
                        {
                            var e = h.GetComponent<EnemyAI>();
                            if (e != null)
                            {
                                e.HP -= aoeDamage;
                                if (e.HP <= 0) Runner.Despawn(e.Object);
                            }
                        }
                    }
                }
                else
                {
                    ai.HP -= damage;
                    if (ai.HP <= 0) Runner.Despawn(ai.Object);
                }
            }

            Runner.Despawn(Object);
        }
        else if (other.CompareTag("Turret"))
        {
            Runner.Despawn(Object);
        }
    }
}
