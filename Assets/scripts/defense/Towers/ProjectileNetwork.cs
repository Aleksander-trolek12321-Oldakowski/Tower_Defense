using System.Collections;
using Fusion;
using UnityEngine;
using Networking;

public class ProjectileNetwork : NetworkBehaviour
{
    [Networked] public Vector2 NetworkedPosition { get; set; }
    [Networked] public Vector2 NetworkedVelocity { get; set; }
    [Networked] public float NetworkedRotation { get; set; } = 0f;
    [Networked] public int OwnerTeam { get; set; } = -1;

    [Header("Projectile settings")]
    public float damage = 1f;
    public float lifeTime = 5f;
    public float aoeRadius = 0f;

    public float speed = 8f;
    public float maxChaseDistance = 6f;
    public float overlapRadius = 0.06f;

    [Header("Collision")]
    public LayerMask collisionMask = ~0;

    Rigidbody2D rb;

    private EnemyAI targetEnemy = null;
    private Vector2 originPos;
    private float _spawnTime;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.simulated = true;
        }

        _spawnTime = Time.time;

        if (Runner != null && Runner.IsServer)
        {
            if (NetworkedVelocity != Vector2.zero)
            {
                speed = NetworkedVelocity.magnitude;
                NetworkedRotation = Mathf.Atan2(NetworkedVelocity.y, NetworkedVelocity.x) * Mathf.Rad2Deg;
            }

            NetworkedPosition = transform.position;
            StartCoroutine(DespawnAfter(lifeTime));
        }
        else
        {
            transform.position = NetworkedPosition;
            transform.rotation = Quaternion.Euler(0f, 0f, NetworkedRotation);
        }
    }

    public void InitSpeed(float spd)
    {
        if (Runner != null && Runner.IsServer)
        {
            speed = spd;
            if (NetworkedVelocity == Vector2.zero)
            {
            }
        }
    }

    public void SetTargetAndOrigin(EnemyAI target, Vector2 origin, float maxChaseDist)
    {
        if (Runner == null || !Runner.IsServer) return;

        targetEnemy = target;
        originPos = origin;
        maxChaseDistance = maxChaseDist;

        if (targetEnemy != null)
        {
            Vector2 dir = ((Vector2)targetEnemy.transform.position - (Vector2)transform.position).normalized;
            NetworkedVelocity = dir * speed;
            NetworkedRotation = Mathf.Atan2(NetworkedVelocity.y, NetworkedVelocity.x) * Mathf.Rad2Deg;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner == null) return;

        if (Runner.IsServer)
        {
            float dt = (float)Runner.DeltaTime;
            Vector2 pos = NetworkedPosition;

            if (targetEnemy != null && targetEnemy.Object != null)
            {
                if (targetEnemy.Object == null)
                {
                    targetEnemy = null;
                }
                else
                {
                    Vector2 targetPos = (Vector2)targetEnemy.transform.position;

                    float distFromOrigin = Vector2.Distance(originPos, targetPos);
                    if (distFromOrigin > maxChaseDistance)
                    {
                        if (Object != null) Runner.Despawn(Object);
                        return;
                    }

                    Vector2 dir = (targetPos - pos);
                    float dist = dir.magnitude;

                    if (dist > 0.01f)
                    {
                        dir.Normalize();
                        NetworkedVelocity = dir * speed;
                    }
                    else
                    {
                        NetworkedVelocity = Vector2.zero;
                    }
                }
            }
            else
            {
            }

            Vector2 newPos = pos + NetworkedVelocity * dt;

            RaycastHit2D[] hits = Physics2D.LinecastAll(pos, newPos, collisionMask);
            bool hitDetected = false;
            Vector2 hitPoint = newPos;

            if (hits != null && hits.Length > 0)
            {
                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    if (!h.collider.CompareTag("Unit")) continue;

                    var enemy = h.collider.GetComponent<EnemyAI>();
                    if (enemy == null) continue;

                    hitDetected = true;
                    hitPoint = (h.point != Vector2.zero) ? h.point : newPos;
                    ApplyDamageToEnemy(enemy, damage, hitPoint);
                    break;
                }
            }

            if (!hitDetected)
            {
                Collider2D col = Physics2D.OverlapCircle(newPos, overlapRadius, collisionMask);
                if (col != null && col.CompareTag("Unit"))
                {
                    var enemy = col.GetComponent<EnemyAI>();
                    if (enemy != null)
                    {
                        hitDetected = true;
                        hitPoint = newPos;
                        ApplyDamageToEnemy(enemy, damage, hitPoint);
                    }
                }
            }

            if (hitDetected)
            {
                if (Object != null) Runner.Despawn(Object);
                return;
            }

            NetworkedPosition = newPos;
            if (NetworkedVelocity != Vector2.zero)
                NetworkedRotation = Mathf.Atan2(NetworkedVelocity.y, NetworkedVelocity.x) * Mathf.Rad2Deg;

            transform.position = NetworkedPosition;
            transform.rotation = Quaternion.Euler(0f, 0f, NetworkedRotation);
        }
        else
        {
            Vector2 cur = transform.position;
            Vector2 targ = NetworkedPosition;
            transform.position = Vector2.Lerp(cur, targ, 0.5f);
            transform.rotation = Quaternion.Euler(0f, 0f, NetworkedRotation);
        }
    }

    private void ApplyDamageToEnemy(EnemyAI enemy, float dmg, Vector2 hitPoint)
    {
        if (enemy == null) return;

        if (aoeRadius > 0f)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(hitPoint, aoeRadius, collisionMask);
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (!h.CompareTag("Unit")) continue;
                var e = h.GetComponent<EnemyAI>();
                if (e == null) continue;

                e.HP -= dmg;

                if (enemy.HP <= 0)
                {
                    var players = FindObjectsOfType<PlayerNetwork>();
                    foreach (var player in players)
                    {
                        if (player.Team == 0)
                        {
                            GameRoundManager.Instance?.RewardDefenderForKill();
                        }
                    }
                }
                Debug.Log($"[ProjectileNetwork] SERVER: AOE applied {dmg} to {e.name}. NewHP={e.HP}");
            }
        }
        else
        {
            enemy.HP -= dmg;
            Debug.Log($"[ProjectileNetwork] SERVER: Applied {dmg} to {enemy.name}. NewHP={enemy.HP}");
            if (enemy.HP <= 0)
            {
                var players = FindObjectsOfType<PlayerNetwork>();
                foreach (var player in players)
                {
                    if (player.Team == 0)
                    {
                        GameRoundManager.Instance?.RewardDefenderForKill();
                    }
                }

                if (Runner != null) Runner.Despawn(enemy.Object);
            }
        }
    }

    IEnumerator DespawnAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (Runner != null && Runner.IsServer && Object != null)
            Runner.Despawn(Object);
    }

    void OnDrawGizmosSelected()
    {
        if (aoeRadius > 0f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, aoeRadius);
        }
    }
}
