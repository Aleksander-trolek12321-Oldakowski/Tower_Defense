using System.Collections;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : NetworkBehaviour
{
    [Networked] public float HP { get; set; }
    [Networked] public float Speed { get; set; }
    [Networked] public float Attack { get; set; }
    [Networked] public EnemyType Type { get; set; }
    [Networked] public bool IsFrozen { get; set; }

    [Networked] public Vector2 NetworkedPosition { get; set; }
    [Networked] public int SpriteIndex { get; set; } = 0;
    [Networked] public bool GhostVisible { get; set; } = true;

    private Rigidbody2D rb;
    private PathManager currentPath;
    private int currentWaypointIndex = 0;
    private bool reachedEnd = false;

    [Header("Visuals (fallback)")]
    public GameObject visualsRoot;
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public Sprite[] typeSprites;

    public override void Spawned()
    {
        base.Spawned();

        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.isKinematic = !(Runner != null && Runner.IsServer);
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Debug.Log($"[EnemyAI] Spawned (IsServer={(Runner!=null?Runner.IsServer:false)}) Type={Type} SpriteIndex={SpriteIndex} pos={transform.position}");

        if (Runner != null && Runner.IsServer && Type == EnemyType.Ghost)
        {
            StartCoroutine(GhostCycleCoroutine());
        }

        if (Runner != null && Runner.IsServer)
            NetworkedPosition = transform.position;

        ApplySpriteFromIndex();
    }

    public void InitStats(EnemyType type)
    {
        Type = type;
        switch (type)
        {
            case EnemyType.Werewolf:
                HP = 2f; Speed = 3f; Attack = 0.5f;
                break;
            case EnemyType.Zombie:
                HP = 4f; Speed = 2f; Attack = 1f;
                break;
            case EnemyType.Ork:
                HP = 8f; Speed = 1f; Attack = 2f;
                break;
            case EnemyType.Ghost:
                HP = 1f; Speed = 8f; Attack = 1f;
                break;
            default:
                HP = 1f; Speed = 1f; Attack = 1f;
                break;
        }

        if (Runner != null && Runner.IsServer)
        {
            SpriteIndex = (int)type;
            Debug.Log($"[EnemyAI] InitStats (server): type={type} -> SpriteIndex={SpriteIndex}");
        }
        else
        {
            SpriteIndex = (int)type;
            Debug.Log($"[EnemyAI] InitStats (client/local): tentative SpriteIndex={SpriteIndex}");
        }

        ApplySpriteFromIndex();
    }

    void ApplySpriteFromIndex()
    {
        if (spriteRenderer == null) return;

        var mgr = Networking.GamePlayManager.Instance;
        if (mgr != null && mgr.enemyTypeSprites != null && SpriteIndex >= 0 && SpriteIndex < mgr.enemyTypeSprites.Length)
        {
            var s = mgr.enemyTypeSprites[SpriteIndex];
            if (s != null && spriteRenderer.sprite != s)
            {
                spriteRenderer.sprite = s;
                return;
            }
        }

        if (typeSprites != null && SpriteIndex >= 0 && SpriteIndex < typeSprites.Length)
        {
            var s2 = typeSprites[SpriteIndex];
            if (s2 != null && spriteRenderer.sprite != s2)
            {
                spriteRenderer.sprite = s2;
                return;
            }
        }

        int tIndex = (int)Type;
        if (typeSprites != null && tIndex >= 0 && tIndex < typeSprites.Length)
        {
            var s3 = typeSprites[tIndex];
            if (s3 != null && spriteRenderer.sprite != s3)
            {
                spriteRenderer.sprite = s3;
                return;
            }
        }
    }

    IEnumerator GhostCycleCoroutine()
    {
        while (true)
        {
            GhostVisible = true;
            yield return new WaitForSeconds(3f);
            GhostVisible = false;
            yield return new WaitForSeconds(4f);
        }
    }

    public void SetInitialNetworkPosition(Vector2 pos)
    {
        if (Runner != null && Runner.IsServer)
        {
            NetworkedPosition = pos;
            transform.position = pos;
        }
    }

    public void SetPath(PathManager manager)
    {
        currentPath = manager;
        currentWaypointIndex = 0;
        reachedEnd = false;

        if (currentPath != null && currentPath.GetWaypoint(0) != null)
        {
            if (Runner != null && Runner.IsServer)
            {
                Vector3 startPos = currentPath.GetWaypoint(0).position;
                transform.position = startPos;
                NetworkedPosition = (Vector2)startPos;
            }
            else
            {
                transform.position = currentPath.GetWaypoint(0).position;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner != null && Runner.IsServer)
        {
            MoveOnServer();
        }
        else
        {
            float interp = 0.2f;
            if (Runner != null) interp = Mathf.Clamp01((float)Runner.DeltaTime * 8f);

            Vector2 cur = transform.position;
            Vector2 target = NetworkedPosition;
            Vector2 newPos = Vector2.Lerp(cur, target, interp);
            transform.position = newPos;
        }

        ApplySpriteFromIndex();
    }

    private void MoveOnServer()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) return;
        }

        if (currentPath == null || reachedEnd || IsFrozen) return;

        var waypoint = currentPath.GetWaypoint(currentWaypointIndex);
        if (waypoint == null) return;

        Vector2 dir = ((Vector2)waypoint.position - rb.position);
        float dist = dir.magnitude;

        if (dist < 0.1f)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= currentPath.GetWaypointCount())
            {
                if (currentPath.HasBranches)
                {
                    int branchToUse = GetBranchToUse();
                    ApplyBranchSelection(branchToUse);
                    return;
                }

                reachedEnd = true;
                return;
            }
        }

        dir.Normalize();
        Vector2 newPos = rb.position + dir * Speed * (float)Runner.DeltaTime;
        rb.MovePosition(newPos);
        NetworkedPosition = newPos;
    }

    private int GetBranchToUse()
    {
        if (currentPath == null || !currentPath.HasBranches) return 0;
        
        // Sprawdź czy jest wymuszona gałąź dla tej ścieżki
        int forcedBranch = PathBranchGate.GetForcedBranchForPath(currentPath);
        
        if (forcedBranch >= 0 && forcedBranch < currentPath.GetBranchCount())
        {
            Debug.Log($"[EnemyAI] Using forced branch {forcedBranch} for path {currentPath.name}");
            return forcedBranch;
        }
        
        Debug.Log($"[EnemyAI] Using default branch 0 for path {currentPath.name}");
        return 0;
    }

    private void ApplyBranchSelection(int branchIndex)
    {
        if (currentPath == null || !currentPath.HasBranches) return;

        var newBranch = currentPath.GetBranch(branchIndex);
        if (newBranch == null) 
        {
            Debug.LogWarning($"[EnemyAI] Branch {branchIndex} is null for path {currentPath.name}");
            return;
        }

        int nearest = newBranch.FindWaypointIndexByPosition(transform.position, 1.0f);
        if (nearest < 0) nearest = 0;

        currentPath = newBranch;
        currentWaypointIndex = nearest;
        reachedEnd = false;

        Debug.Log($"[EnemyAI] ApplyBranchSelection -> switched to {currentPath.name} startIndex={currentWaypointIndex} (enemy={name})");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetBranch(int branchIndex)
    {
        if (!Runner.IsServer) return;
        if (currentPath == null) return;
        if (!currentPath.HasBranches) return;

        Debug.Log($"[EnemyAI] RPC_SetBranch received -> branch {branchIndex} for enemy {name} on path {currentPath.name}");
        
        if (branchIndex >= 0 && branchIndex < currentPath.GetBranchCount())
        {
            ApplyBranchSelection(branchIndex);
        }
        else
        {
            Debug.LogWarning($"[EnemyAI] RPC_SetBranch: branch index {branchIndex} is out of range for path {currentPath.name}");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float dmg)
    {
        if (!Runner.IsServer) return;
        HP -= dmg;
        if (HP <= 0)
            Runner.Despawn(Object);
    }
}