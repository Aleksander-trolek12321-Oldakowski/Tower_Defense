using System;
using System.Collections;
using Fusion;
using UnityEngine;

public class ProjectileNetwork : NetworkBehaviour
{
    [Networked] public Vector2 NetworkedPosition { get; set; }
    [Networked] public Vector2 NetworkedVelocity { get; set; }

    public float damage = 1f;
    public float lifeTime = 5f;
    public float aoeRadius = 0f;

    Rigidbody2D rb;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        if (Runner != null && Runner.IsServer)
        {
            NetworkedPosition = transform.position;
            rb.isKinematic = true;
            StartCoroutine(DespawnAfter(lifeTime));
        }
    }

    public void InitVelocity(Vector2 vel)
    {
        if (Runner != null && Runner.IsServer)
        {
            NetworkedVelocity = vel;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner == null) return;

        if (Runner.IsServer)
        {
            Vector2 pos = NetworkedPosition;
            Vector2 newPos = pos + NetworkedVelocity * (float)Runner.DeltaTime;
            NetworkedPosition = newPos;
            transform.position = newPos;
        }
        else
        {
            Vector2 cur = transform.position;
            Vector2 targ = NetworkedPosition;
            transform.position = Vector2.Lerp(cur, targ, 0.5f);
        }
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (Runner == null) return;

        if (!Runner.IsServer) return;

        if (col.CompareTag("Unit"))
        {
            var ai = col.GetComponent<EnemyAI>();
            if (ai != null)
            {
                if (aoeRadius > 0f)
                {
                    Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, aoeRadius);
                    foreach (var h in hits)
                    {
                        if (!h.CompareTag("Unit")) continue;
                        var e = h.GetComponent<EnemyAI>();
                        if (e != null)
                        {
                            e.RPC_TakeDamage(damage);
                        }
                    }
                }
                else
                {
                    ai.RPC_TakeDamage(damage);
                }
            }

            // despawn projectile
            Runner.Despawn(Object);
            return;
        }

        Runner.Despawn(Object);
    }

    IEnumerator DespawnAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (Runner != null && Runner.IsServer && Object != null)
            Runner.Despawn(Object);
    }
}
