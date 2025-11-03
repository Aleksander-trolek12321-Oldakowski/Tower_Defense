using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : NetworkBehaviour
{
    [Networked] public float HP { get; set; }
    [Networked] public float Speed { get; set; }
    [Networked] public float Attack { get; set; }
    [Networked] public EnemyType Type { get; set; }

    private Rigidbody2D rb;
    private PathManager path;
    private int currentWaypointIndex = 0;
    private bool reachedEnd = false;

    [Networked] private Vector2 NetworkedPosition { get; set; }

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void InitStats(EnemyType type)
    {
        Type = type;
        switch (type)
        {
            case EnemyType.Light:
                HP = 2f; Speed = 3f; Attack = 0.5f;
                break;
            case EnemyType.Normal:
                HP = 4f; Speed = 2f; Attack = 1f;
                break;
            case EnemyType.Heavy:
                HP = 8f; Speed = 1f; Attack = 2f;
                break;
        }
    }

    public void SetPath(PathManager manager)
    {
        path = manager;
        currentWaypointIndex = 0;
        reachedEnd = false;

        // 🔥 Ustaw od razu pozycję celu na pierwszy waypoint
        if (path != null && path.GetWaypoint(0) != null)
        {
            MoveTowardsImmediate(path.GetWaypoint(0).position);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner.IsServer)
        {
            MoveOnServer();
        }
        else
        {
            transform.position = Vector2.Lerp(transform.position, NetworkedPosition, 0.2f);
        }
    }

    private void MoveOnServer()
    {
        if (path == null || reachedEnd) return;

        var waypoint = path.GetWaypoint(currentWaypointIndex);
        if (waypoint == null) return;

        Vector2 dir = ((Vector2)waypoint.position - rb.position);
        float dist = dir.magnitude;
        if (dist < 0.1f)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= path.GetWaypointCount())
            {
                reachedEnd = true;

                // Jeśli są odnogi — wybierz kolejną ścieżkę
                if (path.HasBranches)
                {
                    int branchIndex = GameManager.Instance.SelectedBranchIndex;
                    var nextPath = path.GetBranch(branchIndex);
                    if (nextPath != null)
                    {
                        path = nextPath;
                        currentWaypointIndex = 0;
                        reachedEnd = false;

                        // 🔥 Po zmianie ścieżki od razu rusza do nowego pierwszego waypointu
                        MoveTowardsImmediate(path.GetWaypoint(0).position);
                    }
                }
                return;
            }
        }

        dir.Normalize();
        Vector2 newPos = rb.position + dir * Speed * Runner.DeltaTime;
        rb.MovePosition(newPos);
        NetworkedPosition = newPos;
    }

    /// <summary>
    /// Natychmiastowe ruszenie w kierunku punktu, bez czekania na następny tick.
    /// </summary>
    private void MoveTowardsImmediate(Vector2 target)
    {
        if (rb == null) return;
        Vector2 dir = (target - rb.position).normalized;
        Vector2 newPos = rb.position + dir * Speed * Runner.DeltaTime;
        rb.MovePosition(newPos);
        NetworkedPosition = newPos;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float dmg)
    {
        HP -= dmg;
        if (HP <= 0)
        {
            Runner.Despawn(Object);
        }
    }
}
