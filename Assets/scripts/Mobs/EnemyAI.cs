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

    [Networked] public bool IsFrozen { get; set; } = false;

    private Rigidbody2D rb;
    private PathManager path;
    private int currentWaypointIndex = 0;
    private bool reachedEnd = false;

    float originalSpeed = -1f;

    [Header("Visuals")]
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
            // keep server authoritative physics: clients do interpolation, server moves
            rb.isKinematic = !Runner.IsServer;
        }

        if (spriteRenderer == null && visualsRoot != null)
        {
            spriteRenderer = visualsRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }
        else if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        //start ghost cycle on server (which toggles GhostVisible networked)
        if (Runner != null && Runner.IsServer && Type == EnemyType.Ghost)
        {
            StartCoroutine(GhostCycleCoroutine());
        }

        ApplySpriteFromIndex();

        if (Runner != null && Runner.IsServer)
            NetworkedPosition = transform.position;
    }

    public void InitStats(EnemyType type)
    {
        Type = type;

        SpriteIndex = (int)type;

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
                HP = 3f; Speed = 2f; Attack = 1f;
                break;
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
            if (s != null)
                spriteRenderer.sprite = s;
        }
        else
        {
            if (typeSprites != null && SpriteIndex >= 0 && SpriteIndex < typeSprites.Length)
            {
                spriteRenderer.sprite = typeSprites[SpriteIndex];
            }
        }

        if (Type == EnemyType.Ghost)
        {
            spriteRenderer.enabled = GhostVisible;
        }
        else
        {
            spriteRenderer.enabled = true;
        }


        if (IsFrozen)
        {
            spriteRenderer.color = new Color(0.65f, 0.8f, 1f, 1f);
        }
        else
        {
            spriteRenderer.color = Color.white;
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
            ApplySpriteFromIndex();
        }
    }

    public void SetPath(PathManager manager)
    {
        path = manager;
        currentWaypointIndex = 0;
        reachedEnd = false;
        if (path != null && path.GetWaypoint(0) != null)
        {
            if (Runner != null && Runner.IsServer)
            {
                Vector3 startPos = path.GetWaypoint(0).position;
                transform.position = startPos;
                NetworkedPosition = (Vector2)startPos;
            }
            else
            {
                transform.position = path.GetWaypoint(0).position;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

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
        if (IsFrozen) return;

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
                return;
            }
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
