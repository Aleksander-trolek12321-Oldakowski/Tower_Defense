using System.Collections;
using Fusion;
using UnityEngine;
using Networking;

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

    [Networked] public bool HalfwayRewardGiven { get; set; }
    [Networked] public bool BaseAttackRewardGiven { get; set; }

    private Rigidbody2D rb;
    private PathManager currentPath;
    private int currentWaypointIndex = 0;
    private bool reachedEnd = false;

    [Header("Visuals (fallback)")]
    public GameObject visualsRoot;
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public Sprite[] typeSprites;

    [Header("Frozen visuals")]
    public GameObject frozenSpriteChild;
    private bool lastObservedFrozen = false;

    private bool lastObservedFrozenServer = false;

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

        if (Runner != null && Runner.IsServer && Type == EnemyType.Ghost)
        {
            StartCoroutine(GhostCycleCoroutine());
        }

        if (Runner != null && Runner.IsServer)
            NetworkedPosition = transform.position;

        ApplySpriteFromIndex();

        if (frozenSpriteChild != null)
        {
            frozenSpriteChild.SetActive(IsFrozen);
        }

        lastObservedFrozen = IsFrozen;
        lastObservedFrozenServer = IsFrozen;
    }

    public void InitStats(EnemyType type)
    {
        Type = type;
        switch (type)
        {
            case EnemyType.Werewolf:
                HP = 4f; Speed = 3.5f; Attack = 2f;
                break;
            case EnemyType.Zombie:
                HP = 2f; Speed = 2.5f; Attack = 1f;
                break;
            case EnemyType.Ork:
                HP = 10f; Speed = 1.5f; Attack = 4f;
                break;
            case EnemyType.Ghost:
                HP = 2f; Speed = 6.5f; Attack = 1f;
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
        HandleFrozenStateChange();

        if (Runner != null && Runner.IsServer)
        {
            if (lastObservedFrozenServer != IsFrozen)
            {
                lastObservedFrozenServer = IsFrozen;
                RPC_SyncFrozen(IsFrozen);
            }
        }

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

        if (frozenSpriteChild != null && frozenSpriteChild.activeSelf != IsFrozen)
            frozenSpriteChild.SetActive(IsFrozen);

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

        int forcedBranch = PathBranchGate.GetForcedBranchForPath(currentPath);

        if (forcedBranch >= 0 && forcedBranch < currentPath.GetBranchCount())
        {
            Debug.Log($"[EnemyAI] Using forced branch {forcedBranch} for path {currentPath.name}");
            return forcedBranch;
        }

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Runner.IsServer) return;

        if (other.CompareTag("HalfwayTrigger") && !HalfwayRewardGiven)
        {
            HalfwayRewardGiven = true;
            GameRoundManager.Instance?.RewardAttackerForDistance(true);
            Debug.Log($"[EnemyAI] Halfway reward given for {name}");
        }

        if (other.CompareTag("BaseAttackTrigger") && !BaseAttackRewardGiven)
        {
            BaseAttackRewardGiven = true;
            GameRoundManager.Instance?.RewardAttackerForDistance(false);
            Debug.Log($"[EnemyAI] Base attack reward given for {name}");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float dmg)
    {
        if (!Runner.IsServer) return;
        HP -= dmg;
        if (HP <= 0)
        {
            var players = FindObjectsOfType<PlayerNetwork>();
            foreach (var player in players)
            {
                if (player.Team == 0)
                {
                    GameRoundManager.Instance?.RewardDefenderForKill();
                }
            }
            Runner.Despawn(Object);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncFrozen(bool isFrozen, RpcInfo info = default)
    {
        if (frozenSpriteChild != null)
        {
            frozenSpriteChild.SetActive(isFrozen);
        }

        if (animator != null)
        {
            animator.enabled = !isFrozen;
        }

        lastObservedFrozen = isFrozen;
    }

    private void HandleFrozenStateChange()
    {
        if (lastObservedFrozen == IsFrozen) return;

        lastObservedFrozen = IsFrozen;

        if (frozenSpriteChild != null)
        {
            frozenSpriteChild.SetActive(IsFrozen);
        }
        if (animator != null)
        {
            animator.enabled = !IsFrozen;
        }

        Debug.Log($"[EnemyAI] {name} frozen state changed -> {IsFrozen}");
    }
}
