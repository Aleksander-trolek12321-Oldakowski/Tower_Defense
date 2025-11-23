using Fusion;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : NetworkBehaviour
{
    [Networked] public float HP { get; set; }
    [Networked] public float Speed { get; set; }
    [Networked] public float Attack { get; set; }
    [Networked] public EnemyType Type { get; set; }

    [Networked] public Vector2 NetworkedPosition { get; set; }
    [Networked] public int SpriteIndex { get; set; } = 0;
    [Networked] public bool GhostVisible { get; set; } = true;

    private Rigidbody2D rb;
    private PathManager path;
    private int currentWaypointIndex = 0;
    private bool reachedEnd = false;

    // --- NEW: obsługa gałęzi w PathManagerze ---
    private PathManager currentPath;
    private bool waitingForBranchChoice = false;

    [Header("Visuals")]
    public GameObject visualsRoot;
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public Sprite[] typeSprites;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.isKinematic = !Runner.IsServer;
        }

        if (Runner != null && Runner.IsServer && Type == EnemyType.Ghost)
        {
            StartCoroutine(GhostCycleCoroutine());
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        ApplySpriteFromIndex();

        if (Runner != null && Runner.IsServer)
            NetworkedPosition = transform.position;
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
        }
    }

    void ApplySpriteFromIndex()
    {
        if (spriteRenderer == null) return;
        var mgr = Networking.GamePlayManager.Instance;
        if (mgr != null && mgr.enemyTypeSprites != null && SpriteIndex >= 0 && SpriteIndex < mgr.enemyTypeSprites.Length)
        {
            var s = mgr.enemyTypeSprites[SpriteIndex];
            if (s != null)
                spriteRenderer.sprite = s;
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

    int GetLocalPlayerTeam()
    {
        var localPn = Networking.PlayerNetwork.Local;
        if (localPn != null) return localPn.Team;

        // fallback scanning
        var pns = FindObjectsOfType<MonoBehaviour>();
        foreach (var mb in pns)
        {
            var t = mb.GetType();
            if (t.Name == "PlayerNetwork")
            {
                var prop = t.GetProperty("Team");
                if (prop != null)
                {
                    try
                    {
                        var val = prop.GetValue(mb);
                        if (val is int) return (int)val;
                    }
                    catch { }
                }
            }
        }
        return -1;
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
        path = manager;
        currentPath = manager;            // NEW
        currentWaypointIndex = 0;
        reachedEnd = false;
        waitingForBranchChoice = false;   // NEW

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

    // --- NEW: wybór konkretnej gałęzi dla TEGO moba ---
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetBranch(int branchIndex)
    {
        if (!Runner.IsServer) return;
        if (currentPath == null) return;
        if (!currentPath.HasBranches) return;

        var newBranch = currentPath.GetBranch(branchIndex);
        if (newBranch == null) return;

        currentPath = newBranch;
        currentWaypointIndex = 0;
        reachedEnd = false;
        waitingForBranchChoice = false;

        var wp0 = currentPath.GetWaypoint(0);
        if (wp0 != null)
        {
            Vector2 startPos = wp0.position;
            rb.position = startPos;
            NetworkedPosition = startPos;
        }
    }

    private void MoveOnServer()
    {
        if (currentPath == null || reachedEnd || waitingForBranchChoice) return;

        var waypoint = currentPath.GetWaypoint(currentWaypointIndex);
        if (waypoint == null) return;

        Vector2 dir = ((Vector2)waypoint.position - rb.position);
        float dist = dir.magnitude;

        if (dist < 0.1f)
        {
            currentWaypointIndex++;

            // koniec obecnej ścieżki
            if (currentWaypointIndex >= currentPath.GetWaypointCount())
            {
                // jeśli są gałęzie – zatrzymaj się i czekaj na wybór z UI
                if (currentPath.HasBranches)
                {
                    waitingForBranchChoice = true;

                    PathBranchUI.ShowForEnemy(this, currentPath);

                    return;
                }

                // brak gałęzi – koniec drogi
                reachedEnd = true;
                return;
            }

            waypoint = currentPath.GetWaypoint(currentWaypointIndex);
            if (waypoint == null) return;

            dir = ((Vector2)waypoint.position - rb.position);
        }

        dir.Normalize();
        Vector2 newPos = rb.position + dir * Speed * (float)Runner.DeltaTime;
        rb.MovePosition(newPos);
        NetworkedPosition = newPos;
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
